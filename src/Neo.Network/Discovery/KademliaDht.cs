// Copyright (C) 2015-2025 The Neo Project.
//
// KademliaDht.cs file belongs to the neo project and is free
// software distributed under the MIT software license.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Numerics;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace Neo.Network.Discovery;

/// <summary>
/// Kademlia DHT implementation for distributed peer discovery.
/// Based on libp2p Kademlia DHT specification.
/// </summary>
/// <remarks>
/// Key features:
/// - XOR-based distance metric for peer routing
/// - k-bucket routing table for efficient peer lookup
/// - Iterative node lookup with parallel queries
/// - Provider records for content/service discovery
/// </remarks>
public sealed class KademliaDht : IPeerDiscovery
{
    private const int KBucketSize = 20; // k parameter
    private const int Alpha = 3; // Parallelism factor
    private const int KeySize = 256; // SHA-256 key size in bits

    private readonly KademliaDhtOptions _options;
    private readonly byte[] _localId;
    private readonly KBucket[] _routingTable;
    private readonly ConcurrentDictionary<string, PeerInfo> _knownPeers = new();
    private readonly ConcurrentDictionary<string, ProviderRecord> _providers = new();
    private CancellationTokenSource? _cts;
    private Task? _maintenanceTask;

    public string Name => "Kademlia DHT";
    public bool IsRunning => _cts != null && !_cts.IsCancellationRequested;

    public event Action<PeerInfo>? OnPeerDiscovered;
    public event Action<PeerInfo>? OnPeerLost;

    /// <summary>
    /// Gets the local node ID.
    /// </summary>
    public byte[] LocalId => _localId;

    /// <summary>
    /// Gets the number of peers in the routing table.
    /// </summary>
    public int RoutingTableSize => _routingTable.Sum(b => b.Count);

    public KademliaDht(KademliaDhtOptions? options = null)
    {
        _options = options ?? new KademliaDhtOptions();
        _localId = _options.NodeId ?? GenerateNodeId();
        _routingTable = new KBucket[KeySize];

        for (int i = 0; i < KeySize; i++)
        {
            _routingTable[i] = new KBucket(KBucketSize);
        }
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (IsRunning)
            return;

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        // Bootstrap from seed nodes
        await BootstrapAsync(cancellationToken);

        // Start maintenance tasks
        _maintenanceTask = MaintenanceLoopAsync(_cts.Token);
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (_cts == null)
            return;

        _cts.Cancel();

        try
        {
            if (_maintenanceTask != null)
                await _maintenanceTask.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
        }
        catch (OperationCanceledException) { }
        catch (TimeoutException) { }

        _cts.Dispose();
        _cts = null;
    }

    public async Task<IEnumerable<PeerInfo>> FindPeersAsync(int count, CancellationToken cancellationToken = default)
    {
        // Find peers closest to a random target
        var randomTarget = GenerateNodeId();
        return await FindNodeAsync(randomTarget, count, cancellationToken);
    }

