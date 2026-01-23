// Copyright (C) 2015-2025 The Neo Project.
//
// TcpTransportService.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Microsoft.Extensions.Logging;
using Neo.Network.P2P;
using Neo.Orleans.Interfaces;
using Orleans;
using System;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Neo.Orleans.Services
{
    /// <summary>
    /// TCP-based implementation of ITransportService for Orleans grains.
    /// </summary>
    public sealed class TcpTransportService : ITransportService, IAsyncDisposable
    {
        private static readonly int MaxMessageBytes = Message.PayloadMaxSize + 16;

        private sealed class TcpConnection : IAsyncDisposable
        {
            private readonly TcpClient _client;
            private readonly SemaphoreSlim _writeLock = new(1, 1);
            private bool _disposed;

            public TcpConnection(TcpClient client)
            {
                _client = client;
            }

            internal TcpClient Client => _client;

            public bool IsConnected => _client.Connected;
            public EndPoint? RemoteEndPoint => _client.Client.RemoteEndPoint;

            public NetworkStream GetStream() => _client.GetStream();

            public async Task SendAsync(byte[] message, CancellationToken cancellationToken)
            {
                await _writeLock.WaitAsync(cancellationToken);
                try
                {
                    var stream = GetStream();
                    await stream.WriteAsync(message, cancellationToken);
                    await stream.FlushAsync(cancellationToken);
                }
                finally
                {
                    _writeLock.Release();
                }
            }

            public ValueTask DisposeAsync()
            {
                if (_disposed)
                    return ValueTask.CompletedTask;

                _disposed = true;
                _client.Dispose();
                _writeLock.Dispose();
                return ValueTask.CompletedTask;
            }
        }

        private sealed class ReceiveLoopState
        {
            private int _disposed;

            public ReceiveLoopState(TcpConnection connection)
            {
                Connection = connection;
                TokenSource = new CancellationTokenSource();
            }

            public TcpConnection Connection { get; }
            public CancellationTokenSource TokenSource { get; }
            public Task Task { get; set; } = Task.CompletedTask;

            public void Cancel()
            {
                if (Interlocked.Exchange(ref _disposed, 1) != 0)
                    return;

                try
                {
                    TokenSource.Cancel();
                }
                catch
                {
                    // Ignore cancellation races.
                }
                TokenSource.Dispose();
            }
        }

        private readonly ConcurrentDictionary<string, TcpConnection> _connections = new(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<string, ReceiveLoopState> _receiveLoops = new(StringComparer.OrdinalIgnoreCase);
        private readonly TimeSpan _connectTimeout;
        private readonly TimeSpan _sendTimeout;
        private readonly IGrainFactory? _grainFactory;
        private readonly ILogger<TcpTransportService>? _logger;
        private bool _disposed;

        public TcpTransportService(
            TimeSpan? connectTimeout = null,
            TimeSpan? sendTimeout = null,
            IGrainFactory? grainFactory = null,
            ILogger<TcpTransportService>? logger = null)
        {
            _connectTimeout = connectTimeout ?? TimeSpan.FromSeconds(10);
            _sendTimeout = sendTimeout ?? TimeSpan.FromSeconds(5);
            _grainFactory = grainFactory;
            _logger = logger;
        }

        public async Task<bool> SendAsync(string address, int port, byte[] message, CancellationToken cancellationToken = default)
        {
            if (_disposed || message.Length == 0)
                return false;
            if (message.Length > MaxMessageBytes)
                return false;

            var key = FormatConnectionKey(address, port);

            TcpConnection? connection = null;
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
                    RemoveConnection(key, expected: connection);
                return false;
            }
            catch (SocketException)
            {
                if (connection != null)
                    RemoveConnection(key, expected: connection);
                return false;
            }
            catch
            {
                if (connection != null)
                    RemoveConnection(key, expected: connection);
                return false;
            }
        }

        public async Task<bool> ConnectAsync(string address, int port, CancellationToken cancellationToken = default)
        {
            if (_disposed)
                return false;

            var key = FormatConnectionKey(address, port);

            if (_connections.TryGetValue(key, out var existing) && existing.IsConnected)
                return true;

            RemoveConnection(key);

            TcpClient? client = null;
            try
            {
                client = new TcpClient
                {
                    NoDelay = true
                };

                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(_connectTimeout);

                await client.ConnectAsync(address, port, cts.Token);

                if (client.Connected)
                {
                    var connection = new TcpConnection(client);
                    _connections[key] = connection;
                    StartReceiveLoop(key, connection);
                    client = null;
                    return true;
                }

                return false;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
            catch (SocketException)
            {
                return false;
            }
            catch
            {
                return false;
            }
            finally
            {
                client?.Dispose();
            }
        }

        public async Task DisconnectAsync(string address, int port, CancellationToken cancellationToken = default)
        {
            var key = FormatConnectionKey(address, port);
            if (_connections.TryRemove(key, out var connection))
            {
                StopReceiveLoop(key, connection);
                await connection.DisposeAsync();
            }
        }

        public bool IsConnected(string address, int port)
        {
            var key = FormatConnectionKey(address, port);
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

            var receiveStates = _receiveLoops.Values.ToArray();
            foreach (var state in receiveStates)
                state.Cancel();
            _receiveLoops.Clear();

            if (receiveStates.Length > 0)
            {
                try
                {
                    await Task.WhenAll(receiveStates.Select(state => state.Task));
                }
                catch
                {
                    // Ignore receive loop failures during disposal.
                }
            }
        }

        internal string RegisterInboundConnection(IPEndPoint remoteEndPoint, TcpClient client)
        {
            if (_disposed)
            {
                client.Dispose();
                throw new ObjectDisposedException(nameof(TcpTransportService));
            }

            client.NoDelay = true;
            var key = FormatConnectionKey(remoteEndPoint.Address.ToString(), remoteEndPoint.Port);
            RemoveConnection(key);
            _connections[key] = new TcpConnection(client);
            return key;
        }

        internal void UnregisterConnection(string key, TcpClient? expectedClient = null) =>
            RemoveConnection(key, expectedClient: expectedClient);

        internal bool IsSuperseded(string key, TcpClient? expectedClient)
        {
            if (expectedClient == null)
                return false;

            if (_connections.TryGetValue(key, out var current))
                return !ReferenceEquals(current.Client, expectedClient);

            return false;
        }

        internal async Task DisconnectAllAsync()
        {
            foreach (var key in _connections.Keys.ToArray())
            {
                if (_connections.TryRemove(key, out var connection))
                {
                    StopReceiveLoop(key, connection);
                    await connection.DisposeAsync();
                }
            }
        }

        internal static string FormatConnectionKey(string address, int port)
        {
            if (IPAddress.TryParse(address, out var ip))
                return new IPEndPoint(ip, port).ToString();

            return $"{address}:{port}";
        }

        private bool RemoveConnection(string key, TcpConnection? expected = null, TcpClient? expectedClient = null)
        {
            if (_connections.TryGetValue(key, out var current))
            {
                if (expected != null && !ReferenceEquals(current, expected))
                    return false;
                if (expectedClient != null && !ReferenceEquals(current.Client, expectedClient))
                    return false;
            }

            if (_connections.TryRemove(key, out var connection))
            {
                StopReceiveLoop(key, connection);
                _ = connection.DisposeAsync();
                return true;
            }

            return false;
        }

        private void StartReceiveLoop(string key, TcpConnection connection)
        {
            if (_grainFactory == null)
                return;

            while (true)
            {
                if (_receiveLoops.TryGetValue(key, out var existing))
                {
                    if (ReferenceEquals(existing.Connection, connection))
                        return;

                    var replacement = new ReceiveLoopState(connection);
                    replacement.Task = Task.Run(
                        () => ReceiveLoopAsync(key, replacement, replacement.TokenSource.Token),
                        CancellationToken.None);

                    if (_receiveLoops.TryUpdate(key, replacement, existing))
                    {
                        existing.Cancel();
                        return;
                    }

                    replacement.Cancel();
                    continue;
                }

                var state = new ReceiveLoopState(connection);
                state.Task = Task.Run(() => ReceiveLoopAsync(key, state, state.TokenSource.Token), CancellationToken.None);

                if (_receiveLoops.TryAdd(key, state))
                    return;

                state.Cancel();
            }
        }

        private void StopReceiveLoop(string key, TcpConnection? connection)
        {
            if (_receiveLoops.TryGetValue(key, out var state))
            {
                if (connection != null && !ReferenceEquals(state.Connection, connection))
                    return;

                if (_receiveLoops.TryRemove(key, out var removed))
                    removed.Cancel();
            }
        }

        private async Task ReceiveLoopAsync(string key, ReceiveLoopState state, CancellationToken cancellationToken)
        {
            if (_grainFactory == null)
                return;

            var grain = _grainFactory.GetGrain<IRemoteNodeGrain>(key);
            NetworkStream? stream = null;
            var readBuffer = new byte[64 * 1024];
            var pending = Array.Empty<byte>();
            var pendingCount = 0;
            var remoteEndPoint = state.Connection.RemoteEndPoint;

            try
            {
                stream = state.Connection.GetStream();
                while (!cancellationToken.IsCancellationRequested)
                {
                    int bytesRead;
                    try
                    {
                        bytesRead = await stream.ReadAsync(readBuffer, cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogDebug(ex, "P2P TCP outbound read failed for {Remote}", remoteEndPoint);
                        break;
                    }

                    if (bytesRead <= 0)
                        break;

                    pending = EnsureCapacity(pending, pendingCount + bytesRead);
                    Buffer.BlockCopy(readBuffer, 0, pending, pendingCount, bytesRead);
                    pendingCount += bytesRead;

                    var consumed = 0;
                    while (true)
                    {
                        var span = new ReadOnlySpan<byte>(pending, consumed, pendingCount - consumed);
                        int messageLength;
                        try
                        {
                            messageLength = TryGetMessageLength(span);
                        }
                        catch (FormatException ex)
                        {
                            _logger?.LogWarning(ex, "P2P TCP outbound invalid message length from {Remote}", remoteEndPoint);
                            return;
                        }

                        if (messageLength == 0)
                            break;

                        var messageBytes = new byte[messageLength];
                        Buffer.BlockCopy(pending, consumed, messageBytes, 0, messageLength);
                        consumed += messageLength;

                        try
                        {
                            await grain.HandleMessageAsync(messageBytes);
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogDebug(ex, "P2P TCP outbound message handling failed for {Remote}", remoteEndPoint);
                        }
                    }

                    if (consumed > 0)
                    {
                        Buffer.BlockCopy(pending, consumed, pending, 0, pendingCount - consumed);
                        pendingCount -= consumed;
                    }

                    if (pendingCount > MaxMessageBytes)
                    {
                        _logger?.LogWarning("P2P TCP outbound pending buffer exceeded max size for {Remote}", remoteEndPoint);
                        break;
                    }
                }
            }
            finally
            {
                if (_receiveLoops.TryGetValue(key, out var current) && ReferenceEquals(current, state))
                {
                    _receiveLoops.TryRemove(key, out _);
                    state.Cancel();
                }

                if (!cancellationToken.IsCancellationRequested)
                {
                    var superseded = IsSuperseded(key, state.Connection);
                    var removed = RemoveConnection(key, state.Connection);
                    if (!removed)
                        await state.Connection.DisposeAsync();

                    if (!superseded)
                    {
                        try
                        {
                            await grain.DisconnectAsync();
                        }
                        catch
                        {
                            // Ignore grain disconnect errors on shutdown.
                        }
                    }
                }
            }
        }

        private bool IsSuperseded(string key, TcpConnection expectedConnection)
        {
            if (_connections.TryGetValue(key, out var current))
                return !ReferenceEquals(current, expectedConnection);

            return false;
        }

        private static int TryGetMessageLength(ReadOnlySpan<byte> data)
        {
            if (data.Length < 3)
                return 0;

            ulong length = data[2];
            var headerSize = 3;

            if (length == 0xFD)
            {
                if (data.Length < 5)
                    return 0;
                length = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(3, 2));
                headerSize = 5;
            }
            else if (length == 0xFE)
            {
                if (data.Length < 7)
                    return 0;
                length = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(3, 4));
                headerSize = 7;
            }
            else if (length == 0xFF)
            {
                if (data.Length < 11)
                    return 0;
                length = BinaryPrimitives.ReadUInt64LittleEndian(data.Slice(3, 8));
                headerSize = 11;
            }

            if (length > Message.PayloadMaxSize)
                throw new FormatException($"Payload length {length} exceeds maximum {Message.PayloadMaxSize}");

            if (data.Length < headerSize + (int)length)
                return 0;

            return headerSize + (int)length;
        }

        private static byte[] EnsureCapacity(byte[] buffer, int required)
        {
            if (buffer.Length >= required)
                return buffer;

            var nextSize = buffer.Length == 0 ? 4096 : buffer.Length * 2;
            var newSize = Math.Max(nextSize, required);
            var resized = new byte[newSize];
            if (buffer.Length > 0)
                Buffer.BlockCopy(buffer, 0, resized, 0, buffer.Length);
            return resized;
        }
    }
}
