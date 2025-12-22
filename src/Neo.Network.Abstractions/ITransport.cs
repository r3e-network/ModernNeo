// Copyright (C) 2015-2025 The Neo Project.
//
// ITransport.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace Neo.Network.Abstractions
{
    /// <summary>
    /// Represents a network transport layer (TCP, QUIC, WebSocket).
    /// </summary>
    public interface ITransport : IAsyncDisposable
    {
        /// <summary>
        /// Gets the transport type identifier.
        /// </summary>
        TransportType Type { get; }

        /// <summary>
        /// Gets whether the transport is currently listening.
        /// </summary>
        bool IsListening { get; }

        /// <summary>
        /// Starts listening for incoming connections.
        /// </summary>
        /// <param name="endpoint">The endpoint to listen on.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task StartListeningAsync(IPEndPoint endpoint, CancellationToken cancellationToken = default);

        /// <summary>
        /// Stops listening for incoming connections.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task StopListeningAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Connects to a remote endpoint.
        /// </summary>
        /// <param name="endpoint">The remote endpoint.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A peer connection if successful, null otherwise.</returns>
        Task<IPeerConnection?> ConnectAsync(IPEndPoint endpoint, CancellationToken cancellationToken = default);

        /// <summary>
        /// Event raised when a new connection is accepted.
        /// </summary>
        event EventHandler<ConnectionAcceptedEventArgs>? ConnectionAccepted;
    }

    /// <summary>
    /// Defines the types of network transports.
    /// </summary>
    public enum TransportType : byte
    {
        /// <summary>
        /// TCP transport.
        /// </summary>
        Tcp = 0,

        /// <summary>
        /// QUIC transport.
        /// </summary>
        Quic = 1,

        /// <summary>
        /// WebSocket transport.
        /// </summary>
        WebSocket = 2
    }

    /// <summary>
    /// Event arguments for connection accepted events.
    /// </summary>
    public class ConnectionAcceptedEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the accepted connection.
        /// </summary>
        public IPeerConnection Connection { get; }

        /// <summary>
        /// Creates a new instance.
        /// </summary>
        public ConnectionAcceptedEventArgs(IPeerConnection connection)
        {
            Connection = connection ?? throw new ArgumentNullException(nameof(connection));
        }
    }
}
