// Copyright (C) 2015-2025 The Neo Project.
//
// QuicTransportService.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Network.P2P.Transport;
using Neo.Orleans.Utilities;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace Neo.Orleans.Services
{
    /// <summary>
    /// QUIC-based transport implementation for Orleans grains.
    /// Primarily used for inbound QUIC connections.
    /// </summary>
    public sealed class QuicTransportService : ITransportService, IAsyncDisposable
    {

        private sealed class QuicConnection : IAsyncDisposable
        {
            private readonly IQuicConnection _connection;
            private readonly SemaphoreSlim _writeLock = new(1, 1);
            private readonly Action? _onDisconnected;
            private bool _disposed;

            public QuicConnection(IQuicConnection connection, Action? onDisconnected)
            {
                _connection = connection ?? throw new ArgumentNullException(nameof(connection));
                _onDisconnected = onDisconnected;
                _connection.OnDisconnected += HandleDisconnected;
            }

            internal IQuicConnection Connection => _connection;

            public async Task SendAsync(byte[] message, CancellationToken cancellationToken)
            {
                await _writeLock.WaitAsync(cancellationToken);
                try
                {
                    await _connection.SendAsync(message, cancellationToken);
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
                _connection.OnDisconnected -= HandleDisconnected;
                _writeLock.Dispose();
                await _connection.DisposeAsync();
            }

            private void HandleDisconnected()
            {
                _onDisconnected?.Invoke();
            }
        }

        private readonly ConcurrentDictionary<string, QuicConnection> _connections = new(StringComparer.OrdinalIgnoreCase);
        private readonly TimeSpan _sendTimeout;
        private bool _disposed;

        public QuicTransportService(TimeSpan? sendTimeout = null)
        {
            _sendTimeout = sendTimeout ?? TimeSpan.FromSeconds(5);
        }

        public async Task<bool> SendAsync(string address, int port, byte[] message, CancellationToken cancellationToken = default)
        {
            if (_disposed || message.Length == 0)
                return false;
            if (message.Length > TransportConstants.MaxMessageBytes)
                return false;

            var key = TransportConstants.FormatConnectionKey(address, port);
            if (!_connections.TryGetValue(key, out var connection))
                return false;

            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(_sendTimeout);
                await connection.SendAsync(message, cts.Token);
                return true;
            }
            catch (OperationCanceledException)
            {
                if (connection != null && !cancellationToken.IsCancellationRequested)
                    RemoveConnection(key, dispose: true, expectedConnection: connection.Connection);
                return false;
            }
            catch
            {
                RemoveConnection(key, dispose: true, expectedConnection: connection?.Connection);
                return false;
            }
        }

        public Task<bool> ConnectAsync(string address, int port, CancellationToken cancellationToken = default)
        {
            var key = TransportConstants.FormatConnectionKey(address, port);
            return Task.FromResult(_connections.ContainsKey(key));
        }

        public async Task DisconnectAsync(string address, int port, CancellationToken cancellationToken = default)
        {
            var key = TransportConstants.FormatConnectionKey(address, port);
            if (_connections.TryRemove(key, out var connection))
            {
                await connection.DisposeAsync();
            }
        }

        public bool IsConnected(string address, int port)
        {
            var key = TransportConstants.FormatConnectionKey(address, port);
            return _connections.ContainsKey(key);
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

        internal string RegisterInboundConnection(IPEndPoint remoteEndPoint, IQuicConnection connection)
        {
            if (_disposed)
            {
                _ = connection.DisposeAsync();
                throw new ObjectDisposedException(nameof(QuicTransportService));
            }

            var key = TransportConstants.FormatConnectionKey(remoteEndPoint.Address.ToString(), remoteEndPoint.Port);
            RemoveConnection(key, dispose: true);
            _connections[key] = new QuicConnection(
                connection,
                () => RemoveConnection(key, dispose: true, expectedConnection: connection));
            return key;
        }

        internal void UnregisterConnection(string key, IQuicConnection? expectedConnection = null) =>
            RemoveConnection(key, dispose: true, expectedConnection: expectedConnection);

        internal bool IsSuperseded(string key, IQuicConnection? expectedConnection)
        {
            if (expectedConnection == null)
                return false;

            if (_connections.TryGetValue(key, out var current))
                return !ReferenceEquals(current.Connection, expectedConnection);

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

        private void RemoveConnection(string key, bool dispose, IQuicConnection? expectedConnection = null)
        {
            if (_connections.TryGetValue(key, out var current) &&
                expectedConnection != null &&
                !ReferenceEquals(current.Connection, expectedConnection))
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
