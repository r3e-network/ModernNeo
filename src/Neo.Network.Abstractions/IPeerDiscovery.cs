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

namespace Neo.Network.Abstractions
{
    /// <summary>
    /// Provides peer discovery functionality.
    /// </summary>
    public interface IPeerDiscovery : IAsyncDisposable
    {
        /// <summary>
        /// Gets the discovery method name.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Gets whether discovery is currently active.
        /// </summary>
        bool IsActive { get; }

        /// <summary>
        /// Starts the discovery process.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task StartAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Stops the discovery process.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task StopAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Discovers peers asynchronously.
        /// </summary>
        /// <param name="maxPeers">Maximum number of peers to discover.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Discovered peer endpoints.</returns>
        Task<IReadOnlyList<IPEndPoint>> DiscoverPeersAsync(int maxPeers, CancellationToken cancellationToken = default);

        /// <summary>
        /// Announces this node to the network.
        /// </summary>
        /// <param name="endpoint">The endpoint to announce.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task AnnounceAsync(IPEndPoint endpoint, CancellationToken cancellationToken = default);

        /// <summary>
        /// Event raised when new peers are discovered.
        /// </summary>
        event EventHandler<PeersDiscoveredEventArgs>? PeersDiscovered;
    }

    /// <summary>
    /// Event arguments for peers discovered events.
    /// </summary>
    public class PeersDiscoveredEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the discovered peer endpoints.
        /// </summary>
        public IReadOnlyList<IPEndPoint> Peers { get; }

        /// <summary>
        /// Gets the discovery source.
        /// </summary>
        public string Source { get; }

        /// <summary>
        /// Creates a new instance.
        /// </summary>
        public PeersDiscoveredEventArgs(IReadOnlyList<IPEndPoint> peers, string source)
        {
            Peers = peers ?? throw new ArgumentNullException(nameof(peers));
            Source = source ?? throw new ArgumentNullException(nameof(source));
        }
    }
}
