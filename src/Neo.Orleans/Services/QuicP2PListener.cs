// Copyright (C) 2015-2025 The Neo Project.
//
// QuicP2PListener.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Microsoft.Extensions.Logging;
using Neo.Network.P2P.Transport;
using Neo.Orleans.Hosting;
using Neo.Orleans.Interfaces;
using Neo.Orleans.Options;
using Orleans;
using System;
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;

namespace Neo.Orleans.Services
{
    internal sealed class QuicP2PListener : IP2PListener, IAsyncDisposable
    {
        private readonly IGrainFactory _grainFactory;
        private readonly QuicTransportService _transportService;
        private readonly OrleansOptions _options;
        private readonly ILogger<QuicP2PListener> _logger;
        private readonly SemaphoreSlim _gate = new(1, 1);

        private QuicTransport? _transport;
        private CancellationTokenSource? _cts;
        private int _port;
        private X509Certificate2? _certificate;

        public QuicP2PListener(
            IGrainFactory grainFactory,
            QuicTransportService transportService,
            OrleansOptions options,
            ILogger<QuicP2PListener> logger)
        {
            _grainFactory = grainFactory;
            _transportService = transportService;
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _logger = logger;
        }

        public async Task StartAsync(int tcpPort, string? bindAddress, CancellationToken cancellationToken = default)
        {
            if (!_options.QuicEnabled)
                return;

            if (!QuicTransport.IsSupported)
            {
                _logger.LogWarning("QUIC is enabled but not supported on this platform.");
                _options.QuicEnabled = false;
                return;
            }

            var port = _options.QuicPort > 0 ? _options.QuicPort : (tcpPort > 0 ? tcpPort + 1 : 0);
            if (port <= 0)
                return;

            var address = ParseBindAddress(bindAddress);

            await _gate.WaitAsync(cancellationToken);
            try
            {
                if (_transport != null)
                {
                    if (_port == port)
                        return;

                    await StopInternalAsync();
                }

                _certificate = LoadCertificate() ?? CreateSelfSignedCertificate();
                var options = new QuicTransportOptions
                {
                    ListenEndPoint = new IPEndPoint(address, port),
                    ApplicationProtocol = _options.QuicAlpn,
                    Certificate = _certificate
                };
                if (_options.MaxConnections > 0)
                    options.MaxConnections = _options.MaxConnections;

                _transport = new QuicTransport(options);
                _transport.OnPeerConnected += HandlePeerConnectedAsync;
                _cts = new CancellationTokenSource();
                await _transport.StartAsync(_cts.Token);
                _port = port;

                _logger.LogInformation("P2P QUIC listener started on {Address}:{Port}", address, port);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "P2P QUIC listener failed to start on {Address}:{Port}", address, port);
                if (_transport != null)
                {
                    try
                    {
                        await _transport.DisposeAsync();
                    }
                    catch
                    {
                        // Ignore transport disposal errors on startup failure.
                    }
                }
                _transport = null;
                _cts?.Dispose();
                _cts = null;
                _port = 0;
                _certificate?.Dispose();
                _certificate = null;
                _options.QuicEnabled = false;
                return;
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
            if (_transport == null)
                return;

            try
            {
                _cts?.Cancel();
                if (QuicTransport.IsSupported)
                    await _transport.DisposeAsync();
            }
            finally
            {
                _transport = null;
                _cts?.Dispose();
                _cts = null;
                _port = 0;
                _certificate?.Dispose();
                _certificate = null;
            }

            await _transportService.DisconnectAllAsync();
            _logger.LogInformation("P2P QUIC listener stopped");
        }

        private Task HandlePeerConnectedAsync(QuicPeerConnection connection)
        {
            if (!QuicTransport.IsSupported)
                return Task.CompletedTask;

            var remoteEndPoint = connection.RemoteEndPoint as IPEndPoint;
            if (remoteEndPoint == null)
                return Task.CompletedTask;

            string key;
            try
            {
                key = _transportService.RegisterInboundConnection(remoteEndPoint, connection);
            }
            catch (ObjectDisposedException)
            {
                _ = connection.DisposeAsync();
                return Task.CompletedTask;
            }

            var grain = _grainFactory.GetGrain<IRemoteNodeGrain>(key);

            connection.OnMessageReceived += data => HandleMessageAsync(grain, data, remoteEndPoint);
            connection.OnDisconnected += () => HandleDisconnected(grain, key, connection);

            _ = Task.Run(async () =>
            {
                if (!QuicTransport.IsSupported)
                    return;

                try
                {
                    var token = _cts?.Token ?? CancellationToken.None;
                    await connection.StartReceivingAsync(token);
                }
                catch (OperationCanceledException)
                {
                    // Ignore shutdown cancellations.
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "P2P QUIC receive loop failed for {Remote}", remoteEndPoint);
                }
            });

            return Task.CompletedTask;
        }

        private async Task HandleMessageAsync(IRemoteNodeGrain grain, ReadOnlyMemory<byte> data, EndPoint remoteEndPoint)
        {
            try
            {
                await grain.HandleMessageAsync(data.ToArray());
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "P2P QUIC message handling failed for {Remote}", remoteEndPoint);
            }
        }

        private void HandleDisconnected(IRemoteNodeGrain grain, string key, QuicPeerConnection connection)
        {
            _ = Task.Run(async () =>
            {
                var superseded = _transportService.IsSuperseded(key, connection);
                try
                {
                    if (!superseded)
                        await grain.DisconnectAsync();
                }
                catch
                {
                    // Ignore grain disconnect errors on shutdown.
                }

                _transportService.UnregisterConnection(key, connection);

                if (QuicTransport.IsSupported)
                {
                    try
                    {
                        await connection.DisposeAsync();
                    }
                    catch
                    {
                        // Ignore transport shutdown errors on disconnect.
                    }
                }
            });
        }

        private X509Certificate2? LoadCertificate()
        {
            if (string.IsNullOrWhiteSpace(_options.QuicCertificatePath))
                return null;

            try
            {
#pragma warning disable SYSLIB0057
                return new X509Certificate2(
                    _options.QuicCertificatePath,
                    _options.QuicCertificatePassword,
                    X509KeyStorageFlags.EphemeralKeySet);
#pragma warning restore SYSLIB0057
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load QUIC certificate from {Path}", _options.QuicCertificatePath);
                return null;
            }
        }

        private X509Certificate2 CreateSelfSignedCertificate()
        {
            using var rsa = RSA.Create(2048);
            var request = new CertificateRequest(
                "CN=neo-p2p",
                rsa,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);
            request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));
            request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, false));
            request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, false));

            var cert = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(5));
            _logger.LogInformation("Generated self-signed certificate for QUIC listener.");
            return cert;
        }

        private static IPAddress ParseBindAddress(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return IPAddress.Any;

            return IPAddress.TryParse(value, out var parsed) ? parsed : IPAddress.Any;
        }
    }
}

