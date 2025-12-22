// Copyright (C) 2015-2025 The Neo Project.
//
// ITransportService.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using System.Threading;
using System.Threading.Tasks;

namespace Neo.Orleans.Services
{
    /// <summary>
    /// Interface for network transport operations used by Orleans grains.
    /// Abstracts the underlying transport layer (TCP, WebSocket, QUIC).
    /// </summary>
    public interface ITransportService
    {
        /// <summary>
        /// Sends a message to a remote peer.
        /// </summary>
        /// <param name="address">The remote peer's address.</param>
        /// <param name="port">The remote peer's port.</param>
        /// <param name="message">The message bytes to send.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>True if the message was sent successfully, false otherwise.</returns>
        Task<bool> SendAsync(string address, int port, byte[] message, CancellationToken cancellationToken = default);

        /// <summary>
        /// Establishes a connection to a remote peer.
        /// </summary>
        /// <param name="address">The remote peer's address.</param>
        /// <param name="port">The remote peer's port.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>True if the connection was established, false otherwise.</returns>
        Task<bool> ConnectAsync(string address, int port, CancellationToken cancellationToken = default);

        /// <summary>
        /// Disconnects from a remote peer.
        /// </summary>
        /// <param name="address">The remote peer's address.</param>
        /// <param name="port">The remote peer's port.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task DisconnectAsync(string address, int port, CancellationToken cancellationToken = default);

        /// <summary>
        /// Checks if a connection to a remote peer is active.
        /// </summary>
        /// <param name="address">The remote peer's address.</param>
        /// <param name="port">The remote peer's port.</param>
        /// <returns>True if connected, false otherwise.</returns>
        bool IsConnected(string address, int port);
    }
}
