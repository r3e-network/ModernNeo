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

using Neo;
using Neo.Cryptography;
using Neo.Extensions;
using Neo.IO;
using Neo.IO.Caching;
using Neo.Network.P2P;
using Neo.Network.P2P.Capabilities;
using Neo.Network.P2P.Payloads;
using Neo.Orleans.Hosting;
using Neo.Orleans.Interfaces;
using Neo.Orleans.Options;
using Neo.Orleans.Services;
using Neo.Orleans.States;
using Neo.Orleans.Utilities;
using Neo.SmartContract.Native;
using Orleans;
using Orleans.Runtime;
using System;
using System.Buffers.Binary;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;

#pragma warning disable CS0618 // OrleansOptions is obsolete during migration
namespace Neo.Orleans.Grains
{
    /// <summary>
    /// Orleans Grain implementation for remote peer connection management.
    /// Manages connection lifecycle and peer state.
    /// </summary>
    /// <remarks>
    /// Key differences from the legacy implementation:
    /// - State is persisted via Orleans grain storage
    /// - Message handling is async/await based instead of actor mailbox
    /// - Connection lifecycle managed through grain activation/deactivation
    /// </remarks>
    public class RemoteNodeGrain : Grain, IRemoteNodeGrain
    {
        private readonly IPersistentState<RemoteNodeState> _state;
        private readonly IGrainFactory _grainFactory;
        private readonly INeoSystem _system;
        private readonly ITransportService? _transportService;
        private readonly IOrleansOptions _options;
        private IGrainTimer? _pingTimer;
        private uint _lastHeightSent;
        private DateTime _lastSent = TimeProvider.Current.UtcNow;
        private static readonly TimeSpan PingInterval = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan PingIdleThreshold = TimeSpan.FromMinutes(1);
        private static readonly TimeSpan PendingKnownHashTimeout = TimeSpan.FromMinutes(1);
        private const int ProtocolViolationScore = 100;
        private const int MisbehaviorDisconnectThreshold = 100;
        private BloomFilter? _bloomFilter;
        private readonly Dictionary<UInt256, DateTime> _pendingKnownHashes = new();
        private HashSetCache<UInt256>? _knownHashes;
        private HashSetCache<UInt256>? _sentHashes;

        public RemoteNodeGrain(
            [PersistentState("remotenode", "RemoteNodeStore")]
            IPersistentState<RemoteNodeState> state,
            IGrainFactory grainFactory,
            INeoSystem system,
            ITransportService? transportService = null,
            IOrleansOptions? options = null)
        {
            _state = state;
            _grainFactory = grainFactory;
            _system = system;
            _transportService = transportService;
            _options = options ?? new OrleansOptions();
        }

        /// <summary>
        /// Called when the grain is activated.
        /// Parses the grain key to extract address and port.
        /// </summary>
        public override Task OnActivateAsync(CancellationToken cancellationToken)
        {
            // Grain key format: "address:port"
            var key = this.GetPrimaryKeyString();
            if (IPEndPoint.TryParse(key, out var endPoint))
            {
                _state.State.Address = endPoint.Address.ToString();
                _state.State.Port = endPoint.Port;
            }
            else
            {
                var parts = key.Split(':');
                if (parts.Length == 2)
                {
                    _state.State.Address = parts[0];
                    if (int.TryParse(parts[1], out var port))
                    {
                        _state.State.Port = port;
                    }
                }
            }

            if (_state.State.ConnectionState == (int)ConnectionState.Disconnected)
            {
                _state.State.ConnectionState = (int)ConnectionState.Connecting;
                _state.State.ConnectedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                _state.State.VersionSent = false;
                _state.State.VersionAcknowledged = false;
                _state.State.VersionReceived = false;
                _state.State.MempoolSent = false;
                _state.State.GetAddrSent = false;
                _state.State.MisbehaviorScore = 0;
                _lastHeightSent = 0;
            }

            _state.State.EnableCompression = _options.EnableCompression;
            _lastSent = TimeProvider.Current.UtcNow;

            if (_options.MaxKnownHashes > 0)
                _state.State.MaxKnownHashes = _options.MaxKnownHashes;

            var maxKnownHashes = Math.Max(1, _state.State.MaxKnownHashes);
            _knownHashes = new HashSetCache<UInt256>(maxKnownHashes);
            _sentHashes = new HashSetCache<UInt256>(maxKnownHashes);
            _pendingKnownHashes.Clear();

            _pingTimer ??= this.RegisterGrainTimer(
                _ => OnTimerAsync(),
                new GrainTimerCreationOptions
                {
                    DueTime = PingInterval,
                    Period = PingInterval,
                    Interleave = true
                });

            return base.OnActivateAsync(cancellationToken);
        }

        /// <summary>
        /// Called when the grain is deactivated.
        /// Ensures proper cleanup of connection state.
        /// </summary>
        public override async Task OnDeactivateAsync(DeactivationReason reason, CancellationToken cancellationToken)
        {
            _pingTimer?.Dispose();
            _pingTimer = null;
            _bloomFilter = null;
            _pendingKnownHashes.Clear();
            _knownHashes = null;
            _sentHashes = null;

            var isShuttingDown = cancellationToken.IsCancellationRequested ||
                reason.ReasonCode == DeactivationReasonCode.ShuttingDown;

            if (_state.State.ConnectionState != (int)ConnectionState.Disconnected)
            {
                var address = _state.State.Address;
                var port = _state.State.Port;

                _state.State.ConnectionState = (int)ConnectionState.Disconnected;
                _state.State.VersionAcknowledged = false;
                _state.State.VersionSent = false;
                _state.State.VersionReceived = false;
                _state.State.AwaitingAck = false;
                _state.State.MempoolSent = false;
                _state.State.GetAddrSent = false;
                _state.State.OutboundQueue.Clear();
                _state.State.KnownHashes.Clear();
                _state.State.MisbehaviorScore = 0;

                await _state.WriteStateAsync();

                if (_transportService != null && !isShuttingDown)
                {
                    try
                    {
                        await _transportService.DisconnectAsync(address, port);
                    }
                    catch
                    {
                        // Ignore transport shutdown errors.
                    }
                }

                if (!isShuttingDown)
                {
                    var localNode = _grainFactory.GetGrain<ILocalNodeGrain>(0);
                    await localNode.UnregisterPeerAsync(address, port);
                    await UnregisterTaskManagerSessionAsync();
                }
            }
            else
            {
                await _state.WriteStateAsync();
            }
            await base.OnDeactivateAsync(reason, cancellationToken);
        }

