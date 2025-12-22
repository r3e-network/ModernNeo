// Copyright (C) 2015-2025 The Neo Project.
//
// ProtocolNegotiator.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Neo.Network.P2P.Transport
{
    /// <summary>
    /// Handles protocol negotiation between TCP and QUIC transports.
    /// Supports automatic fallback from QUIC to TCP when QUIC is unavailable.
    /// </summary>
    public class ProtocolNegotiator
    {
        private readonly QuicTransport? _quicTransport;
        private readonly WsTransport? _wsTransport;
        private readonly TimeSpan _quicTimeout;
        private readonly ILogger<ProtocolNegotiator> _logger;

        /// <summary>
        /// Gets whether QUIC transport is available.
        /// </summary>
        public bool QuicAvailable => _quicTransport != null && QuicTransportIsSupported();

        /// <summary>
        /// Initializes a new protocol negotiator.
        /// </summary>
        /// <param name="quicTransport">Optional QUIC transport instance.</param>
        /// <param name="wsTransport">Optional WebSocket transport instance.</param>
        /// <param name="quicTimeout">Timeout for QUIC connection attempts before falling back to TCP.</param>
        /// <param name="logger">Optional logger for diagnostics.</param>
        public ProtocolNegotiator(
            QuicTransport? quicTransport = null,
            WsTransport? wsTransport = null,
            TimeSpan? quicTimeout = null,
            ILogger<ProtocolNegotiator>? logger = null)
        {
            _quicTransport = quicTransport;
            _wsTransport = wsTransport;
            _quicTimeout = quicTimeout ?? TimeSpan.FromSeconds(5);
            _logger = logger ?? NullLogger<ProtocolNegotiator>.Instance;
        }

        /// <summary>
        /// Attempts to connect to a peer, preferring QUIC but falling back to TCP.
        /// </summary>
        /// <param name="tcpEndPoint">The TCP endpoint to connect to.</param>
        /// <param name="quicEndPoint">The QUIC endpoint to connect to (usually same IP, different port).</param>
        /// <param name="wsUri">Optional WebSocket URI to connect with.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Connection result indicating which protocol was used.</returns>
        public async Task<ConnectionResult> ConnectAsync(
            IPEndPoint tcpEndPoint,
            IPEndPoint? quicEndPoint = null,
            Uri? wsUri = null,
            CancellationToken cancellationToken = default)
        {
            // Try QUIC first if available (with platform guard)
            if (QuicAvailable && quicEndPoint != null && QuicTransportIsSupported())
            {
                try
                {
                    using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    cts.CancelAfter(_quicTimeout);

                    var quicConnection = await ConnectQuicInternalAsync(quicEndPoint, cts.Token);
                    if (quicConnection != null)
                    {
                        return new ConnectionResult
                        {
                            Protocol = TransportProtocol.Quic,
                            QuicConnection = quicConnection,
                            RemoteEndPoint = quicEndPoint
                        };
                    }
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    _logger.LogDebug("QUIC connection to {EndPoint} timed out, falling back to TCP", quicEndPoint);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "QUIC connection to {EndPoint} failed: {Message}, falling back to TCP", quicEndPoint, ex.Message);
                }
            }

            // Try WebSocket if provided
            if (_wsTransport != null && wsUri != null)
            {
                try
                {
                    var ws = await _wsTransport.ConnectAsync(wsUri, cancellationToken);
                    return new ConnectionResult
                    {
                        Protocol = TransportProtocol.WebSocket,
                        WsConnection = ws,
                        RemoteEndPoint = tcpEndPoint
                    };
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "WebSocket connection to {Uri} failed: {Message}, falling back to TCP", wsUri, ex.Message);
                }
            }

            // Fall back to TCP (handled by existing Akka.IO infrastructure)
            return new ConnectionResult
            {
                Protocol = TransportProtocol.Tcp,
                QuicConnection = null,
                WsConnection = null,
                RemoteEndPoint = tcpEndPoint
            };
        }

        /// <summary>
        /// Determines the best protocol to use for a given peer based on capabilities.
        /// </summary>
        /// <param name="peerCapabilities">The peer's advertised capabilities.</param>
        /// <returns>The recommended transport protocol.</returns>
        public TransportProtocol RecommendProtocol(PeerCapabilities peerCapabilities)
        {
            if (QuicAvailable && peerCapabilities.HasFlag(PeerCapabilities.Quic))
                return TransportProtocol.Quic;
            if (peerCapabilities.HasFlag(PeerCapabilities.WebSocket))
                return TransportProtocol.WebSocket;
            return TransportProtocol.Tcp;
        }

        [SupportedOSPlatformGuard("linux")]
        [SupportedOSPlatformGuard("windows")]
        [SupportedOSPlatformGuard("osx")]
        private static bool QuicTransportIsSupported()
        {
            return QuicTransport.IsSupported;
        }

        [SupportedOSPlatform("linux")]
        [SupportedOSPlatform("windows")]
        [SupportedOSPlatform("osx")]
        private async Task<QuicPeerConnection?> ConnectQuicInternalAsync(IPEndPoint endPoint, CancellationToken cancellationToken)
        {
            if (_quicTransport == null) return null;
            return await _quicTransport.ConnectAsync(endPoint, cancellationToken);
        }
    }

    /// <summary>
    /// Result of a connection attempt.
    /// </summary>
    public class ConnectionResult
    {
        /// <summary>
        /// The protocol that was successfully used.
        /// </summary>
        public required TransportProtocol Protocol { get; init; }

        /// <summary>
        /// The QUIC connection if QUIC was used, null otherwise.
        /// </summary>
        public QuicPeerConnection? QuicConnection { get; init; }
        public WsPeerConnection? WsConnection { get; init; }

        /// <summary>
        /// The remote endpoint that was connected to.
        /// </summary>
        public required IPEndPoint RemoteEndPoint { get; init; }

        /// <summary>
        /// Gets whether the connection uses QUIC.
        /// </summary>
        public bool IsQuic => Protocol == TransportProtocol.Quic;
        public bool IsWebSocket => Protocol == TransportProtocol.WebSocket;
    }

    /// <summary>
    /// Transport protocol types.
    /// </summary>
    public enum TransportProtocol
    {
        /// <summary>
        /// Traditional TCP transport.
        /// </summary>
        Tcp,

        /// <summary>
        /// QUIC transport with built-in TLS 1.3.
        /// </summary>
        Quic,
        WebSocket
    }

    /// <summary>
    /// Peer capability flags for protocol negotiation.
    /// </summary>
    [Flags]
    public enum PeerCapabilities
    {
        /// <summary>
        /// No special capabilities.
        /// </summary>
        None = 0,

        /// <summary>
        /// Peer supports TCP transport.
        /// </summary>
        Tcp = 1,

        /// <summary>
        /// Peer supports QUIC transport.
        /// </summary>
        Quic = 2,

        /// <summary>
        /// Peer supports message compression.
        /// </summary>
        Compression = 4,

        /// <summary>
        /// Peer is a full node with complete blockchain data.
        /// </summary>
        FullNode = 8,

        /// <summary>
        /// Peer is an archival node with historical data.
        /// </summary>
        ArchivalNode = 16,
        /// <summary>
        /// Peer supports WebSocket transport.
        /// </summary>
        WebSocket = 32
    }
}
