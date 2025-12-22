// Copyright (C) 2015-2025 The Neo Project.
//
// IPeerDiscovery.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace Neo.Network.Discovery
{
    /// <summary>
    /// Interface for peer discovery mechanisms.
    /// Inspired by libp2p peer discovery specifications.
    /// </summary>
    public interface IPeerDiscovery : IAsyncDisposable
    {
        /// <summary>
        /// Gets the name of this discovery mechanism.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Gets whether this discovery mechanism is currently running.
        /// </summary>
        bool IsRunning { get; }

        /// <summary>
        /// Event raised when a new peer is discovered.
        /// </summary>
        event Action<PeerInfo>? OnPeerDiscovered;

        /// <summary>
        /// Event raised when a peer is no longer available.
        /// </summary>
        event Action<PeerInfo>? OnPeerLost;

        /// <summary>
        /// Starts the peer discovery mechanism.
        /// </summary>
        Task StartAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Stops the peer discovery mechanism.
        /// </summary>
        Task StopAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Finds peers that match the given criteria.
        /// </summary>
        /// <param name="count">Maximum number of peers to find.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Collection of discovered peers.</returns>
        Task<IEnumerable<PeerInfo>> FindPeersAsync(int count, CancellationToken cancellationToken = default);

        /// <summary>
        /// Announces this node to the network for discovery by other peers.
        /// </summary>
        Task AnnounceAsync(CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Represents information about a discovered peer.
    /// </summary>
    public sealed record PeerInfo
    {
        /// <summary>
        /// Unique identifier for the peer (typically derived from public key).
        /// </summary>
        public required byte[] PeerId { get; init; }

        /// <summary>
        /// Network addresses where this peer can be reached.
        /// </summary>
        public required IReadOnlyList<IPEndPoint> Addresses { get; init; }

        /// <summary>
        /// Protocols supported by this peer.
        /// </summary>
        public IReadOnlyList<string> Protocols { get; init; } = [];

        /// <summary>
        /// When this peer was last seen.
        /// </summary>
        public DateTimeOffset LastSeen { get; init; } = DateTimeOffset.UtcNow;

        /// <summary>
        /// Discovery source that found this peer.
        /// </summary>
        public string? DiscoverySource { get; init; }

        /// <summary>
        /// Additional metadata about the peer.
        /// </summary>
        public IReadOnlyDictionary<string, string>? Metadata { get; init; }
    }
}
