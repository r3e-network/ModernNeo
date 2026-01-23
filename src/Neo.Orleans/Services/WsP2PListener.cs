// Copyright (C) 2015-2025 The Neo Project.
//
// WsP2PListener.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Microsoft.Extensions.Logging;
using Neo.Network.P2P;
using Neo.Orleans.Hosting;
using Neo.Orleans.Interfaces;
using Orleans;
using System;
using System.Buffers;
using System.IO;
using System.Net;
using System.Net.WebSockets;
using System.Buffers.Binary;
using System.Threading;
using System.Threading.Tasks;

namespace Neo.Orleans.Services
{
    internal sealed class WsP2PListener : IP2PListener, IAsyncDisposable
    {
        private const int ReceiveBufferSize = 64 * 1024;
        private static readonly int MaxMessageBytes = Message.PayloadMaxSize + 16 + 4;

        private readonly IGrainFactory _grainFactory;
        private readonly WsTransportService _transportService;
        private readonly NeoOrleansOptions _options;
        private readonly ILogger<WsP2PListener> _logger;
        private readonly SemaphoreSlim _gate = new(1, 1);

        private HttpListener? _listener;
        private CancellationTokenSource? _cts;
        private Task? _acceptTask;
        private int _port;

        public WsP2PListener(
            IGrainFactory grainFactory,
            WsTransportService transportService,
            NeoOrleansOptions options,
            ILogger<WsP2PListener> logger)
        {
            _grainFactory = grainFactory;
            _transportService = transportService;
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _logger = logger;
        }

        public async Task StartAsync(int tcpPort, string? bindAddress, CancellationToken cancellationToken = default)
        {
            if (!_options.WsEnabled)
                return;

            if (!HttpListener.IsSupported)
            {
                _logger.LogWarning("WebSocket P2P listener is not supported on this platform.");
                _options.WsEnabled = false;
                return;
            }

            var port = _options.WsPort;
            if (port <= 0)
            {
                _logger.LogWarning("WebSocket P2P listener enabled without a valid port.");
                _options.WsEnabled = false;
                return;
            }
            if (tcpPort > 0 && port == tcpPort)
            {
                _logger.LogWarning("WebSocket P2P port {Port} conflicts with TCP listener port.", port);
                _options.WsEnabled = false;
                return;
            }

            await _gate.WaitAsync(cancellationToken);
            try
            {
                if (_listener != null)
                {
                    if (_port == port)
                        return;

                    await StopInternalAsync();
                }

                var prefix = BuildPrefix(bindAddress, port);
                _listener = new HttpListener();
                _listener.Prefixes.Add(prefix);
                try
                {
                    _listener.Start();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "P2P WebSocket listener failed to start on {Prefix}", prefix);
                    _listener.Close();
                    _listener = null;
                    _options.WsEnabled = false;
                    return;
                }

                _cts = new CancellationTokenSource();
                _acceptTask = AcceptLoopAsync(_cts.Token);
                _port = port;

                _logger.LogInformation("P2P WebSocket listener started on {Prefix}", prefix);
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
                _listener.Close();
                _listener = null;
                _acceptTask = null;
                _cts?.Dispose();
                _cts = null;
                _port = 0;
            }

            await _transportService.DisconnectAllAsync();
            _logger.LogInformation("P2P WebSocket listener stopped");
        }

        private async Task AcceptLoopAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested && _listener != null)
            {
                HttpListenerContext? context = null;
                try
                {
                    context = await _listener.GetContextAsync();
                }
                catch (HttpListenerException)
                {
                    break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (InvalidOperationException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "P2P WebSocket accept failed");
                }

                if (context == null)
                    continue;

                _ = Task.Run(() => HandleContextAsync(context, cancellationToken), cancellationToken);
            }
        }

        private async Task HandleContextAsync(HttpListenerContext context, CancellationToken cancellationToken)
        {
            if (!context.Request.IsWebSocketRequest)
            {
                context.Response.StatusCode = 400;
                context.Response.Close();
                return;
            }

            WebSocketContext? wsContext = null;
            try
            {
                wsContext = await context.AcceptWebSocketAsync(subProtocol: null);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "P2P WebSocket handshake failed");
                context.Response.StatusCode = 500;
                context.Response.Close();
                return;
            }

            var remoteEndPoint = context.Request.RemoteEndPoint as IPEndPoint;
            if (remoteEndPoint == null)
            {
                wsContext.WebSocket.Dispose();
                return;
            }

            await HandleConnectionAsync(wsContext.WebSocket, remoteEndPoint, cancellationToken);
        }

        private async Task HandleConnectionAsync(WebSocket socket, IPEndPoint remoteEndPoint, CancellationToken cancellationToken)
        {
            var key = string.Empty;
            try
            {
                key = _transportService.RegisterInboundConnection(remoteEndPoint, socket);
            }
            catch (ObjectDisposedException)
            {
                socket.Dispose();
                return;
            }

            var grain = _grainFactory.GetGrain<IRemoteNodeGrain>(key);

            var buffer = ArrayPool<byte>.Shared.Rent(ReceiveBufferSize);
            var stream = new MemoryStream();
            try
            {
                while (socket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
                {
                    WebSocketReceiveResult? result;
                    try
                    {
                        result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "P2P WebSocket receive failed for {Remote}", remoteEndPoint);
                        break;
                    }

                    if (result.MessageType == WebSocketMessageType.Close)
                        break;

                    if (result.MessageType != WebSocketMessageType.Binary)
                        continue;

                    if (result.Count > 0)
                    {
                        stream.Write(buffer, 0, result.Count);
                        if (stream.Length > MaxMessageBytes)
                        {
                            _logger.LogWarning("P2P WebSocket message exceeded max size for {Remote}", remoteEndPoint);
                            break;
                        }
                    }

                    if (!result.EndOfMessage)
                        continue;

                    var data = stream.ToArray();
                    stream.SetLength(0);

                    var payload = ExtractPayload(data);
                    if (payload.Length == 0)
                        continue;

                    try
                    {
                        await grain.HandleMessageAsync(payload);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "P2P WebSocket message handling failed for {Remote}", remoteEndPoint);
                    }
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
                stream.Dispose();

                var superseded = false;
                if (!string.IsNullOrEmpty(key))
                    superseded = _transportService.IsSuperseded(key, socket);

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
                    _transportService.UnregisterConnection(key, socket);

                socket.Dispose();
            }
        }

        private static byte[] ExtractPayload(byte[] data)
        {
            if (data.Length >= 4)
            {
                var length = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(0, 4));
                if (length == data.Length - 4)
                {
                    var payload = new byte[length];
                    Buffer.BlockCopy(data, 4, payload, 0, length);
                    return payload;
                }
            }

            return data;
        }

        private static string BuildPrefix(string? bindAddress, int port)
        {
            if (string.IsNullOrWhiteSpace(bindAddress))
                return $"http://+:{port}/";

            if (bindAddress is "0.0.0.0" or "::")
                return $"http://+:{port}/";

            if (IPAddress.TryParse(bindAddress, out var ip))
            {
                if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
                    return $"http://[{ip}]:{port}/";

                return $"http://{ip}:{port}/";
            }

            return $"http://{bindAddress}:{port}/";
        }
    }
}
