// Copyright (C) 2015-2025 The Neo Project.
//
// QuicTransport.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using System;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Quic;
using System.Net.Security;
using System.Runtime.Versioning;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;

namespace Neo.Network.P2P.Transport
{
    /// <summary>
    /// QUIC transport implementation for high-performance P2P communication.
    /// Provides 0-RTT connection establishment and multiplexed streams.
    /// </summary>
    /// <remarks>
    /// QUIC is only supported on Windows 11+, Linux with libmsquic, and macOS 14+.
    /// Use <see cref="IsSupported"/> to check availability before using this class.
    /// </remarks>
    [SupportedOSPlatform("linux")]
    [SupportedOSPlatform("windows")]
    [SupportedOSPlatform("osx")]
    public sealed class QuicTransport : IAsyncDisposable
    {
        private readonly QuicTransportOptions _options;
        private QuicListener? _listener;
        private readonly ConcurrentDictionary<EndPoint, QuicPeerConnection> _connections = new();
        private readonly CancellationTokenSource _cts = new();
        private Task? _acceptTask;

        /// <summary>
        /// Event raised when a new peer connects.
        /// </summary>
        public event Func<QuicPeerConnection, Task>? OnPeerConnected;

        /// <summary>
        /// Event raised when a peer disconnects.
        /// </summary>
        public event Action<EndPoint>? OnPeerDisconnected;

        /// <summary>
        /// Gets the number of active connections.
        /// </summary>
        public int ConnectionCount => _connections.Count;

        /// <summary>
        /// Gets whether QUIC is supported on this platform.
        /// </summary>
        [SupportedOSPlatformGuard("linux")]
        [SupportedOSPlatformGuard("windows")]
        [SupportedOSPlatformGuard("osx")]
        public static bool IsSupported => QuicListener.IsSupported;

        public QuicTransport(QuicTransportOptions options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
        }

        /// <summary>
        /// Starts the QUIC listener on the specified endpoint.
        /// </summary>
        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            if (!IsSupported)
                throw new PlatformNotSupportedException("QUIC is not supported on this platform");

            var listenerOptions = new QuicListenerOptions
            {
                ListenEndPoint = _options.ListenEndPoint,
                ApplicationProtocols = [new SslApplicationProtocol(_options.ApplicationProtocol)],
                ConnectionOptionsCallback = (_, _, _) => ValueTask.FromResult(CreateServerConnectionOptions())
            };

            _listener = await QuicListener.ListenAsync(listenerOptions, cancellationToken);
            _acceptTask = AcceptConnectionsAsync(_cts.Token);
        }

        /// <summary>
        /// Connects to a remote peer using QUIC.
        /// </summary>
        public async Task<QuicPeerConnection> ConnectAsync(IPEndPoint remoteEndPoint, CancellationToken cancellationToken = default)
        {
            if (!IsSupported)
                throw new PlatformNotSupportedException("QUIC is not supported on this platform");

            var connectionOptions = new QuicClientConnectionOptions
            {
                RemoteEndPoint = remoteEndPoint,
                DefaultStreamErrorCode = 0,
                DefaultCloseErrorCode = 0,
                ClientAuthenticationOptions = new SslClientAuthenticationOptions
                {
                    ApplicationProtocols = [new SslApplicationProtocol(_options.ApplicationProtocol)],
                    RemoteCertificateValidationCallback = _options.RemoteCertificateValidationCallback
                        ?? ((_, _, _, _) => true) // Allow self-signed certs by default for P2P
                }
            };

            var connection = await QuicConnection.ConnectAsync(connectionOptions, cancellationToken);
            var peerConnection = new QuicPeerConnection(connection, isIncoming: false);

            _connections.TryAdd(remoteEndPoint, peerConnection);
            peerConnection.OnDisconnected += () => HandleDisconnection(remoteEndPoint);

            return peerConnection;
        }

        /// <summary>
        /// Stops the QUIC transport and closes all connections.
        /// </summary>
        public async Task StopAsync()
        {
            _cts.Cancel();

            if (_acceptTask != null)
            {
                try { await _acceptTask; }
                catch (OperationCanceledException) { }
            }

            // Close all connections
            foreach (var (endpoint, connection) in _connections)
            {
                await connection.DisposeAsync();
                _connections.TryRemove(endpoint, out _);
            }

            if (_listener != null)
            {
                await _listener.DisposeAsync();
                _listener = null;
            }
        }

        public async ValueTask DisposeAsync()
        {
            await StopAsync();
            _cts.Dispose();
        }

        private async Task AcceptConnectionsAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested && _listener != null)
            {
                try
                {
                    var connection = await _listener.AcceptConnectionAsync(cancellationToken);
                    var peerConnection = new QuicPeerConnection(connection, isIncoming: true);

                    var remoteEndPoint = connection.RemoteEndPoint;
                    _connections.TryAdd(remoteEndPoint, peerConnection);
                    peerConnection.OnDisconnected += () => HandleDisconnection(remoteEndPoint);

                    // Notify listeners
                    if (OnPeerConnected != null)
                        _ = OnPeerConnected(peerConnection);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (QuicException ex)
                {
                    // Log and continue accepting
                    Console.WriteLine($"QUIC accept error: {ex.Message}");
                }
            }
        }

        private void HandleDisconnection(EndPoint remoteEndPoint)
        {
            _connections.TryRemove(remoteEndPoint, out _);
            OnPeerDisconnected?.Invoke(remoteEndPoint);
        }

        private QuicServerConnectionOptions CreateServerConnectionOptions()
        {
            return new QuicServerConnectionOptions
            {
                DefaultStreamErrorCode = 0,
                DefaultCloseErrorCode = 0,
                ServerAuthenticationOptions = new SslServerAuthenticationOptions
                {
                    ApplicationProtocols = [new SslApplicationProtocol(_options.ApplicationProtocol)],
                    ServerCertificate = _options.Certificate,
                    ClientCertificateRequired = false
                }
            };
        }
    }

    /// <summary>
    /// Configuration options for QUIC transport.
    /// </summary>
    public class QuicTransportOptions
    {
        /// <summary>
        /// The endpoint to listen on.
        /// </summary>
        public IPEndPoint ListenEndPoint { get; set; } = new IPEndPoint(IPAddress.Any, 10334);

        /// <summary>
        /// The application protocol identifier (ALPN).
        /// </summary>
        public string ApplicationProtocol { get; set; } = "neo-p2p";

        /// <summary>
        /// The server certificate for TLS.
        /// </summary>
        public X509Certificate2? Certificate { get; set; }

        /// <summary>
        /// Custom certificate validation callback.
        /// </summary>
        public RemoteCertificateValidationCallback? RemoteCertificateValidationCallback { get; set; }

        /// <summary>
        /// Maximum number of concurrent connections.
        /// </summary>
        public int MaxConnections { get; set; } = 100;

        /// <summary>
        /// Connection idle timeout.
        /// </summary>
        public TimeSpan IdleTimeout { get; set; } = TimeSpan.FromMinutes(2);
    }
}
