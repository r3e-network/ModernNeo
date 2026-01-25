// Copyright (C) 2015-2025 The Neo Project.
//
// LocalNodeGrain.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo;
using Neo.Network.P2P;
using Neo.Network.P2P.Payloads;
using Neo.Orleans.Interfaces;
using Neo.Orleans.States;
using Orleans.Runtime;
using System.Net;

namespace Neo.Orleans.Grains
{
    /// <summary>
    /// Orleans Grain implementation for local P2P node management.
    /// Handles seed nodes and connection management.
    /// </summary>
    public class LocalNodeGrain : Grain, ILocalNodeGrain
    {
        private readonly IPersistentState<LocalNodeState> _state;
        private readonly IGrainFactory _grainFactory;
        private readonly Dictionary<string, long> _pendingConnections = new(StringComparer.OrdinalIgnoreCase);
        private IGrainTimer? _connectionMaintainer;
        private const int MaxCountFromSeedList = 5;
        private static readonly TimeSpan ConnectionMaintenanceInterval = TimeSpan.FromSeconds(5);
        private static readonly TimeSpan PendingConnectionTimeout = TimeSpan.FromSeconds(30);

        public LocalNodeGrain(
            [PersistentState("localnode", "LocalNodeStore")]
            IPersistentState<LocalNodeState> state,
            IGrainFactory grainFactory)
        {
            _state = state;
            _grainFactory = grainFactory;
        }

        public override async Task OnActivateAsync(CancellationToken cancellationToken)
        {
            _connectionMaintainer ??= this.RegisterGrainTimer(
                _ => MaintainConnectionsAsync(),
                new GrainTimerCreationOptions
                {
                    DueTime = ConnectionMaintenanceInterval,
                    Period = ConnectionMaintenanceInterval,
                    Interleave = true
                });

            var normalizedConnected = new Dictionary<string, ConnectedPeerState>(StringComparer.OrdinalIgnoreCase);
            var normalizedUnconnected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var changed = false;

            foreach (var kvp in _state.State.ConnectedPeers)
            {
                var peer = kvp.Value;
                if (peer == null || string.IsNullOrWhiteSpace(peer.Address) || peer.Port <= 0)
                {
                    changed = true;
                    continue;
                }

                var normalizedAddress = NormalizeAddress(peer.Address);
                if (string.IsNullOrEmpty(normalizedAddress))
                {
                    changed = true;
                    continue;
                }

                if (!string.Equals(peer.Address, normalizedAddress, StringComparison.Ordinal))
                {
                    peer.Address = normalizedAddress;
                    changed = true;
                }

                var normalizedKey = FormatPeerKey(peer.Address, peer.Port);
                if (string.IsNullOrEmpty(normalizedKey))
                {
                    changed = true;
                    continue;
                }

                if (!StringComparer.OrdinalIgnoreCase.Equals(kvp.Key, normalizedKey))
                    changed = true;

                if (normalizedConnected.TryGetValue(normalizedKey, out var existing))
                {
                    if (peer.LastSeen > existing.LastSeen)
                        normalizedConnected[normalizedKey] = peer;
                    changed = true;
                    continue;
                }

                normalizedConnected[normalizedKey] = peer;
            }

            foreach (var entry in _state.State.UnconnectedPeers)
            {
                var normalized = NormalizePeerAddress(entry);
                if (string.IsNullOrEmpty(normalized))
                {
                    changed = true;
                    continue;
                }

                if (!normalizedUnconnected.Add(normalized))
                    changed = true;
            }

            if (normalizedConnected.Count > 0)
            {
                foreach (var key in normalizedConnected.Keys)
                {
                    if (normalizedUnconnected.Remove(key))
                        changed = true;
                }
            }

            if (changed)
            {
                _state.State.ConnectedPeers = normalizedConnected;
                _state.State.UnconnectedPeers = normalizedUnconnected;
                await _state.WriteStateAsync();
            }

            await base.OnActivateAsync(cancellationToken);
        }

        public override Task OnDeactivateAsync(DeactivationReason reason, CancellationToken cancellationToken)
        {
            _connectionMaintainer?.Dispose();
            _connectionMaintainer = null;
            _pendingConnections.Clear();
            return base.OnDeactivateAsync(reason, cancellationToken);
        }