        /// <summary>
        /// Handles an incoming protocol message.
        /// </summary>
        /// <param name="message">The message bytes received.</param>
        public async Task HandleMessageAsync(byte[] message)
        {
            if (message == null || message.Length == 0)
                return;

            _state.State.LastMessageAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var updatedHeight = false;
            var shouldRegister = false;
            var shouldSendVersion = false;
            var shouldSendVerack = false;
            var shouldSendAddr = false;
            uint? pingNonceToSend = null;

            if (!TryDeserializeMessage(message, _options.NetworkMagic, out var parsedMessage) || parsedMessage == null)
            {
                await HandleProtocolViolationAsync("Invalid message framing");
                return;
            }

            // Detect message format based on incoming message structure
            if (message.Length >= 24)
            {
                var potentialMagic = BitConverter.ToUInt32(message, 0);
                if (potentialMagic == _options.NetworkMagic)
                {
                    _state.State.UseCompactFormat = false;
                }
                else
                {
                    _state.State.UseCompactFormat = true;
                }
            }
            else
            {
                _state.State.UseCompactFormat = true;
            }


            if ((ConnectionState)_state.State.ConnectionState != ConnectionState.Active &&
                parsedMessage.Command != MessageCommand.Version &&
                parsedMessage.Command != MessageCommand.Verack)
            {
                await HandleProtocolViolationAsync($"Unexpected {parsedMessage.Command} before handshake");
                return;
            }

            switch (parsedMessage.Command)
            {
                case MessageCommand.Version:
                    if (_state.State.VersionReceived)
                    {
                        await HandleProtocolViolationAsync("Duplicate version message");
                        return;
                    }

                    _state.State.VersionReceived = true;
                    if (parsedMessage.Payload is VersionPayload version)
                    {

                        if (version.Network != _options.NetworkMagic)
                        {
                            await DisconnectAsync();
                            return;
                        }

                        var localNode = _grainFactory.GetGrain<ILocalNodeGrain>(0);
                        var allow = await localNode.AllowNewConnectionAsync(
                            version.Nonce,
                            version.Network,
                            _state.State.Address);
                        if (!allow)
                        {
                            await DisconnectAsync();
                            return;
                        }

                        _state.State.UserAgent = version.UserAgent;
                        _state.State.Nonce = version.Nonce;
                        _state.State.EnableCompression = _options.EnableCompression && version.AllowCompression;

                        FullNodeCapability? fullNode = null;
                        foreach (var cap in version.Capabilities)
                        {
                            if (cap is FullNodeCapability fnc)
                            {
                                fullNode = fnc;
                                break;
                            }
                        }
                        if (fullNode != null && _state.State.RemoteHeight != fullNode.StartHeight)
                        {
                            _state.State.RemoteHeight = fullNode.StartHeight;
                            updatedHeight = true;
                        }
                        _state.State.IsFullNode = fullNode != null;

                        ServerCapability? server = null;
                        foreach (var cap in version.Capabilities)
                        {
                            if (cap is ServerCapability sc && sc.Type == NodeCapabilityType.TcpServer)
                            {
                                server = sc;
                                break;
                            }
                        }
                        if (server != null)
                            _state.State.ListenerPort = server.Port;

                        _state.State.ConnectionState = (int)ConnectionState.Handshaking;
                        shouldSendVerack = true;
                        if (!_state.State.VersionSent)
                            shouldSendVersion = true;
                    }
                    break;
                case MessageCommand.Verack:
                    if (!_state.State.VersionReceived || _state.State.VersionAcknowledged)
                    {
                        await HandleProtocolViolationAsync("Unexpected verack message");
                        return;
                    }

                    _state.State.VersionAcknowledged = true;
                    _state.State.ConnectionState = (int)ConnectionState.Active;
                    shouldRegister = true;
                    await CompleteHandshakeAsync(_state.State.RemoteHeight, _state.State.ListenerPort, _state.State.IsFullNode, _state.State.UserAgent);
                    break;
                case MessageCommand.Inv:
                    if (parsedMessage.Payload is InvPayload invPayload)
                    {
                        await HandleInvAsync(invPayload);
                    }
                    break;
                case MessageCommand.GetHeaders:
                    if (parsedMessage.Payload is GetBlockByIndexPayload getHeadersPayload)
                    {
                        await HandleGetHeadersAsync(getHeadersPayload);
                    }
                    break;
                case MessageCommand.Headers:
                    if (parsedMessage.Payload is HeadersPayload headersPayload)
                    {
                        if (await HandleHeadersAsync(headersPayload))
                            updatedHeight = true;
                    }
                    break;
                case MessageCommand.GetBlocks:
                    if (parsedMessage.Payload is GetBlocksPayload getBlocksPayload)
                    {
                        await HandleGetBlocksAsync(getBlocksPayload);
                    }
                    break;
                case MessageCommand.GetBlockByIndex:
                    if (parsedMessage.Payload is GetBlockByIndexPayload getBlocksByIndexPayload)
                    {
                        await HandleGetBlocksByIndexAsync(getBlocksByIndexPayload);
                    }
                    break;
                case MessageCommand.GetData:
                    if (parsedMessage.Payload is InvPayload getDataPayload)
                    {
                        await HandleGetDataAsync(getDataPayload);
                    }
                    break;
                case MessageCommand.Block:
                    if (parsedMessage.Payload is Block block)
                    {
                        await HandleBlockAsync(block);
                    }
                    break;
                case MessageCommand.Transaction:
                    if (parsedMessage.Payload is Transaction transaction &&
                        transaction.Size <= Transaction.MaxTransactionSize)
                    {
                        await HandleTransactionAsync(transaction);
                    }
                    break;
                case MessageCommand.Extensible:
                    if (parsedMessage.Payload is ExtensiblePayload extensible)
                    {
                        await HandleExtensibleAsync(extensible);
                    }
                    break;
                case MessageCommand.NotFound:
                    break;
                case MessageCommand.Ping:
                    if (parsedMessage.Payload is PingPayload pingPayload)
                    {
                        if (_state.State.RemoteHeight != pingPayload.LastBlockIndex)
                        {
                            _state.State.RemoteHeight = pingPayload.LastBlockIndex;
                            updatedHeight = true;
                        }
                        pingNonceToSend = pingPayload.Nonce;
                    }
                    break;
                case MessageCommand.Pong:
                    if (parsedMessage.Payload is PingPayload ping &&
                        _state.State.RemoteHeight != ping.LastBlockIndex)
                    {
                        _state.State.RemoteHeight = ping.LastBlockIndex;
                        updatedHeight = true;
                    }
                    break;
                case MessageCommand.GetAddr:
                    shouldSendAddr = true;
                    break;
                case MessageCommand.Addr:
                    if (parsedMessage.Payload is AddrPayload addrPayload)
                    {
                        await HandleAddrAsync(addrPayload);
                    }
                    break;
                case MessageCommand.Mempool:
                    await HandleMempoolAsync();
                    break;
                case MessageCommand.FilterLoad:
                    if (parsedMessage.Payload is FilterLoadPayload filterLoadPayload)
                    {
                        HandleFilterLoad(filterLoadPayload);
                    }
                    break;
                case MessageCommand.FilterAdd:
                    if (parsedMessage.Payload is FilterAddPayload filterAddPayload)
                    {
                        HandleFilterAdd(filterAddPayload);
                    }
                    break;
                case MessageCommand.FilterClear:
                    HandleFilterClear();
                    break;
            }

            if (shouldSendVersion)
                await SendLocalVersionAsync();
            if (shouldSendVerack)
                await SendVerackAsync();
            if (pingNonceToSend.HasValue)
                await SendPongAsync(pingNonceToSend.Value);
            if (shouldSendAddr)
                await SendAddrAsync();

            // Clear awaiting ack flag
            _state.State.AwaitingAck = false;

            await _state.WriteStateAsync();

            if (updatedHeight || shouldRegister)
            {
                var localNode = _grainFactory.GetGrain<ILocalNodeGrain>(0);
                if (updatedHeight)
                    await localNode.UpdatePeerHeightAsync(_state.State.Address, _state.State.Port, _state.State.RemoteHeight);

                if (shouldRegister)
                {
                    var info = new PeerConnectionInfo(
                        _state.State.Address,
                        _state.State.Port,
                        _state.State.RemoteHeight,
                        _state.State.Nonce,
                        _state.State.UserAgent,
                        _state.State.IsFullNode,
                        _state.State.ListenerPort);
                    await localNode.RegisterPeerAsync(info);
                    await RegisterTaskManagerSessionAsync();
                }
                else if (updatedHeight)
                {
                    await UpdateTaskManagerPeerHeightAsync(_state.State.RemoteHeight);
                }
            }

            // Process any queued outbound messages
            await ProcessOutboundQueueAsync();
        }

