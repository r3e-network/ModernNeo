// Copyright (C) 2015-2025 The Neo Project.
//
// IDhtRpcService.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Neo.Network.Discovery
{
    /// <summary>
    /// Interface for Kademlia DHT RPC operations.
    /// Abstracts the network transport layer for DHT protocol messages.
    /// </summary>
    public interface IDhtRpcService
    {
        /// <summary>
        /// Sends a FIND_NODE RPC to a peer and returns the closest nodes to the target.
        /// </summary>
        /// <param name="peer">The peer to query.</param>
        /// <param name="targetId">The target node ID to find.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Collection of peers closest to the target, or null if the query failed.</returns>
        Task<IEnumerable<PeerInfo>?> FindNodeAsync(PeerInfo peer, byte[] targetId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sends a GET_PROVIDERS RPC to a peer and returns providers for the given key.
        /// </summary>
        /// <param name="peer">The peer to query.</param>
        /// <param name="key">The content key to find providers for.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Collection of provider peers, or null if the query failed.</returns>
        Task<IEnumerable<PeerInfo>?> GetProvidersAsync(PeerInfo peer, byte[] key, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sends an ADD_PROVIDER RPC to announce this node as a provider for the given key.
        /// </summary>
        /// <param name="peer">The peer to notify.</param>
        /// <param name="key">The content key being provided.</param>
        /// <param name="provider">The provider information to announce.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task AddProviderAsync(PeerInfo peer, byte[] key, PeerInfo provider, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sends a PING RPC to check if a peer is alive.
        /// </summary>
        /// <param name="peer">The peer to ping.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>True if the peer responded, false otherwise.</returns>
        Task<bool> PingAsync(PeerInfo peer, CancellationToken cancellationToken = default);
    }
}
