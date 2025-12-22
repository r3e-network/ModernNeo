// Copyright (C) 2015-2025 The Neo Project.
//
// RemoteNodeGrain.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Orleans.Interfaces;
using Neo.Orleans.Services;
using Neo.Orleans.States;
using Orleans.Runtime;

namespace Neo.Orleans.Grains
{
    /// <summary>
    /// Orleans Grain implementation for remote peer connection management.
    /// Replaces Akka.NET RemoteNode Actor.
    /// </summary>
    /// <remarks>
    /// Key differences from Akka.NET implementation:
    /// - State is persisted via Orleans grain storage
    /// - Message handling is async/await based instead of actor mailbox
    /// - Connection lifecycle managed through grain activation/deactivation
    /// </remarks>
    public class RemoteNodeGrain : Grain, IRemoteNodeGrain
    {
        private readonly IPersistentState<RemoteNodeState> _state;
        private readonly IGrainFactory _grainFactory;
        private readonly ITransportService? _transportService;

        public RemoteNodeGrain(
            [PersistentState("remotenode", "RemoteNodeStore")]
            IPersistentState<RemoteNodeState> state,
            IGrainFactory grainFactory,
            ITransportService? transportService = null)
        {
            _state = state;
            _grainFactory = grainFactory;
            _transportService = transportService;
        }

        /// <summary>
        /// Called when the grain is activated.
        /// Parses the grain key to extract address and port.
        /// </summary>
        public override Task OnActivateAsync(CancellationToken cancellationToken)
        {
            // Grain key format: "address:port"
            var key = this.GetPrimaryKeyString();
            var parts = key.Split(':');
            if (parts.Length == 2)
            {
                _state.State.Address = parts[0];
                if (int.TryParse(parts[1], out var port))
                {
                    _state.State.Port = port;
                }
            }

            if (_state.State.ConnectionState == (int)ConnectionState.Disconnected)
            {
                _state.State.ConnectionState = (int)ConnectionState.Connecting;
                _state.State.ConnectedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            }

            return base.OnActivateAsync(cancellationToken);
        }

        /// <summary>
        /// Called when the grain is deactivated.
        /// Ensures proper cleanup of connection state.
        /// </summary>
        public override async Task OnDeactivateAsync(DeactivationReason reason, CancellationToken cancellationToken)
        {
            // Mark as disconnected on deactivation
            _state.State.ConnectionState = (int)ConnectionState.Disconnected;
            await _state.WriteStateAsync();
            await base.OnDeactivateAsync(reason, cancellationToken);
        }

        /// <summary>
        /// Handles an incoming protocol message.
        /// </summary>
        public async Task HandleMessageAsync(byte[] message)
        {
            if (message == null || message.Length == 0)
                return;

            _state.State.LastMessageAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            // Process message based on type (first byte is command)
            var command = message[0];

            // Handle version acknowledgment
            if (command == 0x01) // VERSION_ACK placeholder
            {
                _state.State.VersionAcknowledged = true;
                _state.State.ConnectionState = (int)ConnectionState.Active;
            }

            // Handle height update (simplified)
            if (command == 0x02 && message.Length >= 5) // HEIGHT_UPDATE placeholder
            {
                _state.State.RemoteHeight = BitConverter.ToUInt32(message, 1);
            }

            // Clear awaiting ack flag
            _state.State.AwaitingAck = false;

            await _state.WriteStateAsync();

            // Process any queued outbound messages
            await ProcessOutboundQueueAsync();
        }

        /// <summary>
        /// Sends a message to the remote peer.
        /// </summary>
        public async Task SendAsync(byte[] message)
        {
            if (message == null || message.Length == 0)
                return;

            // If not active or awaiting ack, queue the message
            if (_state.State.ConnectionState != (int)ConnectionState.Active ||
                _state.State.AwaitingAck)
            {
                _state.State.OutboundQueue.Enqueue(message);
                await _state.WriteStateAsync();
                return;
            }

            // Send immediately
            await SendImmediateAsync(message);
        }

        /// <summary>
        /// Gets the connection state.
        /// </summary>
        public Task<ConnectionState> GetStateAsync()
        {
            return Task.FromResult((ConnectionState)_state.State.ConnectionState);
        }

        /// <summary>
        /// Disconnects from the remote peer.
        /// </summary>
        public async Task DisconnectAsync()
        {
            _state.State.ConnectionState = (int)ConnectionState.Disconnected;
            _state.State.OutboundQueue.Clear();
            _state.State.KnownHashes.Clear();

            await _state.WriteStateAsync();

            // Notify LocalNodeGrain about disconnection
            var localNode = _grainFactory.GetGrain<ILocalNodeGrain>(0);
            await localNode.UnregisterPeerAsync(_state.State.Address, _state.State.Port);

            // Deactivate this grain
            DeactivateOnIdle();
        }