        public async Task InitializeAsync(LocalNodeConfig config)
        {
            _state.State.Nonce = config.Nonce;
            _state.State.UserAgent = config.UserAgent;
            _state.State.SeedList = config.SeedList.ToList();
            _state.State.MaxConnections = config.MaxConnections;
            _state.State.MaxConnectionsPerAddress = config.MaxConnectionsPerAddress;
            _state.State.MinDesiredConnections = config.MinDesiredConnections;
            _state.State.ListenerPort = config.ListenerPort;
            _state.State.NetworkMagic = config.NetworkMagic;
            _state.State.ProtocolVersion = config.ProtocolVersion;
            _state.State.EnableCompression = config.EnableCompression;

            await _state.WriteStateAsync();
        }

        public async Task StartAsync()
        {
            if (_state.State.IsStarted)
                return;

            _state.State.IsStarted = true;

            // Add seed nodes to unconnected pool
            foreach (var seed in _state.State.SeedList)
            {
                if (string.IsNullOrWhiteSpace(seed))
                    continue;

                var normalized = NormalizePeerAddress(seed);
                if (!string.IsNullOrEmpty(normalized))
                    _state.State.UnconnectedPeers.Add(normalized);
            }

            await _state.WriteStateAsync();
            await MaintainConnectionsAsync();
        }

        public async Task StopAsync()
        {
            _state.State.IsStarted = false;

            // Disconnect all peers
            foreach (var key in _state.State.ConnectedPeers.Keys.ToList())
            {
                var remoteGrain = _grainFactory.GetGrain<IRemoteNodeGrain>(key);
                await remoteGrain.DisconnectAsync();
            }

            _state.State.ConnectedPeers.Clear();
            _pendingConnections.Clear();
            await _state.WriteStateAsync();
        }

        public async Task RegisterPeerAsync(string address, int port, uint height)
        {
            if (string.IsNullOrWhiteSpace(address) || port <= 0 || port > ushort.MaxValue)
                return;

            var normalizedAddress = NormalizeAddress(address);
            if (string.IsNullOrEmpty(normalizedAddress))
                return;

            var key = FormatPeerKey(normalizedAddress, port);
            if (string.IsNullOrEmpty(key))
                return;
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            if (_state.State.ConnectedPeers.Count >= _state.State.MaxConnections &&
                !_state.State.ConnectedPeers.ContainsKey(key))
            {
                return; // At capacity, reject new connection
            }

            if (_state.State.MaxConnectionsPerAddress > 0 &&
                CountConnectionsForAddress(normalizedAddress) >= _state.State.MaxConnectionsPerAddress &&
                !_state.State.ConnectedPeers.ContainsKey(key))
            {
                return;
            }

            _state.State.ConnectedPeers[key] = new ConnectedPeerState
            {
                Address = normalizedAddress,
                Port = port,
                Height = height,
                ConnectedAt = now,
                LastSeen = now,
                ListenerPort = port
            };

            ClearPending(key);
            // Remove from unconnected pool
            _state.State.UnconnectedPeers.Remove(key);
            var normalizedKey = NormalizePeerAddress(key);
            if (!string.IsNullOrEmpty(normalizedKey))
                _state.State.UnconnectedPeers.Remove(normalizedKey);

            await _state.WriteStateAsync();
        }

        public async Task RegisterPeerAsync(PeerConnectionInfo info)
        {
            if (string.IsNullOrWhiteSpace(info.Address) || info.Port <= 0 || info.Port > ushort.MaxValue)
                return;

            var normalizedAddress = NormalizeAddress(info.Address);
            if (string.IsNullOrEmpty(normalizedAddress))
                return;

            var key = FormatPeerKey(normalizedAddress, info.Port);
            if (string.IsNullOrEmpty(key))
                return;
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            if (_state.State.ConnectedPeers.Count >= _state.State.MaxConnections &&
                !_state.State.ConnectedPeers.ContainsKey(key))
            {
                return;
            }

            if (_state.State.MaxConnectionsPerAddress > 0 &&
                CountConnectionsForAddress(normalizedAddress) >= _state.State.MaxConnectionsPerAddress &&
                !_state.State.ConnectedPeers.ContainsKey(key))
            {
                return;
            }

            _state.State.ConnectedPeers[key] = new ConnectedPeerState
            {
                Address = normalizedAddress,
                Port = info.Port,
                Height = info.Height,
                ConnectedAt = now,
                LastSeen = now,
                Nonce = info.Nonce,
                UserAgent = info.UserAgent,
                IsFullNode = info.IsFullNode,
                ListenerPort = info.ListenerPort
            };

            ClearPending(key);
            _state.State.UnconnectedPeers.Remove(key);
            var normalizedKey = NormalizePeerAddress(key);
            if (!string.IsNullOrEmpty(normalizedKey))
                _state.State.UnconnectedPeers.Remove(normalizedKey);

            await _state.WriteStateAsync();
        }