        /// <summary>
        /// Sends a message to the remote peer.
        /// </summary>
        /// <param name="message">The message bytes to send.</param>
        public async Task SendAsync(byte[] message)
        {
            if (message == null || message.Length == 0)
                return;

            // If not active, queue the message
            if (_state.State.ConnectionState != (int)ConnectionState.Active)
            {
                EnqueueOutboundMessage(message);
                await _state.WriteStateAsync();
                return;
            }

            // Send immediately
            await SendImmediateAsync(message);
        }

        /// <summary>
        /// Sends a message to the remote peer using the negotiated compression settings.
        /// </summary>
        /// <param name="command">The message command.</param>
        /// <param name="payload">The optional payload to serialize.</param>
        public Task SendMessageAsync(MessageCommand command, ISerializable? payload = null)
        {
            var message = SerializeMessage(command, payload);
            return SendAsync(message);
        }

        /// <summary>
        /// Gets the connection state.
        /// </summary>
        /// <returns>The current connection state.</returns>
        public Task<ConnectionState> GetStateAsync()
        {
            return Task.FromResult((ConnectionState)_state.State.ConnectionState);
        }

        /// <summary>
        /// Disconnects from the remote peer.
        /// </summary>
        public async Task DisconnectAsync()
        {
            var address = _state.State.Address;
            var port = _state.State.Port;

            _bloomFilter = null;
            _state.State.ConnectionState = (int)ConnectionState.Disconnected;
            _state.State.VersionAcknowledged = false;
            _state.State.VersionSent = false;
            _state.State.VersionReceived = false;
            _state.State.AwaitingAck = false;
            _state.State.MempoolSent = false;
            _state.State.GetAddrSent = false;
            _state.State.OutboundQueue.Clear();
            _state.State.KnownHashes.Clear();
            _state.State.MisbehaviorScore = 0;

            await _state.WriteStateAsync();

            if (_transportService != null)
            {
                try
                {
                    await _transportService.DisconnectAsync(address, port);
                }
                catch
                {
                    // Ignore transport shutdown errors.
                }
            }

            // Notify LocalNodeGrain about disconnection
            var localNode = _grainFactory.GetGrain<ILocalNodeGrain>(0);
            await localNode.UnregisterPeerAsync(address, port);
            await UnregisterTaskManagerSessionAsync();

            // Deactivate this grain
            DeactivateOnIdle();
        }

        /// <summary>
        /// Gets the remote peer's reported height.
        /// </summary>
        /// <returns>The remote peer's blockchain height.</returns>
        public Task<uint> GetRemoteHeightAsync()
        {
            return Task.FromResult(_state.State.RemoteHeight);
        }

        /// <summary>
        /// Checks if an inventory hash is known.
        /// </summary>
        /// <param name="hash">The hash to check (32 bytes).</param>
        /// <returns>True if the hash is known, false otherwise.</returns>
        public Task<bool> IsKnownHashAsync(byte[] hash)
        {
            if (hash == null || hash.Length != UInt256.Length)
                return Task.FromResult(false);

            var key = new UInt256(hash);
            return Task.FromResult(_knownHashes?.Contains(key) == true);
        }

