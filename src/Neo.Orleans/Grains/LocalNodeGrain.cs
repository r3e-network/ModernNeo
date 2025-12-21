using Neo.Orleans.Interfaces;
using Neo.Orleans.States;
using Orleans.Runtime;

namespace Neo.Orleans.Grains;

/// <summary>
/// Orleans Grain implementation for local P2P node management.
/// Replaces Akka.NET LocalNode Actor with seed nodes and connection management.
/// </summary>
public class LocalNodeGrain : Grain, ILocalNodeGrain
{
    private readonly IPersistentState<LocalNodeState> _state;
    private readonly IGrainFactory _grainFactory;
    private const int MaxCountFromSeedList = 5;

    public LocalNodeGrain(
        [PersistentState("localnode", "LocalNodeStore")]
        IPersistentState<LocalNodeState> state,
        IGrainFactory grainFactory)
    {
        _state = state;
        _grainFactory = grainFactory;
    }

    public async Task InitializeAsync(LocalNodeConfig config)
    {
        _state.State.Nonce = config.Nonce;
        _state.State.UserAgent = config.UserAgent;
        _state.State.SeedList = config.SeedList.ToList();
        _state.State.MaxConnections = config.MaxConnections;
        _state.State.ListenerPort = config.ListenerPort;
        _state.State.NetworkMagic = config.NetworkMagic;
        _state.State.ProtocolVersion = config.ProtocolVersion;

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
            if (!string.IsNullOrEmpty(seed))
                _state.State.UnconnectedPeers.Add(seed);
        }

        await _state.WriteStateAsync();
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
        await _state.WriteStateAsync();
    }

    public async Task RegisterPeerAsync(string address, int port, uint height)
    {
        var key = $"{address}:{port}";
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        if (_state.State.ConnectedPeers.Count >= _state.State.MaxConnections &&
            !_state.State.ConnectedPeers.ContainsKey(key))
        {
            return; // At capacity, reject new connection
        }

        _state.State.ConnectedPeers[key] = new ConnectedPeerState
        {
            Address = address,
            Port = port,
            Height = height,
            ConnectedAt = now,
            LastSeen = now
        };

        // Remove from unconnected pool
        _state.State.UnconnectedPeers.Remove(key);

        await _state.WriteStateAsync();
    }

    public async Task RegisterPeerAsync(PeerConnectionInfo info)
    {
        var key = $"{info.Address}:{info.Port}";
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        if (_state.State.ConnectedPeers.Count >= _state.State.MaxConnections &&
            !_state.State.ConnectedPeers.ContainsKey(key))
        {
            return;
        }

        _state.State.ConnectedPeers[key] = new ConnectedPeerState
        {
            Address = info.Address,
            Port = info.Port,
            Height = info.Height,
            ConnectedAt = now,
            LastSeen = now,
            Nonce = info.Nonce,
            UserAgent = info.UserAgent,
            IsFullNode = info.IsFullNode,
            ListenerPort = info.ListenerPort
        };

        _state.State.UnconnectedPeers.Remove(key);

        await _state.WriteStateAsync();
    }

    public async Task UnregisterPeerAsync(string address, int port)
    {
        var key = $"{address}:{port}";
        if (_state.State.ConnectedPeers.Remove(key))
        {
            await _state.WriteStateAsync();
        }
    }

    public async Task UpdatePeerHeightAsync(string address, int port, uint height)
    {
        var key = $"{address}:{port}";
        if (_state.State.ConnectedPeers.TryGetValue(key, out var peer))
        {
            peer.Height = height;
            peer.LastSeen = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            await _state.WriteStateAsync();
        }
    }

    public Task<bool> AllowNewConnectionAsync(uint nonce, uint networkMagic, string address)
    {
        // Check network magic
        if (networkMagic != _state.State.NetworkMagic)
            return Task.FromResult(false);

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
                var message = new byte[1 + inventoryHash.Length];
                message[0] = inventoryType;
                Array.Copy(inventoryHash, 0, message, 1, inventoryHash.Length);
                return remoteGrain.SendAsync(message);
            });

        await Task.WhenAll(relayTasks);
    }

    public async Task RelayBlockAsync(byte[] blockHash, uint blockIndex)
    {
        var hashKey = Convert.ToBase64String(blockHash);

        if (_state.State.RecentRelays.Contains(hashKey))
            return;

        _state.State.RecentRelays.Add(hashKey);
        await _state.WriteStateAsync();

        // Only relay to peers that are behind
        var relayTasks = _state.State.ConnectedPeers
            .Where(kvp => kvp.Value.Height < blockIndex)
            .Select(kvp =>
            {
                var remoteGrain = _grainFactory.GetGrain<IRemoteNodeGrain>(kvp.Key);
                var message = new byte[1 + blockHash.Length];
                message[0] = 0x02; // Block inventory type
                Array.Copy(blockHash, 0, message, 1, blockHash.Length);
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
            .Select(p => new PeerInfo(p.Address, p.Port, p.Height, p.Nonce, p.UserAgent, p.IsFullNode))
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

            // Don't add if already connected
            if (_state.State.ConnectedPeers.ContainsKey(address))
                continue;

            // Don't exceed max unconnected
            if (_state.State.UnconnectedPeers.Count >= _state.State.MaxUnconnectedPeers)
                break;

            if (_state.State.UnconnectedPeers.Add(address))
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
            var getAddrMessage = new byte[] { 0x10 }; // GetAddr command placeholder
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
}