        public async Task UnregisterPeerAsync(string address, int port)
        {
            if (string.IsNullOrWhiteSpace(address) || port <= 0 || port > ushort.MaxValue)
                return;

            var key = FormatPeerKey(address, port);
            if (string.IsNullOrEmpty(key))
                return;
            var removed = _state.State.ConnectedPeers.Remove(key);
            if (!removed)
            {
                var normalizedKey = NormalizePeerAddress(key);
                if (!string.IsNullOrEmpty(normalizedKey))
                    removed = _state.State.ConnectedPeers.Remove(normalizedKey);
            }

            if (removed)
            {
                ClearPending(key);
                await _state.WriteStateAsync();
            }
        }

        public async Task UpdatePeerHeightAsync(string address, int port, uint height)
        {
            if (string.IsNullOrWhiteSpace(address) || port <= 0 || port > ushort.MaxValue)
                return;

            var key = FormatPeerKey(address, port);
            if (string.IsNullOrEmpty(key))
                return;
            if (!_state.State.ConnectedPeers.TryGetValue(key, out var peer))
            {
                var normalizedKey = NormalizePeerAddress(key);
                if (!string.IsNullOrEmpty(normalizedKey))
                    _state.State.ConnectedPeers.TryGetValue(normalizedKey, out peer);
            }

            if (peer != null)
            {
                peer.Height = height;
                peer.LastSeen = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                await _state.WriteStateAsync();
            }
        }

        public Task<bool> AllowNewConnectionAsync(uint nonce, uint networkMagic, string address)
        {
            if (!_state.State.IsStarted)
                return Task.FromResult(false);

            // Check network magic
            if (networkMagic != _state.State.NetworkMagic)
                return Task.FromResult(false);

            if (_state.State.ConnectedPeers.Count >= _state.State.MaxConnections)
                return Task.FromResult(false);

            if (_state.State.MaxConnectionsPerAddress > 0 &&
                CountConnectionsForAddress(address) >= _state.State.MaxConnectionsPerAddress)
            {
                return Task.FromResult(false);
            }

            // Check if connecting to self
            if (nonce == _state.State.Nonce)
                return Task.FromResult(false);

            // Check for duplicate nonce (same node connecting twice)
            foreach (var peer in _state.State.ConnectedPeers.Values)
            {
                if (peer.Nonce == nonce)
                    return Task.FromResult(false);
            }

            return Task.FromResult(true);
        }

        public async Task RelayAsync(byte[] inventoryHash, byte inventoryType)
        {
            var hashKey = Convert.ToBase64String(inventoryHash);

            // Check if already relayed recently
            if (_state.State.RecentRelays.Contains(hashKey))
                return;

            var message = CreateInvMessage(inventoryHash, inventoryType, _state.State.EnableCompression);
            if (message.Length == 0)
                return;

            // Add to recent relays
            _state.State.RecentRelays.Add(hashKey);

            // Prune if over limit
            if (_state.State.RecentRelays.Count > _state.State.MaxRecentRelays)
            {
                var toKeep = _state.State.RecentRelays
                    .Skip(_state.State.RecentRelays.Count - _state.State.MaxRecentRelays / 2)
                    .ToHashSet();
                _state.State.RecentRelays = toKeep;
            }

            await _state.WriteStateAsync();

            // Relay to all connected peers
            var relayTasks = _state.State.ConnectedPeers.Keys
                .Select(key =>
                {
                    var remoteGrain = _grainFactory.GetGrain<IRemoteNodeGrain>(key);
                    return remoteGrain.SendAsync(message);
                });

            await Task.WhenAll(relayTasks);
        }

        public async Task RelayBlockAsync(byte[] blockHash, uint blockIndex)
        {
            var hashKey = Convert.ToBase64String(blockHash);

            if (_state.State.RecentRelays.Contains(hashKey))
                return;

            var message = CreateInvMessage(blockHash, (byte)InventoryType.Block, _state.State.EnableCompression);
            if (message.Length == 0)
                return;

            _state.State.RecentRelays.Add(hashKey);

            if (_state.State.RecentRelays.Count > _state.State.MaxRecentRelays)
            {
                var toKeep = _state.State.RecentRelays
                    .Skip(_state.State.RecentRelays.Count - _state.State.MaxRecentRelays / 2)
                    .ToHashSet();
                _state.State.RecentRelays = toKeep;
            }

            await _state.WriteStateAsync();

            // Only relay to peers that are behind
            var relayTasks = _state.State.ConnectedPeers
                .Where(kvp => kvp.Value.Height < blockIndex)
                .Select(kvp =>
                {
                    var remoteGrain = _grainFactory.GetGrain<IRemoteNodeGrain>(kvp.Key);
                    return remoteGrain.SendAsync(message);
                });

            await Task.WhenAll(relayTasks);
        }