        /// <summary>
        /// Adds an inventory hash to the known set.
        /// </summary>
        /// <param name="hash">The hash to add (32 bytes).</param>
        public Task AddKnownHashAsync(byte[] hash)
        {
            if (hash == null || hash.Length != UInt256.Length)
                return Task.CompletedTask;

            var key = new UInt256(hash);
            _knownHashes?.TryAdd(key);
            return Task.CompletedTask;
        }

        /// <summary>
        /// Initiates the version handshake.
        /// </summary>
        /// <param name="localHeight">The local blockchain height.</param>
        /// <param name="nonce">The connection nonce.</param>
        /// <param name="userAgent">The user agent string.</param>
        public async Task StartHandshakeAsync(uint localHeight, uint nonce, string userAgent)
        {

            _state.State.ConnectionState = (int)ConnectionState.Handshaking;
            _state.State.Nonce = nonce;
            _state.State.VersionSent = true;
            _state.State.MempoolSent = false;

            var handshakeUserAgent = string.IsNullOrWhiteSpace(userAgent) ? _options.UserAgent : userAgent;
            var capabilities = new List<NodeCapability>
            {
                new FullNodeCapability(localHeight)
            };
            if (!_options.EnableCompression)
            {
                capabilities.Add(new DisableCompressionCapability());
            }

            var localNode = _grainFactory.GetGrain<ILocalNodeGrain>(0);
            var summary = await localNode.GetStateSummaryAsync();
            if (summary.ListenerPort > 0 && summary.ListenerPort <= ushort.MaxValue)
            {
                capabilities.Add(new ServerCapability(NodeCapabilityType.TcpServer, (ushort)summary.ListenerPort));
            }
            var wsPort = _options.WsPort;
            if (_options.WsEnabled && wsPort > 0 && wsPort <= ushort.MaxValue &&
                wsPort != summary.ListenerPort)
            {
#pragma warning disable CS0612 // Type or member is obsolete
                capabilities.Add(new ServerCapability(NodeCapabilityType.WsServer, (ushort)wsPort));
#pragma warning restore CS0612 // Type or member is obsolete
            }

            var version = VersionPayload.Create(_options.NetworkMagic, nonce, handshakeUserAgent, capabilities.ToArray());
            version.Version = _options.ProtocolVersion;
            var versionMessage = SerializeMessage(MessageCommand.Version, version);
            await SendImmediateAsync(versionMessage);

            await _state.WriteStateAsync();
        }

        /// <summary>
        /// Updates connection info after successful handshake.
        /// </summary>
        /// <param name="remoteHeight">The remote peer's reported height.</param>
        /// <param name="listenerPort">The remote peer's listener port.</param>
        /// <param name="isFullNode">Whether the remote is a full node.</param>
        /// <param name="userAgent">The remote peer's user agent.</param>
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
            var info = new PeerConnectionInfo(
                _state.State.Address,
                _state.State.Port,
                _state.State.RemoteHeight,
                _state.State.Nonce,
                _state.State.UserAgent,
                _state.State.IsFullNode,
                _state.State.ListenerPort);
            await localNode.RegisterPeerAsync(info);
            await RegisterTaskManagerSessionAsync();

            await ProcessOutboundQueueAsync();
        }

        #region Private Methods

        private async Task SendImmediateAsync(byte[] message)
        {
            if (message.Length > 0)
            {
                var displayLen = Math.Min(24, message.Length);
            }

            TrackSentCommand(message);
            _lastSent = TimeProvider.Current.UtcNow;
            _state.State.LastMessageAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            // Send via transport service if available
            if (_transportService == null)
            {
                _state.State.AwaitingAck = false;
                await _state.WriteStateAsync();
                return;
            }

            _state.State.AwaitingAck = false;
            await _state.WriteStateAsync();

            message = AdjustCompressionIfNeeded(message);

            var success = await _transportService.SendAsync(
                _state.State.Address,
                _state.State.Port,
                message);


            if (!success)
            {
                await DisconnectAsync();
            }
        }

        private void EnqueueOutboundMessage(byte[] message)
        {
            TrackSentCommand(message);
            _lastSent = TimeProvider.Current.UtcNow;
            _state.State.OutboundQueue.Enqueue(message);
            var maxQueue = _state.State.MaxOutboundQueue;
            if (maxQueue > 0)
            {
                while (_state.State.OutboundQueue.Count > maxQueue)
                {
                    _state.State.OutboundQueue.Dequeue();
                }
            }
        }

        private async Task ProcessOutboundQueueAsync()
        {
            while (_state.State.OutboundQueue.Count > 0 &&
                   _state.State.ConnectionState == (int)ConnectionState.Active)
            {
                var message = _state.State.OutboundQueue.Dequeue();
                await SendImmediateAsync(message);
            }
        }

        private void TrackSentCommand(byte[] message)
        {
            if (message.Length < 2)
                return;

            var command = (MessageCommand)message[1];
            if (!Enum.IsDefined(typeof(MessageCommand), command))
                return;

            if (command == MessageCommand.GetAddr)
                _state.State.GetAddrSent = true;
        }

        private async Task OnTimerAsync()
        {
            CleanupPendingKnownHashes();
            await TrySendPingAsync();
        }

        private void CleanupPendingKnownHashes()
        {
            if (_pendingKnownHashes.Count == 0)
                return;

            var cutoff = TimeProvider.Current.UtcNow - PendingKnownHashTimeout;
            foreach (var (hash, time) in _pendingKnownHashes.ToList())
            {
                if (time <= cutoff)
                    _pendingKnownHashes.Remove(hash);
            }
        }

        private async Task TrySendPingAsync()
        {
            if (_state.State.ConnectionState != (int)ConnectionState.Active)
                return;

            if (TimeProvider.Current.UtcNow - _lastSent < PingIdleThreshold)
                return;

            var blockchain = _grainFactory.GetGrain<IBlockchainGrain>(0);
            var height = await blockchain.GetHeightAsync();
            if (height <= _lastHeightSent)
                return;

            _lastHeightSent = height;
            var payload = PingPayload.Create(height);
            var message = SerializeMessage(MessageCommand.Ping, payload);
            await SendAsync(message);
        }

