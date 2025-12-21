// Copyright (C) 2015-2025 The Neo Project.
//
// QuicPeerConnection.cs file belongs to the neo project and is free
// software distributed under the MIT software license.

using System;
using System.Buffers;
using System.IO;
using System.Net;
using System.Net.Quic;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;

namespace Neo.Network.P2P.Transport;

/// <summary>
/// Represents a QUIC connection to a remote peer.
/// Provides multiplexed bidirectional streams for message exchange.
/// </summary>
[SupportedOSPlatform("linux")]
[SupportedOSPlatform("windows")]
[SupportedOSPlatform("osx")]
public sealed class QuicPeerConnection : IAsyncDisposable
{
    private readonly QuicConnection _connection;
    private readonly bool _isIncoming;
    private QuicStream? _controlStream;
    private bool _disposed;

    /// <summary>
    /// Event raised when the connection is closed.
    /// </summary>
    public event Action? OnDisconnected;

    /// <summary>
    /// Event raised when a message is received.
    /// </summary>
    public event Func<ReadOnlyMemory<byte>, Task>? OnMessageReceived;

    /// <summary>
    /// Gets the remote endpoint of this connection.
    /// </summary>
    public EndPoint RemoteEndPoint => _connection.RemoteEndPoint;

    /// <summary>
    /// Gets the local endpoint of this connection.
    /// </summary>
    public EndPoint LocalEndPoint => _connection.LocalEndPoint;

    /// <summary>
    /// Gets whether this is an incoming connection.
    /// </summary>
    public bool IsIncoming => _isIncoming;

    /// <summary>
    /// Gets whether the connection is still open.
    /// </summary>
    public bool IsConnected => !_disposed;

    internal QuicPeerConnection(QuicConnection connection, bool isIncoming)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _isIncoming = isIncoming;
    }

    /// <summary>
    /// Starts receiving messages on this connection.
    /// </summary>
    public async Task StartReceivingAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Accept the control stream (bidirectional)
            _controlStream = await _connection.AcceptInboundStreamAsync(cancellationToken);
            await ReceiveMessagesAsync(_controlStream, cancellationToken);
        }
        catch (QuicException)
        {
            // Connection closed
        }
        catch (OperationCanceledException)
        {
            // Cancelled
        }
        finally
        {
            OnDisconnected?.Invoke();
        }
    }

    /// <summary>
    /// Opens a new bidirectional stream for communication.
    /// </summary>
    public async Task<QuicStream> OpenStreamAsync(CancellationToken cancellationToken = default)
    {
        return await _connection.OpenOutboundStreamAsync(QuicStreamType.Bidirectional, cancellationToken);
    }

    /// <summary>
    /// Sends a message to the remote peer.
    /// </summary>
    public async Task SendAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default)
    {
        if (_controlStream == null)
        {
            _controlStream = await _connection.OpenOutboundStreamAsync(QuicStreamType.Bidirectional, cancellationToken);
        }

        // Write length-prefixed message
        var lengthBuffer = new byte[4];
        BitConverter.TryWriteBytes(lengthBuffer, data.Length);

        await _controlStream.WriteAsync(lengthBuffer, cancellationToken);
        await _controlStream.WriteAsync(data, cancellationToken);
        await _controlStream.FlushAsync(cancellationToken);
    }

    /// <summary>
    /// Sends a Neo P2P message to the remote peer.
    /// </summary>
    public async Task SendMessageAsync(byte command, ReadOnlyMemory<byte> payload, CancellationToken cancellationToken = default)
    {
        // Neo message format: [length:4][command:1][payload:n]
        var messageLength = 1 + payload.Length;
        var buffer = ArrayPool<byte>.Shared.Rent(4 + messageLength);

        try
        {
            BitConverter.TryWriteBytes(buffer.AsSpan(0, 4), messageLength);
            buffer[4] = command;
            payload.Span.CopyTo(buffer.AsSpan(5));

            await SendAsync(buffer.AsMemory(0, 4 + messageLength), cancellationToken);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    /// <summary>
    /// Closes the connection gracefully.
    /// </summary>
    public async Task CloseAsync(long errorCode = 0)
    {
        if (_disposed) return;

        try
        {
            await _connection.CloseAsync(errorCode);
        }
        catch (QuicException)
        {
            // Already closed
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        if (_controlStream != null)
        {
            await _controlStream.DisposeAsync();
        }

        await _connection.DisposeAsync();
        OnDisconnected?.Invoke();
    }

    private async Task ReceiveMessagesAsync(QuicStream stream, CancellationToken cancellationToken)
    {
        var lengthBuffer = new byte[4];

        while (!cancellationToken.IsCancellationRequested)
        {
            // Read message length
            var bytesRead = await ReadExactAsync(stream, lengthBuffer, cancellationToken);
            if (bytesRead < 4) break;

            var messageLength = BitConverter.ToInt32(lengthBuffer);
            if (messageLength <= 0 || messageLength > 10 * 1024 * 1024) // Max 10MB
            {
                throw new InvalidDataException($"Invalid message length: {messageLength}");
            }

            // Read message body
            var messageBuffer = ArrayPool<byte>.Shared.Rent(messageLength);
            try
            {
                bytesRead = await ReadExactAsync(stream, messageBuffer.AsMemory(0, messageLength), cancellationToken);
                if (bytesRead < messageLength) break;

                // Notify listeners
                if (OnMessageReceived != null)
                {
                    await OnMessageReceived(messageBuffer.AsMemory(0, messageLength));
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(messageBuffer);
            }
        }
    }

    private static async Task<int> ReadExactAsync(QuicStream stream, Memory<byte> buffer, CancellationToken cancellationToken)
    {
        var totalRead = 0;
        while (totalRead < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer[totalRead..], cancellationToken);
            if (read == 0) break;
            totalRead += read;
        }
        return totalRead;
    }
}