    public Task AnnounceAsync(CancellationToken cancellationToken = default)
    {
        // In DHT, we announce by responding to FIND_NODE queries
        // and by periodically refreshing our presence in the routing tables of other nodes
        return RefreshBucketsAsync(cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync(CancellationToken.None);
    }

    /// <summary>
    /// Finds the k closest nodes to the given target ID.
    /// Implements iterative node lookup as per Kademlia specification.
    /// </summary>
    public async Task<IEnumerable<PeerInfo>> FindNodeAsync(byte[] targetId, int count, CancellationToken cancellationToken = default)
    {
        var closest = GetClosestPeers(targetId, Alpha);
        var queried = new HashSet<string>();
        var results = new SortedDictionary<byte[], PeerInfo>(new XorDistanceComparer(targetId));

        // Add initial closest peers to results
        foreach (var peer in closest)
        {
            var key = Convert.ToHexString(peer.PeerId);
            results[peer.PeerId] = peer;
        }

        // Iterative lookup
        while (true)
        {
            var toQuery = results.Values
                .Where(p => !queried.Contains(Convert.ToHexString(p.PeerId)))
                .Take(Alpha)
                .ToList();

            if (toQuery.Count == 0)
                break;

            var tasks = toQuery.Select(async peer =>
            {
                queried.Add(Convert.ToHexString(peer.PeerId));
                return await QueryFindNodeAsync(peer, targetId, cancellationToken);
            });

            var responses = await Task.WhenAll(tasks);

            bool improved = false;
            foreach (var peers in responses.Where(r => r != null))
            {
                foreach (var peer in peers!)
                {
                    var key = Convert.ToHexString(peer.PeerId);
                    if (!results.ContainsKey(peer.PeerId))
                    {
                        results[peer.PeerId] = peer;
                        improved = true;
                        AddPeer(peer);
                    }
                }
            }

            if (!improved)
                break;
        }

        return results.Values.Take(count);
    }

    /// <summary>
    /// Adds a provider record for content discovery.
    /// </summary>
    public void AddProvider(byte[] key, PeerInfo provider)
    {
        var keyHex = Convert.ToHexString(key);
        _providers[keyHex] = new ProviderRecord
        {
            Key = key,
            Provider = provider,
            Timestamp = DateTimeOffset.UtcNow
        };
    }

    /// <summary>
    /// Finds providers for the given key.
    /// </summary>
    public async Task<IEnumerable<PeerInfo>> FindProvidersAsync(byte[] key, int count, CancellationToken cancellationToken = default)
    {
        var providers = new List<PeerInfo>();

        // Check local providers
        var keyHex = Convert.ToHexString(key);
        if (_providers.TryGetValue(keyHex, out var local))
        {
            providers.Add(local.Provider);
        }

        // Query closest nodes for providers
        var closestNodes = await FindNodeAsync(key, KBucketSize, cancellationToken);
        foreach (var node in closestNodes)
        {
            var nodeProviders = await QueryGetProvidersAsync(node, key, cancellationToken);
            if (nodeProviders != null)
            {
                providers.AddRange(nodeProviders);
            }

            if (providers.Count >= count)
                break;
        }

        return providers.Take(count);
    }

    /// <summary>
    /// Adds a peer to the routing table.
    /// </summary>
    public void AddPeer(PeerInfo peer)
    {
        if (peer.PeerId.SequenceEqual(_localId))
            return;

        var bucketIndex = GetBucketIndex(peer.PeerId);
        var bucket = _routingTable[bucketIndex];

        if (bucket.TryAdd(peer))
        {
            var key = Convert.ToHexString(peer.PeerId);
            var isNew = !_knownPeers.ContainsKey(key);
            _knownPeers[key] = peer;

            if (isNew)
            {
                OnPeerDiscovered?.Invoke(peer);
            }
        }
    }

    /// <summary>
    /// Removes a peer from the routing table.
    /// </summary>
    public void RemovePeer(byte[] peerId)
    {
        var bucketIndex = GetBucketIndex(peerId);
        var bucket = _routingTable[bucketIndex];

        if (bucket.TryRemove(peerId, out var peer) && peer != null)
        {
            var key = Convert.ToHexString(peerId);
            _knownPeers.TryRemove(key, out _);
            OnPeerLost?.Invoke(peer);
        }
    }

    private async Task BootstrapAsync(CancellationToken cancellationToken)
    {
        foreach (var seed in _options.BootstrapNodes)
        {
            try
            {
                var seedPeer = new PeerInfo
                {
                    PeerId = GenerateNodeId(), // Will be updated after connection
                    Addresses = [seed],
                    DiscoverySource = "bootstrap"
                };

                AddPeer(seedPeer);

                // Query seed for nodes close to us
                var closePeers = await QueryFindNodeAsync(seedPeer, _localId, cancellationToken);
                if (closePeers != null)
                {
                    foreach (var peer in closePeers)
                    {
                        AddPeer(peer);
                    }
                }
            }
            catch
            {
                // Bootstrap node unavailable, continue with others
            }
        }
    }

    private async Task MaintenanceLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_options.RefreshInterval, cancellationToken);

                // Refresh buckets
                await RefreshBucketsAsync(cancellationToken);

                // Cleanup stale peers
                CleanupStalePeers();