        private async Task HandleInvAsync(InvPayload payload)
        {
            if (payload.Hashes.Length == 0)
                return;

            var hashes = new List<byte[]>(payload.Hashes.Length);
            var blockchain = _grainFactory.GetGrain<IBlockchainGrain>(0);
            var now = TimeProvider.Current.UtcNow;
            var sentHashes = _sentHashes;
            var knownHashes = _knownHashes;

            switch (payload.Type)
            {
                case InventoryType.Block:
                    foreach (var hash in payload.Hashes)
                    {
                        if (_pendingKnownHashes.ContainsKey(hash))
                            continue;
                        if (knownHashes != null && knownHashes.Contains(hash))
                            continue;
                        if (sentHashes != null && sentHashes.Contains(hash))
                            continue;

                        var bytes = hash.GetSpan().ToArray();
                        if (await blockchain.ContainsBlockAsync(bytes))
                            continue;
                        _pendingKnownHashes[hash] = now;
                        hashes.Add(bytes);
                    }
                    break;
                case InventoryType.TX:
                    foreach (var hash in payload.Hashes)
                    {
                        if (_pendingKnownHashes.ContainsKey(hash))
                            continue;
                        if (knownHashes != null && knownHashes.Contains(hash))
                            continue;
                        if (sentHashes != null && sentHashes.Contains(hash))
                            continue;

                        var bytes = hash.GetSpan().ToArray();
                        if (await blockchain.ContainsTransactionAsync(bytes))
                            continue;
                        _pendingKnownHashes[hash] = now;
                        hashes.Add(bytes);
                    }
                    break;
                default:
                    foreach (var hash in payload.Hashes)
                    {
                        if (_pendingKnownHashes.ContainsKey(hash))
                            continue;
                        if (knownHashes != null && knownHashes.Contains(hash))
                            continue;
                        if (sentHashes != null && sentHashes.Contains(hash))
                            continue;

                        _pendingKnownHashes[hash] = now;
                        hashes.Add(hash.GetSpan().ToArray());
                    }
                    break;
            }

            if (hashes.Count == 0)
                return;

            var taskManager = _grainFactory.GetGrain<ITaskManagerGrain>(0);
            await taskManager.NewTasksAsync(this.GetPrimaryKeyString(), (byte)payload.Type, hashes);
        }

        private async Task HandleGetHeadersAsync(GetBlockByIndexPayload payload)
        {
            var maxCount = payload.Count < 0
                ? HeadersPayload.MaxHeadersCount
                : Math.Min((int)payload.Count, HeadersPayload.MaxHeadersCount);
            if (maxCount <= 0)
                return;

            var blockchain = _grainFactory.GetGrain<IBlockchainGrain>(0);
            var currentHeight = await blockchain.GetHeightAsync();
            if (payload.IndexStart > currentHeight)
                return;

            var headerHeight = await blockchain.GetHeaderHeightAsync();

            var headers = new List<Header>(Math.Min(maxCount, 64));
            for (var i = 0; i < maxCount; i++)
            {
                var index = payload.IndexStart + (uint)i;
                if (index > headerHeight)
                    break;

                var header = await TryGetHeaderAsync(blockchain, index);
                if (header == null)
                    break;

                headers.Add(header);
            }

            if (headers.Count == 0)
                return;

            var response = HeadersPayload.Create(headers.ToArray());
            var message = SerializeMessage(MessageCommand.Headers, response);
            await SendAsync(message);
        }

        private async Task<bool> HandleHeadersAsync(HeadersPayload payload)
        {
            if (payload.Headers.Length == 0)
                return false;

            var entries = new List<HeaderCacheEntry>(payload.Headers.Length);
            foreach (var header in payload.Headers)
            {
                entries.Add(new HeaderCacheEntry
                {
                    Index = header.Index,
                    Hash = header.Hash.GetSpan().ToArray(),
                    PrevHash = header.PrevHash.GetSpan().ToArray(),
                    Timestamp = header.Timestamp,
                    Data = header.ToArray()
                });
            }

            var blockchain = _grainFactory.GetGrain<IBlockchainGrain>(0);
            await blockchain.AddHeadersAsync(entries);
            if (payload.Headers.Length > 0)
            {
                var taskManager = _grainFactory.GetGrain<ITaskManagerGrain>(0);
                await taskManager.NotifyHeadersAsync(this.GetPrimaryKeyString());
            }

            var lastIndex = payload.Headers[^1].Index;
            var updatedHeight = false;
            if (lastIndex > _state.State.RemoteHeight)
            {
                _state.State.RemoteHeight = lastIndex;
                updatedHeight = true;
            }

            return updatedHeight;
        }

        private async Task HandleGetBlocksAsync(GetBlocksPayload payload)
        {
            var blockchain = _grainFactory.GetGrain<IBlockchainGrain>(0);
            var startBlock = await blockchain.GetBlockByHashAsync(payload.HashStart.GetSpan().ToArray());
            if (startBlock == null)
                return;

            var maxCount = payload.Count < 0
                ? InvPayload.MaxHashesCount
                : Math.Min((int)payload.Count, InvPayload.MaxHashesCount);
            if (maxCount <= 0)
                return;

            var hashes = new List<UInt256>();
            for (var i = 0; i < maxCount; i++)
            {
                var index = startBlock.Index + 1 + (uint)i;
                var hash = await blockchain.GetBlockHashByIndexAsync(index);
                if (hash == null || hash.Length != UInt256.Length)
                    break;

                if (!await blockchain.ContainsBlockAsync(hash))
                    break;

                hashes.Add(new UInt256(hash));
            }

            if (hashes.Count == 0)
                return;

            foreach (var inv in InvPayload.CreateGroup(InventoryType.Block, hashes))
            {
                var message = SerializeMessage(MessageCommand.Inv, inv);
                await SendAsync(message);
            }
        }

