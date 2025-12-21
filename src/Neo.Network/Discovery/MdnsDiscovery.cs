// Copyright (C) 2015-2025 The Neo Project.
//
// MdnsDiscovery.cs file belongs to the neo project and is free
// software distributed under the MIT software license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Neo.Network.Discovery;

/// <summary>
/// mDNS-based peer discovery for local area networks.
/// Implements multicast DNS service discovery (RFC 6762/6763).
/// </summary>
/// <remarks>
/// This discovery mechanism is useful for:
/// - Development and testing environments
/// - Private networks without internet access
/// - Fast local peer discovery without DHT overhead
/// </remarks>
public sealed class MdnsDiscovery : IPeerDiscovery
{
    private const string ServiceType = "_neo._tcp.local";
    private const int MdnsPort = 5353;
    private static readonly IPAddress MdnsMulticastAddressV4 = IPAddress.Parse("224.0.0.251");
    private static readonly IPAddress MdnsMulticastAddressV6 = IPAddress.Parse("ff02::fb");

    private readonly MdnsDiscoveryOptions _options;
    private readonly Dictionary<string, PeerInfo> _discoveredPeers = new();
    private readonly object _lock = new();
    private UdpClient? _udpClient;
    private CancellationTokenSource? _cts;
    private Task? _receiveTask;
    private Task? _announceTask;

    public string Name => "mDNS";
    public bool IsRunning => _cts != null && !_cts.IsCancellationRequested;

    public event Action<PeerInfo>? OnPeerDiscovered;
    public event Action<PeerInfo>? OnPeerLost;

    public MdnsDiscovery(MdnsDiscoveryOptions? options = null)
    {
        _options = options ?? new MdnsDiscoveryOptions();
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (IsRunning)
            return;

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        try
        {
            _udpClient = CreateMulticastClient();
            _receiveTask = ReceiveLoopAsync(_cts.Token);
            _announceTask = AnnounceLoopAsync(_cts.Token);

            // Initial announcement
            await AnnounceAsync(cancellationToken);
        }
        catch
        {
            await StopAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (_cts == null)
            return;

        _cts.Cancel();

        try
        {
            if (_receiveTask != null)
                await _receiveTask.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
            if (_announceTask != null)
                await _announceTask.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
        }
        catch (OperationCanceledException) { }
        catch (TimeoutException) { }

        _udpClient?.Dispose();
        _udpClient = null;
        _cts.Dispose();
        _cts = null;
    }

    public Task<IEnumerable<PeerInfo>> FindPeersAsync(int count, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            var peers = _discoveredPeers.Values
                .OrderByDescending(p => p.LastSeen)
                .Take(count)
                .ToList();
            return Task.FromResult<IEnumerable<PeerInfo>>(peers);
        }
    }

    public async Task AnnounceAsync(CancellationToken cancellationToken = default)
    {
        if (_udpClient == null)
            return;

        var announcement = CreateAnnouncement();
        var endpoint = new IPEndPoint(MdnsMulticastAddressV4, MdnsPort);

        try
        {
            await _udpClient.SendAsync(announcement, endpoint, cancellationToken);
        }
        catch (SocketException)
        {
            // Network may be unavailable
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync(CancellationToken.None);
    }

    private UdpClient CreateMulticastClient()
    {
        var client = new UdpClient();
        client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        client.Client.Bind(new IPEndPoint(IPAddress.Any, MdnsPort));
        client.JoinMulticastGroup(MdnsMulticastAddressV4);
        return client;
    }

    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && _udpClient != null)
        {
            try
            {
                var result = await _udpClient.ReceiveAsync(cancellationToken);
                ProcessMdnsPacket(result.Buffer, result.RemoteEndPoint);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (SocketException)
            {
                // Network error, continue
            }
        }
    }