                // Republish provider records
                await RepublishProvidersAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task RefreshBucketsAsync(CancellationToken cancellationToken)
    {
        for (int i = 0; i < KeySize; i++)
        {
            if (_routingTable[i].Count == 0 || _routingTable[i].NeedsRefresh(_options.BucketRefreshInterval))
            {
                // Generate random ID in this bucket's range
                var randomId = GenerateRandomIdInBucket(i);
                await FindNodeAsync(randomId, KBucketSize, cancellationToken);
            }
        }
    }

    private void CleanupStalePeers()
    {
        var staleThreshold = DateTimeOffset.UtcNow - _options.PeerTimeout;

        foreach (var bucket in _routingTable)
        {
            var stalePeers = bucket.GetStalePeers(staleThreshold);
            foreach (var peer in stalePeers)
            {
                RemovePeer(peer.PeerId);
            }
        }
    }

    private async Task RepublishProvidersAsync(CancellationToken cancellationToken)
    {
        var expiredThreshold = DateTimeOffset.UtcNow - _options.ProviderRecordTtl;
        var toRemove = new List<string>();

        foreach (var (key, record) in _providers)
        {
            if (record.Timestamp < expiredThreshold)
            {
                toRemove.Add(key);
            }
            else
            {
                // Republish to closest nodes
                var closestNodes = await FindNodeAsync(record.Key, KBucketSize, cancellationToken);
                foreach (var node in closestNodes.Take(KBucketSize))
                {
                    await SendAddProviderAsync(node, record.Key, record.Provider, cancellationToken);
                }
            }
        }

        foreach (var key in toRemove)
        {
            _providers.TryRemove(key, out _);
        }
    }

    private IEnumerable<PeerInfo> GetClosestPeers(byte[] targetId, int count)
    {
        return _knownPeers.Values
            .OrderBy(p => p.PeerId, new XorDistanceComparer(targetId))
            .Take(count);
    }

    private int GetBucketIndex(byte[] peerId)
    {
        var distance = XorDistance(_localId, peerId);
        return GetLeadingZeroBits(distance);
    }

    private static byte[] XorDistance(byte[] a, byte[] b)
    {
        var result = new byte[Math.Max(a.Length, b.Length)];
        for (int i = 0; i < result.Length; i++)
        {
            var ai = i < a.Length ? a[i] : (byte)0;
            var bi = i < b.Length ? b[i] : (byte)0;
            result[i] = (byte)(ai ^ bi);
        }
        return result;
    }

    private static int GetLeadingZeroBits(byte[] data)
    {
        int count = 0;
        foreach (var b in data)
        {
            if (b == 0)
            {
                count += 8;
            }
            else
            {
                count += BitOperations.LeadingZeroCount(b) - 24; // Adjust for byte
                break;
            }
        }
        return Math.Min(count, KeySize - 1);
    }

    private byte[] GenerateRandomIdInBucket(int bucketIndex)
    {
        var id = new byte[32];
        RandomNumberGenerator.Fill(id);

        // Set the appropriate prefix to fall into the target bucket
        var prefixBits = bucketIndex;
        for (int i = 0; i < prefixBits / 8; i++)
        {
            id[i] = _localId[i];
        }

        if (prefixBits % 8 > 0)
        {
            var byteIndex = prefixBits / 8;
            var bitMask = (byte)(0xFF << (8 - prefixBits % 8));
            id[byteIndex] = (byte)((_localId[byteIndex] & bitMask) | (id[byteIndex] & ~bitMask));
            // Flip the bit at the boundary to ensure different bucket
            id[byteIndex] ^= (byte)(1 << (7 - prefixBits % 8));
        }

        return id;
    }

    private static byte[] GenerateNodeId()
    {
        var id = new byte[32];
        RandomNumberGenerator.Fill(id);
        return id;
    }

    // Network operations - these would be implemented with actual network calls
    private Task<IEnumerable<PeerInfo>?> QueryFindNodeAsync(PeerInfo peer, byte[] targetId, CancellationToken cancellationToken)
    {
        // TODO: Implement actual network RPC
        // For now, return empty to allow compilation
        return Task.FromResult<IEnumerable<PeerInfo>?>(Enumerable.Empty<PeerInfo>());
    }

    private Task<IEnumerable<PeerInfo>?> QueryGetProvidersAsync(PeerInfo peer, byte[] key, CancellationToken cancellationToken)
    {
        // TODO: Implement actual network RPC
        return Task.FromResult<IEnumerable<PeerInfo>?>(Enumerable.Empty<PeerInfo>());
    }

