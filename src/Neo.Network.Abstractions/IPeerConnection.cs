// Copyright (C) 2015-2025 The Neo Project.
//
// IPeerConnection.cs file belongs to the neo project and is free
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
    /// Represents a connection to a remote peer.
    /// </summary>
    public interface IPeerConnection : IAsyncDisposable
    {
        /// <summary>
        /// Gets the unique identifier for this connection.
        /// </summary>
        string ConnectionId { get; }

        /// <summary>
        /// Gets the remote endpoint.
        /// </summary>
        IPEndPoint RemoteEndPoint { get; }

        /// <summary>
        /// Gets the local endpoint.
        /// </summary>
        IPEndPoint LocalEndPoint { get; }

        /// <summary>
        /// Gets whether the connection is currently open.
        /// </summary>
        bool IsConnected { get; }

        /// <summary>
        /// Gets the transport type of this connection.
        /// </summary>
        TransportType TransportType { get; }

        /// <summary>
        /// Sends data to the remote peer.
        /// </summary>
        /// <param name="data">The data to send.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task SendAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default);

        /// <summary>
        /// Receives data from the remote peer.
        /// </summary>
        /// <param name="buffer">The buffer to receive into.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The number of bytes received, or 0 if the connection is closed.</returns>
        Task<int> ReceiveAsync(Memory<byte> buffer, CancellationToken cancellationToken = default);

        /// <summary>
        /// Closes the connection gracefully.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task CloseAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Event raised when data is received.
        /// </summary>
        event EventHandler<DataReceivedEventArgs>? DataReceived;

        /// <summary>
        /// Event raised when the connection is closed.
        /// </summary>
        event EventHandler<ConnectionClosedEventArgs>? ConnectionClosed;
    }

    /// <summary>
    /// Event arguments for data received events.
    /// </summary>
    public class DataReceivedEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the received data.
        /// </summary>
        public ReadOnlyMemory<byte> Data { get; }

        /// <summary>
        /// Creates a new instance.
        /// </summary>
        public DataReceivedEventArgs(ReadOnlyMemory<byte> data)
        {
            Data = data;
        }
    }

    /// <summary>
    /// Event arguments for connection closed events.
    /// </summary>
    public class ConnectionClosedEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the reason for closure.
        /// </summary>
        public string? Reason { get; }

        /// <summary>
        /// Gets whether the closure was initiated locally.
        /// </summary>
        public bool IsLocal { get; }

        /// <summary>
        /// Creates a new instance.
        /// </summary>
        public ConnectionClosedEventArgs(string? reason = null, bool isLocal = false)
        {
            Reason = reason;
            IsLocal = isLocal;
        }
    }
}