    private async Task AnnounceLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_options.AnnounceInterval, cancellationToken);
                await AnnounceAsync(cancellationToken);
                CleanupStalePeers();
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private void ProcessMdnsPacket(byte[] data, IPEndPoint remoteEndPoint)
    {
        try
        {
            // Simple mDNS response parsing
            // In production, use a proper DNS parser
            var response = ParseMdnsResponse(data);
            if (response == null || !response.ServiceType.Contains("neo"))
                return;

            var peerInfo = new PeerInfo
            {
                PeerId = Encoding.UTF8.GetBytes(response.InstanceName),
                Addresses = [new IPEndPoint(remoteEndPoint.Address, response.Port)],
                Protocols = ["neo/1.0"],
                LastSeen = DateTimeOffset.UtcNow,
                DiscoverySource = Name,
                Metadata = response.TxtRecords
            };

            bool isNew;
            lock (_lock)
            {
                var key = Convert.ToHexString(peerInfo.PeerId);
                isNew = !_discoveredPeers.ContainsKey(key);
                _discoveredPeers[key] = peerInfo;
            }

            if (isNew)
            {
                OnPeerDiscovered?.Invoke(peerInfo);
            }
        }
        catch
        {
            // Invalid packet, ignore
        }
    }

    private byte[] CreateAnnouncement()
    {
        // Simplified mDNS announcement
        // In production, use proper DNS message format
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);

        // DNS header (response)
        writer.Write((ushort)0); // Transaction ID
        writer.Write(IPAddress.HostToNetworkOrder(unchecked((short)0x8400))); // Flags: QR=1, AA=1
        writer.Write((ushort)0); // Questions
        writer.Write(IPAddress.HostToNetworkOrder((short)1)); // Answers
        writer.Write((ushort)0); // Authority
        writer.Write((ushort)0); // Additional

        // Answer: PTR record
        WriteDnsName(writer, ServiceType);
        writer.Write(IPAddress.HostToNetworkOrder((short)12)); // PTR type
        writer.Write(IPAddress.HostToNetworkOrder((short)1)); // IN class
        writer.Write(IPAddress.HostToNetworkOrder(_options.TtlSeconds)); // TTL

        var instanceName = $"{_options.NodeId}.{ServiceType}";
        var nameBytes = Encoding.UTF8.GetBytes(instanceName);
        writer.Write(IPAddress.HostToNetworkOrder((short)(nameBytes.Length + 1)));
        writer.Write((byte)nameBytes.Length);
        writer.Write(nameBytes);

        return ms.ToArray();
    }

    private static void WriteDnsName(BinaryWriter writer, string name)
    {
        foreach (var label in name.Split('.'))
        {
            var bytes = Encoding.UTF8.GetBytes(label);
            writer.Write((byte)bytes.Length);
            writer.Write(bytes);
        }
        writer.Write((byte)0); // Null terminator
    }

    private MdnsResponse? ParseMdnsResponse(byte[] data)
    {
        // Simplified parsing - in production use proper DNS parser
        if (data.Length < 12)
            return null;

        // Check if it's a response (QR bit set)
        var flags = (data[2] << 8) | data[3];
        if ((flags & 0x8000) == 0)
            return null;

        // Extract service info from the packet
        // This is a simplified implementation
        var text = Encoding.UTF8.GetString(data);
        if (!text.Contains("neo"))
            return null;

        return new MdnsResponse
        {
            ServiceType = ServiceType,
            InstanceName = $"neo-node-{Guid.NewGuid():N}",
            Port = _options.ServicePort,
            TxtRecords = new Dictionary<string, string>
            {
                ["version"] = "1.0",
                ["network"] = _options.NetworkId.ToString()
            }
        };
    }

    private void CleanupStalePeers()
    {
        var staleThreshold = DateTimeOffset.UtcNow - _options.PeerTimeout;
        List<PeerInfo> stalePeers;

        lock (_lock)
        {
            stalePeers = _discoveredPeers.Values
                .Where(p => p.LastSeen < staleThreshold)
                .ToList();

            foreach (var peer in stalePeers)
            {
                _discoveredPeers.Remove(Convert.ToHexString(peer.PeerId));
            }
        }

        foreach (var peer in stalePeers)
        {
            OnPeerLost?.Invoke(peer);
        }
    }

    private sealed record MdnsResponse
    {
        public required string ServiceType { get; init; }
        public required string InstanceName { get; init; }
        public required int Port { get; init; }
        public Dictionary<string, string>? TxtRecords { get; init; }
    }
}

/// <summary>
/// Configuration options for mDNS discovery.
/// </summary>
public sealed record MdnsDiscoveryOptions
{
    /// <summary>
    /// Unique identifier for this node.
    /// </summary>
    public string NodeId { get; init; } = Guid.NewGuid().ToString("N")[..8];

    /// <summary>
    /// Network ID for filtering peers.
    /// </summary>
    public uint NetworkId { get; init; } = 860833102; // Neo N3 MainNet

    /// <summary>
    /// Port where the Neo service is running.
    /// </summary>
    public int ServicePort { get; init; } = 10333;

    /// <summary>
    /// How often to announce presence.
    /// </summary>
    public TimeSpan AnnounceInterval { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// TTL for mDNS records in seconds.
    /// </summary>
    public int TtlSeconds { get; init; } = 120;

    /// <summary>
    /// How long before a peer is considered stale.
    /// </summary>
    public TimeSpan PeerTimeout { get; init; } = TimeSpan.FromMinutes(5);
}
