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

using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace Neo.Orleans.Services
{
    /// <summary>
    /// WebSocket-based implementation of ITransportService for Orleans grains.
    /// Provides connection pooling and automatic reconnection.
    /// </summary>
    public sealed class WsTransportService : ITransportService, IAsyncDisposable
    {
        private readonly ConcurrentDictionary<string, ClientWebSocket> _connections = new();
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
            if (_disposed)
                return false;

            var key = GetConnectionKey(address, port);

            try
            {
                // Get or create connection
                if (!_connections.TryGetValue(key, out var ws) || ws.State != WebSocketState.Open)
                {
                    if (!await ConnectAsync(address, port, cancellationToken))
                        return false;

                    _connections.TryGetValue(key, out ws);
                }

                if (ws == null || ws.State != WebSocketState.Open)
                    return false;

                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(_sendTimeout);

                await ws.SendAsync(
                    new ArraySegment<byte>(message),
                    WebSocketMessageType.Binary,
                    endOfMessage: true,
                    cts.Token);

                return true;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
            catch (WebSocketException)
            {
                // Connection failed, remove from pool
                RemoveConnection(key);
                return false;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> ConnectAsync(string address, int port, CancellationToken cancellationToken = default)
        {
            if (_disposed)
                return false;

            var key = GetConnectionKey(address, port);

            // Check if already connected
            if (_connections.TryGetValue(key, out var existing) && existing.State == WebSocketState.Open)
                return true;

            // Remove stale connection
            RemoveConnection(key);

            try
            {
                var ws = new ClientWebSocket();
                var uri = new Uri($"ws://{address}:{port}");

                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(_connectTimeout);

                await ws.ConnectAsync(uri, cts.Token);

                if (ws.State == WebSocketState.Open)
                {
                    _connections[key] = ws;
                    return true;
                }

                ws.Dispose();
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
        }

        public async Task DisconnectAsync(string address, int port, CancellationToken cancellationToken = default)
        {
            var key = GetConnectionKey(address, port);

            if (_connections.TryRemove(key, out var ws))
            {
                try
                {
                    if (ws.State == WebSocketState.Open)
                    {
                        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                        cts.CancelAfter(TimeSpan.FromSeconds(2));

                        await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Disconnecting", cts.Token);
                    }
                }
                catch
                {
                    // Ignore close errors
                }
                finally
                {
                    ws.Dispose();
                }
            }
        }

        public bool IsConnected(string address, int port)
        {
            var key = GetConnectionKey(address, port);
            return _connections.TryGetValue(key, out var ws) && ws.State == WebSocketState.Open;
        }

        public async ValueTask DisposeAsync()
        {
            if (_disposed)
                return;

            _disposed = true;

            foreach (var kvp in _connections)
            {
                try
                {
                    if (kvp.Value.State == WebSocketState.Open)
                    {
                        await kvp.Value.CloseAsync(
                            WebSocketCloseStatus.NormalClosure,
                            "Service disposing",
                            CancellationToken.None);
                    }
                }
                catch
                {
                    // Ignore
                }
                finally
                {
                    kvp.Value.Dispose();
                }
            }

            _connections.Clear();
        }

        private static string GetConnectionKey(string address, int port) => $"{address}:{port}";

        private void RemoveConnection(string key)
        {
            if (_connections.TryRemove(key, out var ws))
            {
                ws.Dispose();
            }
        }
    }
}