        private async Task HandleGetBlocksByIndexAsync(GetBlockByIndexPayload payload)
        {
            var maxCount = payload.Count < 0
                ? InvPayload.MaxHashesCount
                : Math.Min((int)payload.Count, InvPayload.MaxHashesCount);
            if (maxCount <= 0)
                return;

            var blockchain = _grainFactory.GetGrain<IBlockchainGrain>(0);
            for (var i = 0; i < maxCount; i++)
            {
                var index = payload.IndexStart + (uint)i;
                var block = await blockchain.GetBlockByIndexAsync(index);
                if (block is not Block fullBlock)
                    break;

                if (_bloomFilter == null)
                {
                    var message = SerializeMessage(MessageCommand.Block, fullBlock);
                    await SendAsync(message);
                }
                else
                {
                    var flags = new BitArray(fullBlock.Transactions.Select(IsFilteredTransaction).ToArray());
                    var merkleBlock = MerkleBlockPayload.Create(fullBlock, flags);
                    var message = SerializeMessage(MessageCommand.MerkleBlock, merkleBlock);
                    await SendAsync(message);
                }
            }
        }

        private async Task HandleGetDataAsync(InvPayload payload)
        {
            if (payload.Hashes.Length == 0)
                return;

            var missing = new List<UInt256>();
            var sentHashes = _sentHashes;

            switch (payload.Type)
            {
                case InventoryType.Block:
                    {
                        var blockchain = _grainFactory.GetGrain<IBlockchainGrain>(0);
                        foreach (var hash in payload.Hashes)
                        {
                            if (sentHashes != null && !sentHashes.TryAdd(hash))
                                continue;

                            var block = await blockchain.GetBlockByHashAsync(hash.GetSpan().ToArray());
                            if (block is Block fullBlock)
                            {
                                byte[] message;
                                if (_bloomFilter == null)
                                {
                                    message = SerializeMessage(MessageCommand.Block, fullBlock);
                                }
                                else
                                {
                                    var flags = new BitArray(fullBlock.Transactions.Select(IsFilteredTransaction).ToArray());
                                    var merkleBlock = MerkleBlockPayload.Create(fullBlock, flags);
                                    message = SerializeMessage(MessageCommand.MerkleBlock, merkleBlock);
                                }
                                await SendAsync(message);
                            }
                            else
                            {
                                missing.Add(hash);
                            }
                        }
                        break;
                    }
                case InventoryType.TX:
                    {
                        var memoryPool = _grainFactory.GetGrain<IMemoryPoolGrain>(0);
                        foreach (var hash in payload.Hashes)
                        {
                            if (sentHashes != null && !sentHashes.TryAdd(hash))
                                continue;

                            var transaction = await memoryPool.GetTransactionAsync(hash.GetSpan().ToArray());
                            if (transaction is Transaction fullTransaction)
                            {
                                var message = SerializeMessage(MessageCommand.Transaction, fullTransaction);
                                await SendAsync(message);
                            }
                            else
                            {
                                missing.Add(hash);
                            }
                        }
                        break;
                    }
                case InventoryType.Extensible:
                    {
                        foreach (var hash in payload.Hashes)
                        {
                            if (sentHashes != null && !sentHashes.TryAdd(hash))
                                continue;

                            if (_system.RelayCache.TryGet(hash, out var inventory) &&
                                inventory is ExtensiblePayload extensible)
                            {
                                var message = SerializeMessage(MessageCommand.Extensible, extensible);
                                await SendAsync(message);
                            }
                            else
                            {
                                missing.Add(hash);
                            }
                        }
                        break;
                    }
                default:
                    foreach (var hash in payload.Hashes)
                    {
                        if (sentHashes != null && !sentHashes.TryAdd(hash))
                            continue;
                        missing.Add(hash);
                    }
                    break;
            }

            if (missing.Count == 0)
                return;

            foreach (var inv in InvPayload.CreateGroup(payload.Type, missing))
            {
                var message = SerializeMessage(MessageCommand.NotFound, inv);
                await SendAsync(message);
            }
        }

        private async Task HandleBlockAsync(Block block)
        {
            if (_knownHashes != null && !_knownHashes.TryAdd(block.Hash))
                return;

            _pendingKnownHashes.Remove(block.Hash);

            var taskManager = _grainFactory.GetGrain<ITaskManagerGrain>(0);
            await taskManager.CompleteBlockAsync(this.GetPrimaryKeyString(), block.Hash.GetSpan().ToArray(), block.Index);

            if (block.Index > _state.State.RemoteHeight)
            {
                _state.State.RemoteHeight = block.Index;
                await UpdateTaskManagerPeerHeightAsync(block.Index);
                var localNode = _grainFactory.GetGrain<ILocalNodeGrain>(0);
                await localNode.UpdatePeerHeightAsync(_state.State.Address, _state.State.Port, block.Index);
            }

            var currentHeight = NativeContract.Ledger.CurrentIndex(_system.StoreView);
            if (block.Index > currentHeight + InvPayload.MaxHashesCount)
                return;

            var blockchain = _grainFactory.GetGrain<IBlockchainGrain>(0);
            var result = await blockchain.PersistBlockAsync(block, _state.State.Address);
            if (result == BlockVerifyResult.Invalid)
            {
                await taskManager.NotifyInvalidBlockAsync(block.Hash.GetSpan().ToArray(), block.Index);
            }
        }

        private async Task HandleTransactionAsync(Transaction transaction)
        {
            if (_knownHashes != null && !_knownHashes.TryAdd(transaction.Hash))
                return;

            _pendingKnownHashes.Remove(transaction.Hash);

            var taskManager = _grainFactory.GetGrain<ITaskManagerGrain>(0);
            await taskManager.CompleteTaskAsync(this.GetPrimaryKeyString(), transaction.Hash.GetSpan().ToArray());

            if (_system.ContainsTransaction(transaction.Hash) != ContainsTransactionType.NotExist)
                return;

            if (_system.ContainsConflictHash(transaction.Hash, transaction.Signers.Select(s => s.Account)))
                return;

            var consensus = _grainFactory.GetGrain<IConsensusGrain>(0);
            await consensus.OnTransactionAsync(transaction);

            var memoryPool = _grainFactory.GetGrain<IMemoryPoolGrain>(0);
            var txRouter = _grainFactory.GetGrain<ITxRouterGrain>(0);
            var preverify = await txRouter.PreverifyAsync(transaction, relay: true);
            if (!preverify.IsValid)
            {
                return;
            }

            var addResult = await memoryPool.AddTransactionAsync(transaction);
            if (addResult == MemoryPoolAddResult.Succeed && preverify.ShouldRelay)
            {
                var localNode = _grainFactory.GetGrain<ILocalNodeGrain>(0);
                await localNode.RelayAsync(transaction.Hash.GetSpan().ToArray(), (byte)InventoryType.TX);
            }

        }