        public Task<int> GetConnectedPeerCountAsync() =>
            Task.FromResult(_state.State.ConnectedPeers.Count);

        public Task<int> GetUnconnectedPeerCountAsync() =>
            Task.FromResult(_state.State.UnconnectedPeers.Count);

        public Task<IEnumerable<PeerInfo>> GetConnectedPeersAsync()
        {
            var peers = _state.State.ConnectedPeers.Values
                .Select(p => new PeerInfo(p.Address, p.Port, p.Height, p.Nonce, p.UserAgent, p.IsFullNode, p.ListenerPort))
                .ToList();
            return Task.FromResult<IEnumerable<PeerInfo>>(peers);
        }

        public Task<IEnumerable<string>> GetUnconnectedPeersAsync()
        {
            return Task.FromResult<IEnumerable<string>>(_state.State.UnconnectedPeers.ToList());
        }

        public async Task AddPeersAsync(IEnumerable<string> addresses)
        {
            var added = false;
            foreach (var address in addresses)
            {
                if (string.IsNullOrEmpty(address))
                    continue;

                var normalized = NormalizePeerAddress(address);

                // Don't add if already connected
                if (string.IsNullOrEmpty(normalized))
                    continue;

                if (_state.State.ConnectedPeers.ContainsKey(normalized))
                    continue;

                // Don't exceed max unconnected
                if (_state.State.UnconnectedPeers.Count >= _state.State.MaxUnconnectedPeers)
                    break;

                if (_state.State.UnconnectedPeers.Add(normalized))
                    added = true;
            }

            if (added)
                await _state.WriteStateAsync();
        }

        public async Task BroadcastAsync(byte[] message)
        {
            var broadcastTasks = _state.State.ConnectedPeers.Keys
                .Select(key =>
                {
                    var remoteGrain = _grainFactory.GetGrain<IRemoteNodeGrain>(key);
                    return remoteGrain.SendAsync(message);
                });

            await Task.WhenAll(broadcastTasks);
        }

        public async Task RequestMorePeersAsync(int count)
        {
            count = Math.Max(count, MaxCountFromSeedList);

            if (_state.State.ConnectedPeers.Count > 0)
            {
                // Request addresses from connected peers (GetAddr message)
                var getAddrMessage = Message.Create(MessageCommand.GetAddr).ToArray(_state.State.EnableCompression);
                await BroadcastAsync(getAddrMessage);
            }
            else
            {
                // Use seed list when no connections
                var seedsToAdd = _state.State.SeedList
                    .Where(s => !string.IsNullOrEmpty(s))
                    .OrderBy(_ => Random.Shared.Next())
                    .Take(count);

                await AddPeersAsync(seedsToAdd);
            }
        }

        public Task<uint> GetNonceAsync() =>
            Task.FromResult(_state.State.Nonce);

        public Task<string> GetUserAgentAsync() =>
            Task.FromResult(_state.State.UserAgent);

        public Task<LocalNodeStateSummary> GetStateSummaryAsync()
        {
            return Task.FromResult(new LocalNodeStateSummary(
                _state.State.ConnectedPeers.Count,
                _state.State.UnconnectedPeers.Count,
                _state.State.Nonce,
                _state.State.UserAgent,
                _state.State.ListenerPort,
                _state.State.IsStarted));
        }

        private async Task MaintainConnectionsAsync()
        {
            if (!_state.State.IsStarted)
                return;

            var maxConnections = _state.State.MaxConnections;
            if (maxConnections <= 0)
                return;

            var desiredConnections = _state.State.MinDesiredConnections > 0
                ? _state.State.MinDesiredConnections
                : maxConnections;
            desiredConnections = Math.Min(desiredConnections, maxConnections);

            if (_state.State.ConnectedPeers.Count >= desiredConnections)
                return;

            var needed = desiredConnections - _state.State.ConnectedPeers.Count;
            if (needed <= 0)
                return;

            if (_state.State.UnconnectedPeers.Count == 0)
            {
                await RequestMorePeersAsync(needed);
                return;
            }

            var blockchain = _grainFactory.GetGrain<IBlockchainGrain>(0);
            var localHeight = await blockchain.GetHeightAsync();
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var candidates = _state.State.UnconnectedPeers
                .OrderBy(_ => Random.Shared.Next())
                .ToList();
            var stateChanged = false;

            foreach (var candidate in candidates)
            {
                if (needed <= 0)
                    break;

                if (!TryParsePeerEndpoint(candidate, out var address, out var port))
                {
                    _state.State.UnconnectedPeers.Remove(candidate);
                    stateChanged = true;
                    continue;
                }

                var key = FormatPeerKey(address, port);
                if (string.IsNullOrEmpty(key))
                {
                    _state.State.UnconnectedPeers.Remove(candidate);
                    stateChanged = true;
                    continue;
                }
                if (_state.State.ConnectedPeers.ContainsKey(key))
                {
                    _state.State.UnconnectedPeers.Remove(candidate);
                    stateChanged = true;
                    continue;
                }

                if (IsPendingConnection(key, now))
                    continue;

                if (_state.State.MaxConnectionsPerAddress > 0 &&
                    CountConnectionsForAddress(address) >= _state.State.MaxConnectionsPerAddress)
                {
                    continue;
                }

                _pendingConnections[key] = now;
                needed--;

                var remoteGrain = _grainFactory.GetGrain<IRemoteNodeGrain>(key);
                _ = remoteGrain.StartHandshakeAsync(localHeight, _state.State.Nonce, _state.State.UserAgent);
            }

            if (stateChanged)
                await _state.WriteStateAsync();
        }