        /// <summary>
        /// Gets the remote peer's reported height.
        /// </summary>
        public Task<uint> GetRemoteHeightAsync()
        {
            return Task.FromResult(_state.State.RemoteHeight);
        }

        /// <summary>
        /// Checks if an inventory hash is known.
        /// </summary>
        public Task<bool> IsKnownHashAsync(byte[] hash)
        {
            var hashKey = Convert.ToBase64String(hash);
            return Task.FromResult(_state.State.KnownHashes.Contains(hashKey));
        }

        /// <summary>
        /// Adds an inventory hash to the known set.
        /// </summary>
        public async Task AddKnownHashAsync(byte[] hash)
        {
            var hashKey = Convert.ToBase64String(hash);

            if (_state.State.KnownHashes.Contains(hashKey))
                return;

            _state.State.KnownHashes.Add(hashKey);

            // Prune if over limit
            if (_state.State.KnownHashes.Count > _state.State.MaxKnownHashes)
            {
                // Remove oldest entries (approximation - HashSet doesn't preserve order)
                var toRemove = _state.State.KnownHashes
                    .Take(_state.State.KnownHashes.Count - _state.State.MaxKnownHashes / 2)
                    .ToList();
                foreach (var item in toRemove)
                {
                    _state.State.KnownHashes.Remove(item);
                }
            }

            await _state.WriteStateAsync();
        }

        /// <summary>
        /// Initiates the version handshake.
        /// </summary>
        public async Task StartHandshakeAsync(uint localHeight, uint nonce, string userAgent)
        {
            _state.State.ConnectionState = (int)ConnectionState.Handshaking;
            _state.State.Nonce = nonce;

            // Create version message (simplified)
            var versionMessage = CreateVersionMessage(localHeight, nonce, userAgent);
            await SendImmediateAsync(versionMessage);

            await _state.WriteStateAsync();
        }

        /// <summary>
        /// Updates connection info after successful handshake.
        /// </summary>
        public async Task CompleteHandshakeAsync(uint remoteHeight, int listenerPort, bool isFullNode, string userAgent)
        {
            _state.State.RemoteHeight = remoteHeight;
            _state.State.ListenerPort = listenerPort;
            _state.State.IsFullNode = isFullNode;
            _state.State.UserAgent = userAgent;
            _state.State.VersionAcknowledged = true;
            _state.State.ConnectionState = (int)ConnectionState.Active;

            await _state.WriteStateAsync();

            // Register with LocalNodeGrain
            var localNode = _grainFactory.GetGrain<ILocalNodeGrain>(0);
            await localNode.RegisterPeerAsync(_state.State.Address, _state.State.Port, remoteHeight);
        }

        #region Private Methods

        private async Task SendImmediateAsync(byte[] message)
        {
            _state.State.AwaitingAck = true;
            _state.State.LastMessageAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            await _state.WriteStateAsync();

            // Send via transport service if available
            if (_transportService != null)
            {
                var success = await _transportService.SendAsync(
                    _state.State.Address,
                    _state.State.Port,
                    message);

                if (!success)
                {
                    // Transport failed - mark connection as disconnected
                    _state.State.ConnectionState = (int)ConnectionState.Disconnected;
                    _state.State.AwaitingAck = false;
                    await _state.WriteStateAsync();
                }
            }
            // If no transport service, operate in local-only mode (for testing)
        }

        private async Task ProcessOutboundQueueAsync()
        {
            while (_state.State.OutboundQueue.Count > 0 &&
                   _state.State.ConnectionState == (int)ConnectionState.Active &&
                   !_state.State.AwaitingAck)
            {
                var message = _state.State.OutboundQueue.Dequeue();
                await SendImmediateAsync(message);
            }
        }

        private static byte[] CreateVersionMessage(uint height, uint nonce, string userAgent)
        {
            // Simplified version message format:
            // [0]: Command (0x00 = VERSION)
            // [1-4]: Height (uint32)
            // [5-8]: Nonce (uint32)
            // [9+]: UserAgent (UTF8)

            var userAgentBytes = System.Text.Encoding.UTF8.GetBytes(userAgent ?? string.Empty);
            var message = new byte[9 + userAgentBytes.Length];

            message[0] = 0x00; // VERSION command
            BitConverter.GetBytes(height).CopyTo(message, 1);
            BitConverter.GetBytes(nonce).CopyTo(message, 5);
            userAgentBytes.CopyTo(message, 9);

            return message;
        }

        #endregion
    }
}
