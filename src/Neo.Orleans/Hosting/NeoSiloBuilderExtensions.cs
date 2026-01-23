// Copyright (C) 2015-2025 The Neo Project.
//
// NeoSiloBuilderExtensions.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Neo;
using Neo.Orleans.Bridge;
using Neo.Orleans.Services;
using Neo.Persistence;
using Neo.Persistence.Providers;
using Orleans;
using Orleans.Configuration;
using System.Globalization;

namespace Neo.Orleans.Hosting
{
    /// <summary>
    /// Extension methods for configuring Neo Orleans silo.
    /// </summary>
    public static class NeoSiloBuilderExtensions
    {
        /// <summary>
        /// Configures the silo with Neo-specific settings.
        /// </summary>
        /// <param name="siloBuilder">The silo builder.</param>
        /// <param name="options">Optional Neo Orleans configuration options.</param>
        /// <returns>The silo builder for chaining.</returns>
        public static ISiloBuilder UseNeo(
            this ISiloBuilder siloBuilder,
            Action<NeoOrleansOptions>? options = null)
        {
            var config = new NeoOrleansOptions();
            options?.Invoke(config);
            siloBuilder.Services.AddSingleton(config);
            siloBuilder.Services.AddSingleton(config.ProtocolSettings);

            siloBuilder.Services.TryAddSingleton(sp =>
            {
                var opts = sp.GetRequiredService<NeoOrleansOptions>();
                var settings = opts.ProtocolSettings;
                var storageEngine = string.IsNullOrWhiteSpace(opts.StorageEngine)
                    ? nameof(MemoryStore)
                    : opts.StorageEngine;
                var provider = StoreFactory.GetStoreProvider(storageEngine)
                    ?? throw new InvalidOperationException($"Unknown storage engine '{storageEngine}'.");
                var storagePath = ExpandStoragePath(opts.StoragePath, settings.Network);
                var system = new NeoSystem(settings, provider, storagePath);
                var grainFactory = sp.GetRequiredService<IGrainFactory>();
                system.SetLocalNode(new OrleansLocalNodeMessageTarget(grainFactory, settings, opts));
                return system;
            });

            // Configure grain storage providers
            ConfigureStorage(siloBuilder, config);

            // Configure transport for outbound P2P messaging
            ConfigureTransport(siloBuilder);

            // Configure serialization for Neo types
            ConfigureSerialization(siloBuilder);

            // Configure grain collection (deactivation)
            ConfigureGrainCollection(siloBuilder, config);

            // Configure cluster identity
            ConfigureClusterOptions(siloBuilder, config);

            return siloBuilder;
        }

        private static void ConfigureStorage(ISiloBuilder siloBuilder, NeoOrleansOptions config)
        {
            if (config.UseMemoryStorage)
            {
                // In-memory storage for development/testing
                siloBuilder.AddMemoryGrainStorage("BlockchainStore");
                siloBuilder.AddMemoryGrainStorage("MemoryPoolStore");
                siloBuilder.AddMemoryGrainStorage("LocalNodeStore");
                siloBuilder.AddMemoryGrainStorage("ConsensusStore");
                siloBuilder.AddMemoryGrainStorage("RemoteNodeStore");
                siloBuilder.AddMemoryGrainStorage("TaskManagerStore");
                siloBuilder.AddMemoryGrainStorage("TxRouterStore");
                siloBuilder.Services.TryAddSingleton<IBlockStorageService, InMemoryBlockStorageService>();
            }
            else
            {
                // Assume external grain storage providers are configured by the host.
            }
        }

