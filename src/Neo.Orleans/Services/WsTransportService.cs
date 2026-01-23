// Copyright (C) 2015-2025 The Neo Project.
//
// WsTransportService.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Network.P2P;
using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace Neo.Orleans.Services
{
    /// <summary>
    /// WebSocket-based implementation of ITransportService for Orleans grains.
    /// Uses a 4-byte length prefix for message framing to match WsPeerConnection.
    /// </summary>
    public sealed class WsTransportService : ITransportService, IAsyncDisposable
    {
        private static readonly int MaxMessageBytes = Message.PayloadMaxSize + 16;

        private sealed class WsConnection : IAsyncDisposable
        {
            private readonly WebSocket _socket;
            private readonly SemaphoreSlim _writeLock = new(1, 1);
            private bool _disposed;

            public WsConnection(WebSocket socket)
            {
                _socket = socket;
            }

            internal WebSocket Socket => _socket;

            public bool IsConnected => _socket.State == WebSocketState.Open;

            public async Task SendAsync(byte[] message, CancellationToken cancellationToken)
            {
                await _writeLock.WaitAsync(cancellationToken);
                try
                {
                    var buffer = ArrayPool<byte>.Shared.Rent(4 + message.Length);
                    try
                    {
                        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(0, 4), message.Length);
                        message.CopyTo(buffer.AsSpan(4));
                        await _socket.SendAsync(
                            new ArraySegment<byte>(buffer, 0, 4 + message.Length),
                            WebSocketMessageType.Binary,
                            endOfMessage: true,
                            cancellationToken);
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

            public async ValueTask DisposeAsync()
            {
                if (_disposed)
                    return;

                _disposed = true;
                _writeLock.Dispose();

                try
                {
                    if (_socket.State == WebSocketState.Open || _socket.State == WebSocketState.CloseReceived)
                        await _socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Disconnecting", CancellationToken.None);
                }
                catch
                {
                    // Ignore close errors
                }
                _socket.Dispose();
            }
        }

        private readonly ConcurrentDictionary<string, WsConnection> _connections = new(StringComparer.OrdinalIgnoreCase);
        private readonly TimeSpan _connectTimeout;
        private readonly TimeSpan _sendTimeout;
        private bool _disposed;

        public WsTransportService(TimeSpan? connectTimeout = null, TimeSpan? sendTimeout = null)
        {
            _connectTimeout = connectTimeout ?? TimeSpan.FromSeconds(10);
            _sendTimeout = sendTimeout ?? TimeSpan.FromSeconds(5);
        }

        public async Task<bool> SendAsync(string address, int port, byte[] message, CancellationToken cancellationToken = default)
        {
            if (_disposed || message.Length == 0)
                return false;
            if (message.Length > MaxMessageBytes)
                return false;

            var key = TcpTransportService.FormatConnectionKey(address, port);

            WsConnection? connection = null;
            try
            {
                if (!_connections.TryGetValue(key, out connection) || !connection.IsConnected)
                {
                    if (!await ConnectAsync(address, port, cancellationToken))
                        return false;

                    _connections.TryGetValue(key, out connection);
                }

                if (connection == null || !connection.IsConnected)
                    return false;

                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(_sendTimeout);

                await connection.SendAsync(message, cts.Token);
                return true;
            }
            catch (OperationCanceledException)
            {
                if (connection != null && !cancellationToken.IsCancellationRequested)
                    RemoveConnection(key, dispose: true, expectedSocket: connection.Socket);
                return false;
            }
            catch (WebSocketException)
            {
                if (connection != null)
                    RemoveConnection(key, dispose: true, expectedSocket: connection.Socket);
                return false;
            }
            catch
            {
                if (connection != null)
                    RemoveConnection(key, dispose: true, expectedSocket: connection.Socket);
                return false;
            }
        }

        public async Task<bool> ConnectAsync(string address, int port, CancellationToken cancellationToken = default)
        {
            if (_disposed)
                return false;

            var key = TcpTransportService.FormatConnectionKey(address, port);

            if (_connections.TryGetValue(key, out var existing) && existing.IsConnected)
                return true;

            RemoveConnection(key, dispose: true);

            ClientWebSocket? ws = null;
            try
            {
                ws = new ClientWebSocket();
                var host = address;
                if (IPAddress.TryParse(address, out var ip) && ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
                    host = $"[{ip}]";
                var uri = new Uri($"ws://{host}:{port}/");

                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(_connectTimeout);

                await ws.ConnectAsync(uri, cts.Token);

                if (ws.State == WebSocketState.Open)
                {
                    _connections[key] = new WsConnection(ws);
                    ws = null;
                    return true;
                }

                return false;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
            catch (WebSocketException)
            {
                return false;
            }
            catch
            {
                return false;
            }
            finally
            {
                ws?.Dispose();
            }
        }

        public async Task DisconnectAsync(string address, int port, CancellationToken cancellationToken = default)
        {
            var key = TcpTransportService.FormatConnectionKey(address, port);
            if (_connections.TryRemove(key, out var connection))
            {
                await connection.DisposeAsync();
            }
        }

        public bool IsConnected(string address, int port)
        {
            var key = TcpTransportService.FormatConnectionKey(address, port);
            return _connections.TryGetValue(key, out var connection) && connection.IsConnected;
        }

        public async ValueTask DisposeAsync()
        {
            if (_disposed)
                return;

            _disposed = true;

            foreach (var connection in _connections.Values)
            {
                await connection.DisposeAsync();
            }

            _connections.Clear();
        }

        internal string RegisterInboundConnection(IPEndPoint remoteEndPoint, WebSocket socket)
        {
            if (_disposed)
            {
                socket.Dispose();
                throw new ObjectDisposedException(nameof(WsTransportService));
            }

            var key = TcpTransportService.FormatConnectionKey(remoteEndPoint.Address.ToString(), remoteEndPoint.Port);
            RemoveConnection(key, dispose: true);
            _connections[key] = new WsConnection(socket);
            return key;
        }

        internal void UnregisterConnection(string key, WebSocket? expectedSocket = null) =>
            RemoveConnection(key, dispose: true, expectedSocket: expectedSocket);

        internal bool IsSuperseded(string key, WebSocket? expectedSocket)
        {
            if (expectedSocket == null)
                return false;

            if (_connections.TryGetValue(key, out var current))
                return !ReferenceEquals(current.Socket, expectedSocket);

            return false;
        }

        internal async Task DisconnectAllAsync()
        {
            foreach (var key in _connections.Keys.ToArray())
            {
                if (_connections.TryRemove(key, out var connection))
                    await connection.DisposeAsync();
            }
        }

        private void RemoveConnection(string key, bool dispose, WebSocket? expectedSocket = null)
        {
            if (_connections.TryGetValue(key, out var current) &&
                expectedSocket != null &&
                !ReferenceEquals(current.Socket, expectedSocket))
            {
                return;
            }

            if (_connections.TryRemove(key, out var connection) && dispose)
            {
                _ = connection.DisposeAsync();
            }
        }
    }
}
