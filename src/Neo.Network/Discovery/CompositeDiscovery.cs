// Copyright (C) 2015-2025 The Neo Project.
//
// CompositeDiscovery.cs file belongs to the neo project and is free
// software distributed under the MIT software license.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Neo.Network.Discovery;

/// <summary>
/// Composite peer discovery that combines multiple discovery mechanisms.
/// Aggregates results from DHT, mDNS, and other discovery sources.
/// </summary>
public sealed class CompositeDiscovery : IPeerDiscovery
{
    private readonly List<IPeerDiscovery> _discoveries = new();
    private readonly ConcurrentDictionary<string, PeerInfo> _allPeers = new();
    private CancellationTokenSource? _cts;

    public string Name => "Composite";
    public bool IsRunning => _cts != null && !_cts.IsCancellationRequested;

    public event Action<PeerInfo>? OnPeerDiscovered;
    public event Action<PeerInfo>? OnPeerLost;

    /// <summary>
    /// Gets the number of registered discovery mechanisms.
    /// </summary>
    public int DiscoveryCount => _discoveries.Count;

    /// <summary>
    /// Gets the total number of discovered peers.
    /// </summary>
    public int PeerCount => _allPeers.Count;

    /// <summary>
    /// Adds a discovery mechanism to the composite.
    /// </summary>
    public void AddDiscovery(IPeerDiscovery discovery)
    {
        _discoveries.Add(discovery);
        discovery.OnPeerDiscovered += HandlePeerDiscovered;
        discovery.OnPeerLost += HandlePeerLost;
    }

    /// <summary>
    /// Removes a discovery mechanism from the composite.
    /// </summary>
    public bool RemoveDiscovery(IPeerDiscovery discovery)
    {
        discovery.OnPeerDiscovered -= HandlePeerDiscovered;
        discovery.OnPeerLost -= HandlePeerLost;
        return _discoveries.Remove(discovery);
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (IsRunning)
            return;

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        var tasks = _discoveries.Select(d => d.StartAsync(_cts.Token));
        await Task.WhenAll(tasks);
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (_cts == null)
            return;

        _cts.Cancel();

        var tasks = _discoveries.Select(d => d.StopAsync(cancellationToken));
        await Task.WhenAll(tasks);

        _cts.Dispose();
        _cts = null;
    }

    public async Task<IEnumerable<PeerInfo>> FindPeersAsync(int count, CancellationToken cancellationToken = default)
    {
        var allPeers = new ConcurrentBag<PeerInfo>();

        var tasks = _discoveries.Select(async d =>
        {
            try
            {
                var peers = await d.FindPeersAsync(count, cancellationToken);
                foreach (var peer in peers)
                {
                    allPeers.Add(peer);
                }
            }
            catch (OperationCanceledException) { }
            catch { /* Log error */ }
        });

        await Task.WhenAll(tasks);

        // Deduplicate and return
        return allPeers
            .GroupBy(p => Convert.ToHexString(p.PeerId))
            .Select(g => g.OrderByDescending(p => p.LastSeen).First())
            .Take(count);
    }

    public async Task AnnounceAsync(CancellationToken cancellationToken = default)
    {
        var tasks = _discoveries.Select(d => d.AnnounceAsync(cancellationToken));
        await Task.WhenAll(tasks);
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync(CancellationToken.None);

        foreach (var discovery in _discoveries)
        {
            await discovery.DisposeAsync();
        }

        _discoveries.Clear();
    }

    private void HandlePeerDiscovered(PeerInfo peer)
    {
        var key = Convert.ToHexString(peer.PeerId);
        var isNew = !_allPeers.ContainsKey(key);
        _allPeers[key] = peer;

        if (isNew)
        {
            OnPeerDiscovered?.Invoke(peer);
        }
    }

    private void HandlePeerLost(PeerInfo peer)
    {
        var key = Convert.ToHexString(peer.PeerId);
        if (_allPeers.TryRemove(key, out _))
        {
            OnPeerLost?.Invoke(peer);
        }
    }
}