        private static void ConfigureTransport(ISiloBuilder siloBuilder)
        {
            // Default to composite transport (TCP + optional QUIC/WS inbound); host can override with a custom ITransportService.
            siloBuilder.Services.TryAddSingleton<TcpTransportService>();
            siloBuilder.Services.TryAddSingleton<QuicTransportService>();
            siloBuilder.Services.TryAddSingleton<WsTransportService>();
            siloBuilder.Services.TryAddSingleton<CompositeTransportService>();
            siloBuilder.Services.TryAddSingleton<ITransportService>(sp => sp.GetRequiredService<CompositeTransportService>());
            siloBuilder.Services.TryAddSingleton<TcpP2PListener>();
            siloBuilder.Services.TryAddSingleton<QuicP2PListener>();
            siloBuilder.Services.TryAddSingleton<WsP2PListener>();
            siloBuilder.Services.TryAddSingleton<IP2PListener, CompositeP2PListener>();
        }

        private static void ConfigureSerialization(ISiloBuilder siloBuilder)
        {
            // Neo type serialization surrogates (UInt256, UInt160) are auto-discovered
            // via [RegisterConverter] attributes in Neo.Orleans.Serialization namespace.
            // No additional configuration needed - Orleans auto-discovers them.
        }

        private static void ConfigureGrainCollection(ISiloBuilder siloBuilder, NeoOrleansOptions config)
        {
            siloBuilder.Configure<GrainCollectionOptions>(options =>
            {
                // Configure grain deactivation timeouts
                options.CollectionAge = config.GrainCollectionAge;
                options.CollectionQuantum = TimeSpan.FromMinutes(1);
            });
        }

        private static void ConfigureClusterOptions(ISiloBuilder siloBuilder, NeoOrleansOptions config)
        {
            siloBuilder.Configure<ClusterOptions>(options =>
            {
                options.ClusterId = config.ClusterId;
                options.ServiceId = config.ServiceId;
            });
        }

        private static string? ExpandStoragePath(string? template, uint network)
        {
            if (string.IsNullOrWhiteSpace(template)) return null;
            if (!template.Contains("{0}", StringComparison.Ordinal)) return template;
            return string.Format(CultureInfo.InvariantCulture, template, network);
        }
    }

    /// <summary>
    /// Configuration options for Neo Orleans.
    /// </summary>
    public class NeoOrleansOptions
    {
        /// <summary>
        /// Full protocol settings for transaction and block validation.
        /// </summary>
        public ProtocolSettings ProtocolSettings { get; set; } = ProtocolSettings.Default;

        /// <summary>
        /// Storage provider name (MemoryStore, LevelDB, RocksDB).
        /// Default: MemoryStore.
        /// </summary>
        public string StorageEngine { get; set; } = nameof(MemoryStore);

        /// <summary>
        /// Storage path template for Neo system state.
        /// Default: Data_{0}.
        /// </summary>
        public string? StoragePath { get; set; } = "Data_{0}";

        /// <summary>
        /// Validation mode for Orleans ingress.
        /// Default: Full.
        /// </summary>
        public NeoValidationMode ValidationMode { get; set; } = NeoValidationMode.Full;

        /// <summary>
        /// Whether to use in-memory storage (for development/testing).
        /// Default: true
        /// </summary>
        public bool UseMemoryStorage { get; set; } = true;

        /// <summary>
        /// Connection string for persistent storage (when UseMemoryStorage is false).
        /// </summary>
        public string? StorageConnectionString { get; set; }

        /// <summary>
        /// Time after which idle grains are deactivated.
        /// Default: 2 hours
        /// </summary>
        public TimeSpan GrainCollectionAge { get; set; } = TimeSpan.FromHours(2);

        /// <summary>
        /// Maximum number of concurrent connections per LocalNodeGrain.
        /// Default: 40
        /// </summary>
        public int MaxConnections { get; set; } = Neo.Network.P2P.ChannelsConfig.DefaultMaxConnections;

        /// <summary>
        /// Whether to enable payload compression for P2P messages.
        /// Default: true
        /// </summary>
        public bool EnableCompression { get; set; } = Neo.Network.P2P.ChannelsConfig.DefaultEnableCompression;

        /// <summary>
        /// Minimum number of desired connections to maintain.
        /// Default: 10
        /// </summary>
        public int MinDesiredConnections { get; set; } = Neo.Network.P2P.ChannelsConfig.DefaultMinDesiredConnections;