        private async Task HandleExtensibleAsync(ExtensiblePayload payload)
        {
            if (!payload.TryGetHash(out var hash))
                return;

            if (_knownHashes != null && !_knownHashes.TryAdd(hash))
                return;

            _pendingKnownHashes.Remove(hash);

            var taskManager = _grainFactory.GetGrain<ITaskManagerGrain>(0);
            await taskManager.CompleteTaskAsync(this.GetPrimaryKeyString(), hash.GetSpan().ToArray());

            var blockchain = _grainFactory.GetGrain<IBlockchainGrain>(0);
            var verifyResult = await blockchain.VerifyExtensiblePayloadAsync(payload);

            if (verifyResult == Neo.Ledger.VerifyResult.Succeed)
            {
                if (string.Equals(payload.Category, "dBFT", StringComparison.Ordinal))
                {
                    var consensus = _grainFactory.GetGrain<IConsensusGrain>(0);
                    await consensus.OnConsensusMessageAsync(payload.ToArray(), _state.State.Address);
                }

                var localNode = _grainFactory.GetGrain<ILocalNodeGrain>(0);
                await localNode.RelayAsync(hash.GetSpan().ToArray(), (byte)InventoryType.Extensible);
            }
        }

        private async Task HandleNotFoundAsync(InvPayload payload)
        {
            if (payload.Hashes.Length == 0)
                return;

            var taskManager = _grainFactory.GetGrain<ITaskManagerGrain>(0);
            var hashes = payload.Hashes.Select(h => h.GetSpan().ToArray());
            await taskManager.RestartTasksAsync(hashes, (byte)payload.Type);
        }

        private async Task HandleMempoolAsync()
        {
            var memoryPool = _grainFactory.GetGrain<IMemoryPoolGrain>(0);
            var items = await memoryPool.GetVerifiedTransactionsAsync(int.MaxValue);
            if (items.Count == 0)
                return;

            var hashes = items
                .Where(item => item.Hash.Length == UInt256.Length)
                .Select(item => new UInt256(item.Hash))
                .ToArray();
            if (hashes.Length == 0)
                return;

            foreach (var inv in InvPayload.CreateGroup(InventoryType.TX, hashes))
            {
                var message = SerializeMessage(MessageCommand.Inv, inv);
                await SendAsync(message);
            }
        }

        private void HandleFilterLoad(FilterLoadPayload payload)
        {
            _bloomFilter = new BloomFilter(payload.Filter.Length * 8, payload.K, payload.Tweak, payload.Filter);
        }

        private void HandleFilterAdd(FilterAddPayload payload)
        {
            _bloomFilter?.Add(payload.Data);
        }

        private void HandleFilterClear()
        {
            _bloomFilter = null;
        }

        private async Task HandleAddrAsync(AddrPayload payload)
        {
            if (!_state.State.GetAddrSent)
                return;

            _state.State.GetAddrSent = false;

            if (payload.AddressList.Length == 0)
                return;

            var addresses = new List<string>(payload.AddressList.Length);
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var addr in payload.AddressList)
            {
                var endPoint = addr.EndPoint;
                if (endPoint.Port <= 0)
                    continue;

                var address = endPoint.ToString();
                if (endPoint.Address.ToString() == _state.State.Address && endPoint.Port == _state.State.Port)
                    continue;

                if (seen.Add(address))
                    addresses.Add(address);
            }

            if (addresses.Count == 0)
                return;

