// Copyright (C) 2015-2025 The Neo Project.
//
// ILocalNodeService.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Core.Abstractions;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Neo.Network.Abstractions
{
    /// <summary>
    /// Provides local node network operations.
    /// </summary>
    public interface ILocalNodeService
    {
        /// <summary>
        /// Gets the number of connected peers.
        /// </summary>
        int ConnectedCount { get; }

        /// <summary>
        /// Gets the number of unconnected (known) peers.
        /// </summary>
        int UnconnectedCount { get; }

        /// <summary>
        /// Gets the maximum allowed connections.
        /// </summary>
        int MaxConnections { get; }

        /// <summary>
        /// Gets all connected peer information.
        /// </summary>
        /// <returns>Connected peer information.</returns>
        IReadOnlyList<IPeerInfo> GetConnectedPeers();

        /// <summary>
        /// Broadcasts a message to all connected peers.
        /// </summary>
        /// <param name="message">The message to broadcast.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task BroadcastAsync(INetworkMessage message, CancellationToken cancellationToken = default);

        /// <summary>
        /// Relays an inventory item to connected peers.
        /// </summary>
        /// <param name="inventory">The inventory to relay.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task RelayAsync(IInventory inventory, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sends a message to a specific peer.
        /// </summary>
        /// <param name="peerId">The peer identifier.</param>
        /// <param name="message">The message to send.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task SendToPeerAsync(string peerId, INetworkMessage message, CancellationToken cancellationToken = default);

        /// <summary>
        /// Disconnects a peer.
        /// </summary>
        /// <param name="peerId">The peer identifier.</param>
        /// <param name="reason">The reason for disconnection.</param>
        Task DisconnectPeerAsync(string peerId, string? reason = null);

        /// <summary>
        /// Event raised when a peer connects.
        /// </summary>
        event EventHandler<PeerConnectedEventArgs>? PeerConnected;

        /// <summary>
        /// Event raised when a peer disconnects.
        /// </summary>
        event EventHandler<PeerDisconnectedEventArgs>? PeerDisconnected;

        /// <summary>
        /// Event raised when a message is received from a peer.
        /// </summary>
        event EventHandler<PeerMessageReceivedEventArgs>? MessageReceived;
    }

    /// <summary>
    /// Represents information about a connected peer.
    /// </summary>
    public interface IPeerInfo
    {
        /// <summary>
        /// Gets the peer identifier.
        /// </summary>
        string PeerId { get; }

        /// <summary>
        /// Gets the remote address.
        /// </summary>
        string Address { get; }

        /// <summary>
        /// Gets the peer's user agent.
        /// </summary>
        string UserAgent { get; }

        /// <summary>
        /// Gets the peer's protocol version.
        /// </summary>
        uint Version { get; }

        /// <summary>
        /// Gets the peer's last known block height.
        /// </summary>
        uint LastBlockIndex { get; }

        /// <summary>
        /// Gets the connection time.
        /// </summary>
        DateTimeOffset ConnectedAt { get; }
    }

    /// <summary>
    /// Event arguments for peer connected events.
    /// </summary>
    public class PeerConnectedEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the peer information.
        /// </summary>
        public IPeerInfo Peer { get; }

        /// <summary>
        /// Creates a new instance.
        /// </summary>
        public PeerConnectedEventArgs(IPeerInfo peer)
        {
            Peer = peer ?? throw new ArgumentNullException(nameof(peer));
        }
    }

    /// <summary>
    /// Event arguments for peer disconnected events.
    /// </summary>
    public class PeerDisconnectedEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the peer identifier.
        /// </summary>
        public string PeerId { get; }

        /// <summary>
        /// Gets the disconnection reason.
        /// </summary>
        public string? Reason { get; }

        /// <summary>
        /// Creates a new instance.
        /// </summary>
        public PeerDisconnectedEventArgs(string peerId, string? reason = null)
        {
            PeerId = peerId ?? throw new ArgumentNullException(nameof(peerId));
            Reason = reason;
        }
    }

    /// <summary>
    /// Event arguments for message received events.
    /// </summary>
    public class PeerMessageReceivedEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the peer identifier.
        /// </summary>
        public string PeerId { get; }

        /// <summary>
        /// Gets the received message.
        /// </summary>
        public INetworkMessage Message { get; }

        /// <summary>
        /// Creates a new instance.
        /// </summary>
        public PeerMessageReceivedEventArgs(string peerId, INetworkMessage message)
        {
            PeerId = peerId ?? throw new ArgumentNullException(nameof(peerId));
            Message = message ?? throw new ArgumentNullException(nameof(message));
        }
    }
}