        /// <summary>
        /// Maximum number of connections per remote address.
        /// Default: 3
        /// </summary>
        public int MaxConnectionsPerAddress { get; set; } = Neo.Network.P2P.ChannelsConfig.DefaultMaxConnectionsPerAddress;

        /// <summary>
        /// Maximum known inventory hashes per peer.
        /// Default: 1000
        /// </summary>
        public int MaxKnownHashes { get; set; } = Neo.Network.P2P.ChannelsConfig.DefaultMaxKnownHashes;

        /// <summary>
        /// TCP listener port for inbound P2P connections.
        /// Default: 0 (disabled)
        /// </summary>
        public int TcpPort { get; set; }

        /// <summary>
        /// TCP listener bind address (optional).
        /// Defaults to IPAddress.Any when not specified.
        /// </summary>
        public string? TcpBindAddress { get; set; }

        /// <summary>
        /// Whether to enable QUIC listener for inbound P2P connections.
        /// Default: false
        /// </summary>
        public bool QuicEnabled { get; set; }

        /// <summary>
        /// QUIC listener port.
        /// Default: 0 (disabled)
        /// </summary>
        public int QuicPort { get; set; }

        /// <summary>
        /// QUIC ALPN string.
        /// Default: "neo-p2p"
        /// </summary>
        public string QuicAlpn { get; set; } = "neo-p2p";

        /// <summary>
        /// Optional certificate path for QUIC listener.
        /// </summary>
        public string? QuicCertificatePath { get; set; }

        /// <summary>
        /// Optional certificate password for QUIC listener.
        /// </summary>
        public string? QuicCertificatePassword { get; set; }

        /// <summary>
        /// Whether to enable WebSocket listener for inbound P2P connections.
        /// Default: false
        /// </summary>
        public bool WsEnabled { get; set; }

        /// <summary>
        /// WebSocket listener port.
        /// Default: 0 (disabled)
        /// </summary>
        public int WsPort { get; set; }

        /// <summary>
        /// Maximum transactions in memory pool.
        /// Default: 50000
        /// </summary>
        public int MaxMemoryPoolSize { get; set; } = 50000;

        /// <summary>
        /// Network magic number used for P2P handshake.
        /// Defaults to <see cref="ProtocolSettings.Default"/> network value.
        /// </summary>
        public uint NetworkMagic { get; set; } = ProtocolSettings.Default.Network;

        /// <summary>
        /// Protocol version advertised during handshake.
        /// Default: 0
        /// </summary>
        public uint ProtocolVersion { get; set; }

        /// <summary>
        /// User agent string advertised during handshake.
        /// Default: "/Neo:{version}/" derived from the Neo assembly.
        /// </summary>
        public string UserAgent { get; set; } =
            $"/Neo:{typeof(NeoSystem).Assembly.GetName().Version?.ToString(3) ?? "0.0.0"}/";

        /// <summary>
        /// Seed list used for initial peer discovery.
        /// Defaults to <see cref="ProtocolSettings.Default"/> seeds.
        /// </summary>
        public IReadOnlyList<string> SeedList { get; set; } = ProtocolSettings.Default.SeedList;

        /// <summary>
        /// Cluster ID for Orleans cluster.
        /// Default: "neo-cluster"
        /// </summary>
        public string ClusterId { get; set; } = "neo-cluster";

        /// <summary>
        /// Service ID for Orleans service.
        /// Default: "neo-service"
        /// </summary>
        public string ServiceId { get; set; } = "neo-service";
    }

    /// <summary>
    /// Validation mode for Orleans ingress.
    /// </summary>
    public enum NeoValidationMode
    {
        /// <summary>No protocol validation (test-only).</summary>
        None,
        /// <summary>State-independent validation only.</summary>
        Preverify,
        /// <summary>Full state-dependent validation.</summary>
        Full
    }
}