            var localNode = _grainFactory.GetGrain<ILocalNodeGrain>(0);
            await localNode.AddPeersAsync(addresses);
        }

        private async Task SendAddrAsync()
        {
            var localNode = _grainFactory.GetGrain<ILocalNodeGrain>(0);
            var connectedPeers = await localNode.GetConnectedPeersAsync();

            var now = (uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var addresses = new List<NetworkAddressWithTime>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var peer in connectedPeers)
            {
                var advertisedPort = peer.ListenerPort;
                if (advertisedPort <= 0 || advertisedPort > ushort.MaxValue)
                    continue;

                if (TryCreateNetworkAddress(peer.Address, advertisedPort, now, peer.IsFullNode, peer.Height, out var address))
                {
                    if (IsSelfAddress(address))
                        continue;

                    if (seen.Add(address.EndPoint.ToString()))
                        addresses.Add(address);
                }
            }

            if (addresses.Count == 0)
                return;

            var payload = AddrPayload.Create(addresses
                .OrderBy(_ => RandomNumberGenerator.GetInt32(int.MaxValue))
                .Take(AddrPayload.MaxCountToSend)
                .ToArray());
            var message = SerializeMessage(MessageCommand.Addr, payload);
            await SendImmediateAsync(message);
        }

        private async Task SendLocalVersionAsync()
        {
            var localNode = _grainFactory.GetGrain<ILocalNodeGrain>(0);
            var summary = await localNode.GetStateSummaryAsync();
            var blockchain = _grainFactory.GetGrain<IBlockchainGrain>(0);
            var localHeight = await blockchain.GetHeightAsync();

            var handshakeUserAgent = string.IsNullOrWhiteSpace(summary.UserAgent)
                ? _options.UserAgent
                : summary.UserAgent;
            var capabilities = new List<NodeCapability>
            {
                new FullNodeCapability(localHeight)
            };
            if (!_options.EnableCompression)
            {
                capabilities.Add(new DisableCompressionCapability());
            }

            if (summary.ListenerPort > 0 && summary.ListenerPort <= ushort.MaxValue)
            {
                capabilities.Add(new ServerCapability(NodeCapabilityType.TcpServer, (ushort)summary.ListenerPort));
            }
            var wsPort = _options.WsPort;
            if (_options.WsEnabled && wsPort > 0 && wsPort <= ushort.MaxValue &&
                wsPort != summary.ListenerPort)
            {
#pragma warning disable CS0612 // Type or member is obsolete
                capabilities.Add(new ServerCapability(NodeCapabilityType.WsServer, (ushort)wsPort));
#pragma warning restore CS0612 // Type or member is obsolete
            }

            var version = VersionPayload.Create(_options.NetworkMagic, summary.Nonce, handshakeUserAgent, capabilities.ToArray());
            version.Version = _options.ProtocolVersion;
            _state.State.VersionSent = true;
            var versionMessage = SerializeMessage(MessageCommand.Version, version);
            await SendImmediateAsync(versionMessage);
        }

        private async Task SendPongAsync(uint nonce)
        {
            var blockchain = _grainFactory.GetGrain<IBlockchainGrain>(0);
            var height = await blockchain.GetHeightAsync();
            if (height > _lastHeightSent)
                _lastHeightSent = height;
            var payload = PingPayload.Create(height, nonce);
            var pongMessage = SerializeMessage(MessageCommand.Pong, payload);
            await SendImmediateAsync(pongMessage);
        }

        private Task SendVerackAsync()
        {
            var verackMessage = SerializeMessage(MessageCommand.Verack);
            return SendImmediateAsync(verackMessage);
        }

        private Task RegisterTaskManagerSessionAsync()
        {
            var taskManager = _grainFactory.GetGrain<ITaskManagerGrain>(0);
            return taskManager.RegisterSessionAsync(
                this.GetPrimaryKeyString(),
                _state.State.RemoteHeight,
                _state.State.UserAgent);
        }

        private Task UpdateTaskManagerPeerHeightAsync(uint height)
        {
            var taskManager = _grainFactory.GetGrain<ITaskManagerGrain>(0);
            return taskManager.UpdatePeerHeightAsync(this.GetPrimaryKeyString(), height);
        }

        private Task UnregisterTaskManagerSessionAsync()
        {
            var taskManager = _grainFactory.GetGrain<ITaskManagerGrain>(0);
            return taskManager.UnregisterSessionAsync(this.GetPrimaryKeyString());
        }

        private bool IsSelfAddress(NetworkAddressWithTime address)
        {
            return address.Address.ToString() == _state.State.Address &&
                address.EndPoint.Port == _state.State.Port;
        }

        private static bool TryCreateNetworkAddress(
            string address,
            int port,
            uint timestamp,
            bool isFullNode,
            uint height,
            out NetworkAddressWithTime networkAddress)
        {
            networkAddress = null!;

            if (port <= 0 || port > ushort.MaxValue)
                return false;

            if (!IPAddress.TryParse(address, out var ip))
                return false;

            var capabilities = new List<NodeCapability>
            {
                new ServerCapability(NodeCapabilityType.TcpServer, (ushort)port)
            };

            if (isFullNode)
            {
                capabilities.Add(new FullNodeCapability(height));
            }

            networkAddress = NetworkAddressWithTime.Create(ip, timestamp, capabilities.ToArray());
            return true;
        }

        private bool IsFilteredTransaction(Transaction transaction)
        {
            if (_bloomFilter == null)
                return false;

            if (_bloomFilter.Check(transaction.Hash.ToArray()))
                return true;

            return transaction.Signers.Any(s => _bloomFilter.Check(s.Account.ToArray()));
        }

        private async Task<Header?> TryGetHeaderAsync(IBlockchainGrain blockchain, uint index)
        {
            var entry = await blockchain.GetHeaderAsync(index);
            if (entry != null && entry.Data.Length > 0)
            {
                if (SerializationHelper.TryDeserializeHeader(entry.Data, out var parsed))
                    return parsed;
            }

            var block = await blockchain.GetBlockByIndexAsync(index);
            if (block is Block fullBlock)
                return fullBlock.Header;

            return null;
        }

        private byte[] SerializeMessage(MessageCommand command, ISerializable? payload = null)
        {
            var message = Message.Create(command, payload);
            if (_state.State.UseCompactFormat)
            {
                return message.ToArray(_state.State.EnableCompression);
            }
            else
            {
                return message.ToArrayStandardN3(_options.NetworkMagic, _state.State.EnableCompression);
            }
        }

        private byte[] AdjustCompressionIfNeeded(byte[] message)
        {
            if (_state.State.EnableCompression || message.Length < 3)
                return message;

            if ((message[0] & (byte)MessageFlags.Compressed) == 0)
                return message;

            if (TryDeserializeMessage(message, _options.NetworkMagic, out var parsed) && parsed != null)
                return Message.Create(parsed.Command, parsed.Payload).ToArray(false);

            return message;
        }

        private Task HandleProtocolViolationAsync(string reason)
        {
            return AddMisbehaviorAsync(ProtocolViolationScore, reason);
        }

        private async Task AddMisbehaviorAsync(int score, string reason)
        {
            if (_state.State.ConnectionState == (int)ConnectionState.Disconnected)
                return;

            _state.State.MisbehaviorScore = Math.Max(0, _state.State.MisbehaviorScore + score);
            Utility.Log(nameof(RemoteNodeGrain), LogLevel.Warning,
                $"Protocol violation from {_state.State.Address}:{_state.State.Port}: {reason} (score {_state.State.MisbehaviorScore})");

            await _state.WriteStateAsync();

            if (_state.State.MisbehaviorScore >= MisbehaviorDisconnectThreshold)
                await DisconnectAsync();
        }

        private static bool TryDeserializeMessage(byte[] data, uint networkMagic, out Message? message)
        {
            message = null;
            if (data.Length < 3)
                return false;

            if (data.Length >= 24)
            {
                var span = new ReadOnlySpan<byte>(data);
                var potentialMagic = BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(0, 4));
                if (potentialMagic == networkMagic)
                {
                    return Message.TryDeserializeStandardN3(span, networkMagic, out message);
                }
            }

            var flags = data[0];
            if (flags > (byte)MessageFlags.Compressed)
                return false;

            var command = (MessageCommand)data[1];
            if (!Enum.IsDefined(typeof(MessageCommand), command))
                return false;

            try
            {
                var reader = new MemoryReader(data);
                var parsed = new Message();
                ((ISerializable)parsed).Deserialize(ref reader);
                message = parsed;
                return true;
            }
            catch (FormatException)
            {
                return false;
            }
            catch
            {
                return false;
            }
        }

        #endregion
    }
}
#pragma warning restore CS0618
