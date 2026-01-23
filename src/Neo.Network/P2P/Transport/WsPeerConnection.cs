// Copyright (C) 2015-2025 The Neo Project.
//
// WsPeerConnection.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using System;
using System.Buffers;
using System.Buffers.Binary;
using System.IO;
using System.Net;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace Neo.Network.P2P.Transport
{
    /// <summary>
    /// Represents a WebSocket connection to a remote peer.
    /// Uses binary messages with a 4-byte length-prefixed payload for parity with other transports.
    /// </summary>
    public sealed class WsPeerConnection : IAsyncDisposable
    {
        // Align with Neo.Network.P2P.Message.PayloadMaxSize plus header slack.
        private const int MaxMessageBytes = 0x02000000 + 16 + 4;

        private readonly ClientWebSocket _client;
        private readonly Uri _remoteUri;
        private readonly SemaphoreSlim _writeLock = new(1, 1);
        private int _disconnected;
        private bool _disposed;

        /// <summary>
        /// Event raised when the connection is closed.
        /// </summary>
        public event Action? OnDisconnected;

        /// <summary>
        /// Event raised when a message is received.
        /// </summary>
        public event Func<ReadOnlyMemory<byte>, Task>? OnMessageReceived;

        public EndPoint RemoteEndPoint => new DnsEndPoint(_remoteUri.Host, _remoteUri.Port);
        public bool IsConnected => _client.State == WebSocketState.Open;

        internal WsPeerConnection(ClientWebSocket client, Uri remoteUri)
        {
            _client = client;
            _remoteUri = remoteUri;
        }

        public async Task StartReceivingAsync(CancellationToken cancellationToken = default)
        {
            var buffer = ArrayPool<byte>.Shared.Rent(64 * 1024);
            try
            {
                using var ms = new MemoryStream();
                while (!cancellationToken.IsCancellationRequested && _client.State == WebSocketState.Open)
                {
                    var result = await _client.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
                    if (result.MessageType == WebSocketMessageType.Close)
                        break;

                    ms.Write(buffer, 0, result.Count);
                    if (ms.Length > MaxMessageBytes)
                        break;
                    if (!result.EndOfMessage)
                        continue;

                    // One complete message received; process
                    var data = ms.ToArray();
                    ms.SetLength(0);

                    if (data.Length >= 4)
                    {
                        var length = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(0, 4));
                        if (length == data.Length - 4)
                        {
                            var payload = new ReadOnlyMemory<byte>(data, 4, length);
                            if (OnMessageReceived != null)
                                await OnMessageReceived(payload);
                            continue;
                        }
                    }

                    // Fallback: deliver entire message
                    if (OnMessageReceived != null)
                        await OnMessageReceived(new ReadOnlyMemory<byte>(data));
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (WebSocketException)
            {
                // Socket closed or aborted.
            }
            catch
            {
                // Ignore unexpected receive errors; caller will observe disconnect.
            }
            finally
            {
                NotifyDisconnected();
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        /// <summary>
        /// Sends a raw payload with 4-byte length prefix.
        /// </summary>
        public async Task SendAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default)
        {
            if (data.Length + 4 > MaxMessageBytes)
                throw new ArgumentOutOfRangeException(nameof(data), $"Message length {data.Length} exceeds max {MaxMessageBytes - 4} bytes.");

            await _writeLock.WaitAsync(cancellationToken);
            try
            {
                var buffer = ArrayPool<byte>.Shared.Rent(4 + data.Length);
                try
                {
                    BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(0, 4), data.Length);
                    data.Span.CopyTo(buffer.AsSpan(4));
                    await _client.SendAsync(new ArraySegment<byte>(buffer, 0, 4 + data.Length), WebSocketMessageType.Binary, true, cancellationToken);
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(buffer);
                }
            }
            finally
            {
                _writeLock.Release();
            }
        }

        /// <summary>
        /// Sends a Neo P2P message [len][cmd][payload].
        /// </summary>
        public async Task SendMessageAsync(byte command, ReadOnlyMemory<byte> payload, CancellationToken cancellationToken = default)
        {
            var messageLength = 1 + payload.Length;
            var buffer = ArrayPool<byte>.Shared.Rent(messageLength);
            try
            {
                buffer[0] = command;
                payload.Span.CopyTo(buffer.AsSpan(1));
                await SendAsync(buffer.AsMemory(0, messageLength), cancellationToken);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (_disposed) return;
            _disposed = true;
            try
            {
                if (_client.State == WebSocketState.Open)
                    await _client.CloseAsync(WebSocketCloseStatus.NormalClosure, "dispose", CancellationToken.None);
            }
            catch { }
            _client.Dispose();
            _writeLock.Dispose();
            NotifyDisconnected();
        }

        private void NotifyDisconnected()
        {
            if (Interlocked.Exchange(ref _disconnected, 1) != 0)
                return;

            OnDisconnected?.Invoke();
        }
    }

}
