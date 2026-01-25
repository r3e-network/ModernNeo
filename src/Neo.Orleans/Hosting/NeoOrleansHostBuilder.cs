// Copyright (C) 2015-2025 The Neo Project.
//
// NeoOrleansHostBuilder.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Neo;
using Neo.Cryptography.ECC;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace Neo.Orleans.Hosting
{
    /// <summary>
    /// Builder for creating Neo Orleans host instances.
    /// Provides a simplified API for common deployment scenarios.
    /// </summary>
    public class NeoOrleansHostBuilder
    {
        private readonly HostApplicationBuilder _builder;
        private readonly NeoOrleansOptions _options = new();
        private bool _isDevelopment;
        private int _siloPort = 11111;
        private int _gatewayPort = 30000;

        /// <summary>
        /// Creates a new Neo Orleans host builder.
        /// </summary>
        public NeoOrleansHostBuilder()
        {
            _builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
            {
                DisableDefaults = true
            });
            _builder.Configuration
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
                .AddJsonFile($"appsettings.{_builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: false)
                .AddEnvironmentVariables();
        }

        /// <summary>
        /// Configures the host for development mode with localhost clustering.
        /// </summary>
        public NeoOrleansHostBuilder UseDevelopment()
        {
            _isDevelopment = true;
            _options.UseMemoryStorage = true;
            if (_options.ProtocolSettings.StandbyCommittee.Count == 0 || _options.ProtocolSettings.ValidatorsCount == 0)
            {
                ApplyProtocolSettings(_options, CreateDevelopmentProtocolSettings());
            }
            return this;
        }

        /// <summary>
        /// Configures Neo Orleans options.
        /// </summary>
        public NeoOrleansHostBuilder Configure(Action<NeoOrleansOptions> configure)
        {
            configure(_options);
            return this;
        }

        /// <summary>
        /// Configures Neo Orleans options from application configuration.
        /// </summary>
        public NeoOrleansHostBuilder ConfigureFromConfiguration(IConfiguration configuration)
        {
            var protocolSettings = ProtocolSettings.Load(configuration.GetSection("ProtocolConfiguration"));
            _options.ProtocolSettings = protocolSettings;
            ApplyProtocolSettings(_options, protocolSettings);

            var storage = configuration.GetSection("ApplicationConfiguration").GetSection("Storage");
            var storageEngine = storage.GetValue<string>("Engine");
            if (!string.IsNullOrWhiteSpace(storageEngine))
                _options.StorageEngine = storageEngine;
            var storagePath = storage.GetValue<string>("Path");
            if (!string.IsNullOrWhiteSpace(storagePath))
                _options.StoragePath = storagePath;

            var validationModeText = configuration.GetValue<string>("ApplicationConfiguration:Validation:Mode");
            if (!string.IsNullOrWhiteSpace(validationModeText) &&
                Enum.TryParse<NeoValidationMode>(validationModeText, ignoreCase: true, out var validationMode))
            {
                _options.ValidationMode = validationMode;
            }

            var p2p = configuration.GetSection("ApplicationConfiguration").GetSection("P2P");
            _options.MinDesiredConnections = p2p.GetValue("MinDesiredConnections", _options.MinDesiredConnections);
            _options.MaxConnections = p2p.GetValue("MaxConnections", _options.MaxConnections);
            _options.MaxConnectionsPerAddress = p2p.GetValue("MaxConnectionsPerAddress", _options.MaxConnectionsPerAddress);
            _options.MaxKnownHashes = p2p.GetValue("MaxKnownHashes", _options.MaxKnownHashes);
            _options.EnableCompression = p2p.GetValue("EnableCompression", _options.EnableCompression);
            _options.ProtocolVersion = p2p.GetValue("ProtocolVersion", _options.ProtocolVersion);
            _options.TcpPort = p2p.GetValue("Port", _options.TcpPort);

            var userAgent = p2p.GetValue<string>("UserAgent");
            if (!string.IsNullOrWhiteSpace(userAgent))
                _options.UserAgent = userAgent;

            var bindAddress = p2p.GetValue<string>("BindAddress") ?? p2p.GetValue<string>("ListenAddress");
            if (!string.IsNullOrWhiteSpace(bindAddress))
                _options.TcpBindAddress = bindAddress;

            var quic = p2p.GetSection("Quic");
            _options.QuicEnabled = quic.GetValue("Enabled", _options.QuicEnabled);
            _options.QuicPort = quic.GetValue("Port", _options.QuicPort);
            var alpn = quic.GetValue<string>("Alpn");
            if (!string.IsNullOrWhiteSpace(alpn))
                _options.QuicAlpn = alpn;

            var certPath = quic.GetValue<string>("CertificatePath");
            if (!string.IsNullOrWhiteSpace(certPath))
                _options.QuicCertificatePath = certPath;

            var certPassword = quic.GetValue<string>("CertificatePassword");
            if (!string.IsNullOrWhiteSpace(certPassword))
                _options.QuicCertificatePassword = certPassword;

            var ws = p2p.GetSection("WebSocket");
            var wsEnabled = ws.GetValue<bool?>("Enabled");
            _options.WsPort = ws.GetValue("Port", _options.WsPort);
            if (_options.WsPort <= 0)
                _options.WsPort = p2p.GetValue("WsPort", _options.WsPort);
            if (wsEnabled.HasValue)
                _options.WsEnabled = wsEnabled.Value;
            else if (_options.WsPort > 0)
                _options.WsEnabled = true;

            return this;
        }

        /// <summary>
        /// Configures Neo Orleans options from protocol settings.
        /// </summary>
        public NeoOrleansHostBuilder WithProtocolSettings(ProtocolSettings settings)
        {
            ApplyProtocolSettings(_options, settings);
            return this;
        }

        /// <summary>
        /// Sets the silo port for Orleans clustering.
        /// </summary>
        public NeoOrleansHostBuilder WithSiloPort(int port)
        {
            _siloPort = port;
            return this;
        }

        /// <summary>
        /// Sets the gateway port for Orleans client connections.
        /// </summary>
        public NeoOrleansHostBuilder WithGatewayPort(int port)
        {
            _gatewayPort = port;
            return this;
        }

        /// <summary>
        /// Configures logging.
        /// </summary>
        public NeoOrleansHostBuilder ConfigureLogging(Action<ILoggingBuilder> configure)
        {
            _builder.Logging.ClearProviders();
            configure(_builder.Logging);
            return this;
        }

        /// <summary>
        /// Builds the Orleans host.
        /// </summary>
        public IHost Build()
        {
            _builder.UseOrleans(siloBuilder =>
            {
                if (_isDevelopment)
                {
                    // Development: localhost clustering
                    siloBuilder.UseLocalhostClustering(
                        siloPort: _siloPort,
                        gatewayPort: _gatewayPort);
                }

                // Apply Neo-specific configuration
                siloBuilder.UseNeo(opts =>
                {
                    opts.ProtocolSettings = _options.ProtocolSettings;
                    opts.StorageEngine = _options.StorageEngine;
                    opts.StoragePath = _options.StoragePath;
                    opts.ValidationMode = _options.ValidationMode;
                    opts.UseMemoryStorage = _options.UseMemoryStorage;
                    opts.StorageConnectionString = _options.StorageConnectionString;
                    opts.GrainCollectionAge = _options.GrainCollectionAge;
                    opts.MinDesiredConnections = _options.MinDesiredConnections;
                    opts.MaxConnections = _options.MaxConnections;
                    opts.MaxConnectionsPerAddress = _options.MaxConnectionsPerAddress;
                    opts.MaxKnownHashes = _options.MaxKnownHashes;
                    opts.EnableCompression = _options.EnableCompression;
                    opts.TcpPort = _options.TcpPort;
                    opts.TcpBindAddress = _options.TcpBindAddress;
                    opts.QuicEnabled = _options.QuicEnabled;
                    opts.QuicPort = _options.QuicPort;
                    opts.QuicAlpn = _options.QuicAlpn;
                    opts.QuicCertificatePath = _options.QuicCertificatePath;
                    opts.QuicCertificatePassword = _options.QuicCertificatePassword;
                    opts.WsEnabled = _options.WsEnabled;
                    opts.WsPort = _options.WsPort;
                    opts.MaxMemoryPoolSize = _options.MaxMemoryPoolSize;
                    opts.NetworkMagic = _options.NetworkMagic;
                    opts.ProtocolVersion = _options.ProtocolVersion;
                    opts.UserAgent = _options.UserAgent;
                    opts.SeedList = _options.SeedList;
                    opts.ClusterId = _options.ClusterId;
                    opts.ServiceId = _options.ServiceId;
                });
            });

            return _builder.Build();
        }

        /// <summary>
        /// Creates a development host with default settings.
        /// Convenience method for quick local testing.
        /// </summary>
        public static IHost CreateDevelopmentHost()
        {
            return new NeoOrleansHostBuilder()
                .UseDevelopment()
                .ConfigureLogging(logging =>
                {
                    logging.AddConsole();
                    logging.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Information);
                })
                .Build();
        }

        private static ProtocolSettings CreateDevelopmentProtocolSettings()
        {
            return ProtocolSettings.Default with
            {
                Network = 0x334F454Eu,
                StandbyCommittee =
                [
                    ECPoint.Parse("0278ed78c917797b637a7ed6e7a9d94e8c408444c41ee4c0a0f310a256b9271eda", ECCurve.Secp256r1)
                ],
                ValidatorsCount = 1,
                SeedList =
                [
                    "seed1.neo.org:10333"
                ],
            };
        }

        private static void ApplyProtocolSettings(NeoOrleansOptions options, ProtocolSettings settings)
        {
            options.ProtocolSettings = settings;
            options.NetworkMagic = settings.Network;
            options.SeedList = settings.SeedList;
            options.MaxMemoryPoolSize = settings.MemoryPoolMaxTransactions;
        }
    }
}