    private Task SendAddProviderAsync(PeerInfo peer, byte[] key, PeerInfo provider, CancellationToken cancellationToken)
    {
        // TODO: Implement actual network RPC
        return Task.CompletedTask;
    }

    private sealed class XorDistanceComparer(byte[] target) : IComparer<byte[]>
    {
        public int Compare(byte[]? x, byte[]? y)
        {
            if (x == null && y == null) return 0;
            if (x == null) return -1;
            if (y == null) return 1;

            var distX = XorDistance(x, target);
            var distY = XorDistance(y, target);

            for (int i = 0; i < Math.Max(distX.Length, distY.Length); i++)
            {
                var dx = i < distX.Length ? distX[i] : (byte)0;
                var dy = i < distY.Length ? distY[i] : (byte)0;
                if (dx != dy) return dx.CompareTo(dy);
            }
            return 0;
        }
    }

    private sealed record ProviderRecord
    {
        public required byte[] Key { get; init; }
        public required PeerInfo Provider { get; init; }
        public required DateTimeOffset Timestamp { get; init; }
    }
}

/// <summary>
/// K-bucket for Kademlia routing table.
/// </summary>
internal sealed class KBucket
{
    private readonly int _maxSize;
    private readonly List<PeerInfo> _peers = new();
    private readonly object _lock = new();
    private DateTimeOffset _lastRefresh = DateTimeOffset.UtcNow;

    public int Count
    {
        get { lock (_lock) return _peers.Count; }
    }

    public KBucket(int maxSize)
    {
        _maxSize = maxSize;
    }

    public bool TryAdd(PeerInfo peer)
    {
        lock (_lock)
        {
            var existing = _peers.FindIndex(p => p.PeerId.SequenceEqual(peer.PeerId));
            if (existing >= 0)
            {
                // Move to end (most recently seen)
                _peers.RemoveAt(existing);
                _peers.Add(peer);
                return true;
            }

            if (_peers.Count < _maxSize)
            {
                _peers.Add(peer);
                _lastRefresh = DateTimeOffset.UtcNow;
                return true;
            }

            // Bucket full - could implement replacement policy here
            return false;
        }
    }

    public bool TryRemove(byte[] peerId, out PeerInfo? peer)
    {
        lock (_lock)
        {
            var index = _peers.FindIndex(p => p.PeerId.SequenceEqual(peerId));
            if (index >= 0)
            {
                peer = _peers[index];
                _peers.RemoveAt(index);
                return true;
            }
            peer = null;
            return false;
        }
    }

    public bool NeedsRefresh(TimeSpan interval)
    {
        return DateTimeOffset.UtcNow - _lastRefresh > interval;
    }

    public IEnumerable<PeerInfo> GetStalePeers(DateTimeOffset threshold)
    {
        lock (_lock)
        {
            return _peers.Where(p => p.LastSeen < threshold).ToList();
        }
    }

    public IEnumerable<PeerInfo> GetAll()
    {
        lock (_lock)
        {
            return _peers.ToList();
        }
    }
}

/// <summary>
/// Configuration options for Kademlia DHT.
/// </summary>
public sealed record KademliaDhtOptions
{
    /// <summary>
    /// Local node ID. If null, a random ID will be generated.
    /// </summary>
    public byte[]? NodeId { get; init; }

    /// <summary>
    /// Bootstrap nodes to connect to initially.
    /// </summary>
    public IReadOnlyList<IPEndPoint> BootstrapNodes { get; init; } = [];

    /// <summary>
    /// How often to refresh the routing table.
    /// </summary>
    public TimeSpan RefreshInterval { get; init; } = TimeSpan.FromMinutes(10);

    /// <summary>
    /// How often to refresh individual buckets.
    /// </summary>
    public TimeSpan BucketRefreshInterval { get; init; } = TimeSpan.FromHours(1);

    /// <summary>
    /// How long before a peer is considered stale.
    /// </summary>
    public TimeSpan PeerTimeout { get; init; } = TimeSpan.FromMinutes(15);

    /// <summary>
    /// TTL for provider records.
    /// </summary>
    public TimeSpan ProviderRecordTtl { get; init; } = TimeSpan.FromHours(24);
}