        private static byte[] CreateInvMessage(byte[] inventoryHash, byte inventoryType, bool enableCompression)
        {
            if (inventoryHash.Length != UInt256.Length)
                return Array.Empty<byte>();

            var typedInventory = (InventoryType)inventoryType;
            if (!Enum.IsDefined(typeof(InventoryType), typedInventory))
                return Array.Empty<byte>();

            var payload = InvPayload.Create(typedInventory, new UInt256(inventoryHash));
            return Message.Create(MessageCommand.Inv, payload).ToArray(enableCompression);
        }

        private static string FormatPeerKey(string address, int port)
        {
            if (port <= 0 || port > ushort.MaxValue)
                return string.Empty;

            address = NormalizeAddress(address);
            if (string.IsNullOrEmpty(address))
                return string.Empty;

            if (IPAddress.TryParse(address, out var ip))
            {
                return new IPEndPoint(ip, port).ToString();
            }

            return $"{address}:{port}";
        }

        private int CountConnectionsForAddress(string address)
        {
            var normalized = NormalizeAddress(address);
            var count = 0;
            foreach (var peer in _state.State.ConnectedPeers.Values)
            {
                if (NormalizeAddress(peer.Address) == normalized)
                    count++;
            }

            return count;
        }

        private static string NormalizeAddress(string address)
        {
            if (string.IsNullOrWhiteSpace(address))
                return string.Empty;

            address = address.Trim();
            if (address.Length > 1 && address[0] == '[' && address[^1] == ']')
            {
                var inner = address.Substring(1, address.Length - 2);
                if (IPAddress.TryParse(inner, out var bracketed))
                    return bracketed.ToString();
            }

            if (IPAddress.TryParse(address, out var ip))
                return ip.ToString();

            return address.ToLowerInvariant();
        }

        private static string NormalizePeerAddress(string address)
        {
            if (string.IsNullOrWhiteSpace(address))
                return string.Empty;

            address = address.Trim();
            if (TryParsePeerEndpoint(address, out var parsedAddress, out var port))
                return FormatPeerKey(parsedAddress, port);

            return string.Empty;
        }

        private static bool TryParsePeerEndpoint(string value, out string address, out int port)
        {
            address = string.Empty;
            port = 0;

            if (string.IsNullOrWhiteSpace(value))
                return false;

            value = value.Trim();

            if (IPEndPoint.TryParse(value, out var endPoint))
            {
                address = endPoint.Address.ToString();
                port = endPoint.Port;
                return port > 0;
            }

            if (Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Port > 0)
            {
                address = uri.Host;
                port = uri.Port;
                return port > 0;
            }

            if (Uri.TryCreate($"tcp://{value}", UriKind.Absolute, out var tcpUri) && tcpUri.Port > 0)
            {
                address = tcpUri.Host;
                port = tcpUri.Port;
                return port > 0;
            }

            return false;
        }

        private bool IsPendingConnection(string key, long now)
        {
            if (_pendingConnections.TryGetValue(key, out var startedAt))
            {
                if (now - startedAt <= PendingConnectionTimeout.TotalMilliseconds)
                    return true;

                _pendingConnections.Remove(key);
            }

            return false;
        }

        private void ClearPending(string key)
        {
            if (string.IsNullOrEmpty(key))
                return;

            _pendingConnections.Remove(key);
            var normalizedKey = NormalizePeerAddress(key);
            if (!string.IsNullOrEmpty(normalizedKey))
                _pendingConnections.Remove(normalizedKey);
        }
    }
}
