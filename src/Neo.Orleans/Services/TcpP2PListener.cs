// Copyright (C) 2015-2025 The Neo Project.
//
// TcpP2PListener.cs file belongs to the neo project and is free
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
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Neo.Orleans.Services
{
    internal interface IP2PListener
    {
        Task StartAsync(int tcpPort, string? bindAddress, CancellationToken cancellationToken = default);
        Task StopAsync(CancellationToken cancellationToken = default);
    }

    internal sealed class TcpP2PListener : IP2PListener, IAsyncDisposable
    {
        private readonly IGrainFactory _grainFactory;
        private readonly TcpTransportService _transportService;
        private readonly ILogger<TcpP2PListener> _logger;
        private readonly SemaphoreSlim _gate = new(1, 1);

        private TcpListener? _listener;
        private CancellationTokenSource? _cts;
        private Task? _acceptTask;
        private int _port;

        public TcpP2PListener(
            IGrainFactory grainFactory,
            TcpTransportService transportService,
            ILogger<TcpP2PListener> logger)
        {
            _grainFactory = grainFactory;
            _transportService = transportService;
            _logger = logger;
        }

        public async Task StartAsync(int tcpPort, string? bindAddress, CancellationToken cancellationToken = default)
        {
            if (tcpPort <= 0)
                return;

            await _gate.WaitAsync(cancellationToken);
            try
            {
                if (_listener != null)
                {
                    if (_port == tcpPort)
                        return;

                    await StopInternalAsync();
                }

                var address = ParseBindAddress(bindAddress);
                _listener = new TcpListener(address, tcpPort);
                _listener.Start();

                _cts = new CancellationTokenSource();
                _acceptTask = AcceptLoopAsync(_cts.Token);
                _port = tcpPort;

                _logger.LogInformation("P2P TCP listener started on {Address}:{Port}", address, tcpPort);
            }
            finally
            {
                _gate.Release();
            }
        }

        public async Task StopAsync(CancellationToken cancellationToken = default)
        {
            await _gate.WaitAsync(cancellationToken);
            try
            {
                await StopInternalAsync();
            }
            finally
            {
                _gate.Release();
            }
        }

        public async ValueTask DisposeAsync()
        {
            await StopAsync();
            _gate.Dispose();
        }

        private async Task StopInternalAsync()
        {
            if (_listener == null)
                return;

            try
            {
                _cts?.Cancel();
                _listener.Stop();
                if (_acceptTask != null)
                {
                    try { await _acceptTask; }
                    catch (OperationCanceledException) { }
                }
            }
            finally
            {
                _listener = null;
                _acceptTask = null;
                _cts?.Dispose();
                _cts = null;
                _port = 0;
            }

            await _transportService.DisconnectAllAsync();
            _logger.LogInformation("P2P TCP listener stopped");
        }

        private async Task AcceptLoopAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested && _listener != null)
            {
                TcpClient? client = null;
                try
                {
                    client = await _listener.AcceptTcpClientAsync(cancellationToken);
                    client.NoDelay = true;
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "P2P TCP accept failed");
                    client?.Dispose();
                    continue;
                }

                _ = Task.Run(() => HandleClientAsync(client, cancellationToken), cancellationToken);
            }
        }

        private async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
        {
            var remoteEndPoint = client.Client.RemoteEndPoint as IPEndPoint;
            if (remoteEndPoint == null)
            {
                client.Dispose();
                return;
            }

            var key = string.Empty;
            try
            {
                key = _transportService.RegisterInboundConnection(remoteEndPoint, client);
            }
            catch (ObjectDisposedException)
            {
                client.Dispose();
                return;
            }

            var grain = _grainFactory.GetGrain<IRemoteNodeGrain>(key);

            try
            {
                await ReadLoopAsync(client, grain, cancellationToken);
            }
            finally
            {
                var superseded = false;
                if (!string.IsNullOrEmpty(key))
                    superseded = _transportService.IsSuperseded(key, client);

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

                if (!string.IsNullOrEmpty(key))
                    _transportService.UnregisterConnection(key, client);

                client.Dispose();
            }
        }

        private async Task ReadLoopAsync(TcpClient client, IRemoteNodeGrain grain, CancellationToken cancellationToken)
        {
            var stream = client.GetStream();
            var readBuffer = new byte[64 * 1024];
            var pending = Array.Empty<byte>();
            var pendingCount = 0;

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
                    _logger.LogDebug(ex, "P2P TCP read failed");
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
                        _logger.LogWarning(ex, "P2P TCP invalid message length from {Remote}", client.Client.RemoteEndPoint);
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
                        _logger.LogDebug(ex, "P2P TCP message handling failed for {Remote}", client.Client.RemoteEndPoint);
                    }
                }

                if (consumed > 0)
                {
                    Buffer.BlockCopy(pending, consumed, pending, 0, pendingCount - consumed);
                    pendingCount -= consumed;
                }

                if (pendingCount > Message.PayloadMaxSize + 16)
                {
                    _logger.LogWarning("P2P TCP pending buffer exceeded max size for {Remote}", client.Client.RemoteEndPoint);
                    break;
                }
            }
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

        private static IPAddress ParseBindAddress(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return IPAddress.Any;

            return IPAddress.TryParse(value, out var parsed) ? parsed : IPAddress.Any;
        }
    }
}
