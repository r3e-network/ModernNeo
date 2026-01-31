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
using Microsoft.Extensions.Logging;
using Neo;
using Neo.Core;
using Neo.Cryptography.ECC;
using Neo.Ledger;
using Neo.Network.P2P;
using Neo.Orleans.Adapters;
using Neo.Orleans.Bridge;
using Neo.Orleans.Options;
using Neo.Orleans.Services;
using Neo.Persistence;
using Neo.Persistence.Providers;
using Neo.SmartContract.Native;
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
        /// Configures the silo with Neo-specific settings using focused option classes.
        /// This is the recommended approach for new applications.
        /// </summary>
        /// <param name="siloBuilder">The silo builder.</param>
        /// <param name="storageOptions">Storage configuration options.</param>
        /// <param name="p2POptions">P2P network configuration options.</param>
        /// <param name="consensusOptions">Consensus and network protocol configuration options.</param>
        /// <param name="orleansOptions">Orleans-specific configuration options.</param>
        /// <returns>The silo builder for chaining.</returns>
        public static ISiloBuilder UseNeo(
            this ISiloBuilder siloBuilder,
            StorageOptions? storageOptions = null,
            P2POptions? p2POptions = null,
            ConsensusOptions? consensusOptions = null,
            OrleansOptions? orleansOptions = null)
        {
            storageOptions ??= new StorageOptions();
            p2POptions ??= new P2POptions();
            consensusOptions ??= new ConsensusOptions();
            orleansOptions ??= new OrleansOptions();

            var effectiveProtocolSettings = consensusOptions.ProtocolSettings ?? ProtocolSettings.Default;

            siloBuilder.Services.AddSingleton<IProtocolSettings>(effectiveProtocolSettings);

            siloBuilder.Services.TryAddSingleton<INeoSystem>(sp =>
            {
                var settings = effectiveProtocolSettings;
                var storageEngine = string.IsNullOrWhiteSpace(storageOptions.Engine)
                    ? nameof(MemoryStore)
                    : storageOptions.Engine;
                var provider = StoreFactory.GetStoreProvider(storageEngine)
                    ?? throw new InvalidOperationException($"Unknown storage engine '{storageEngine}'.");
                var storagePath = ExpandStoragePath(storageOptions.Path, settings.Network);
                var system = new NeoSystem(settings, provider, storagePath, null);
                var grainFactory = sp.GetRequiredService<IGrainFactory>();
                system.SetLocalNode(new OrleansLocalNodeMessageTarget(grainFactory, settings, orleansOptions));
                return new NeoSystemAdapter(system);
            });

            ConfigureStorage(siloBuilder, storageOptions);
            ConfigureTransport(siloBuilder, p2POptions, effectiveProtocolSettings);
            ConfigureSerialization(siloBuilder);
            ConfigureGrainCollection(siloBuilder, orleansOptions);
            ConfigureClusterOptions(siloBuilder, orleansOptions);

            return siloBuilder;
        }

        /// <summary>
        /// Configures the silo with Neo-specific settings using OrleansOptions.
        /// </summary>
        /// <param name="siloBuilder">The silo builder.</param>
        /// <param name="configure">Action to configure Orleans options.</param>
        /// <returns>The silo builder for chaining.</returns>
        public static ISiloBuilder UseNeo(
            this ISiloBuilder siloBuilder,
            Action<OrleansOptions> configure)
        {
            var orleansOptions = new OrleansOptions();
            configure(orleansOptions);

            var storageOptions = new StorageOptions
            {
                Engine = orleansOptions.StorageEngine,
                Path = orleansOptions.StoragePath,
                UseMemory = orleansOptions.UseMemoryStorage,
                ConnectionString = orleansOptions.StorageConnectionString
            };

            var p2POptions = new P2POptions
            {
                TcpPort = orleansOptions.TcpPort,
                TcpBindAddress = orleansOptions.TcpBindAddress,
                MaxConnections = orleansOptions.MaxConnections,
                MaxConnectionsPerAddress = orleansOptions.MaxConnectionsPerAddress,
                MinDesiredConnections = orleansOptions.MinDesiredConnections,
                EnableCompression = orleansOptions.EnableCompression,
                MaxKnownHashes = orleansOptions.MaxKnownHashes,
                WsPort = orleansOptions.WsPort,
                QuicPort = orleansOptions.QuicPort,
                QuicAlpn = orleansOptions.QuicAlpn,
                QuicCertificatePath = orleansOptions.QuicCertificatePath,
                QuicCertificatePassword = orleansOptions.QuicCertificatePassword
            };

            var consensusOptions = new ConsensusOptions
            {
                ProtocolSettings = orleansOptions.ProtocolSettings,
                NetworkMagic = orleansOptions.NetworkMagic,
                ProtocolVersion = orleansOptions.ProtocolVersion,
                UserAgent = orleansOptions.UserAgent,
                SeedList = orleansOptions.SeedList
            };

            return UseNeo(siloBuilder, storageOptions, p2POptions, consensusOptions, orleansOptions);
        }

        private static void ConfigureStorage(ISiloBuilder siloBuilder, StorageOptions storageOptions)
        {
            if (storageOptions.UseMemory)
            {
                siloBuilder.AddMemoryGrainStorage("BlockchainStore");
                siloBuilder.AddMemoryGrainStorage("MemoryPoolStore");
                siloBuilder.AddMemoryGrainStorage("LocalNodeStore");
                siloBuilder.AddMemoryGrainStorage("ConsensusStore");
                siloBuilder.AddMemoryGrainStorage("RemoteNodeStore");
                siloBuilder.AddMemoryGrainStorage("TaskManagerStore");
                siloBuilder.AddMemoryGrainStorage("TxRouterStore");
                siloBuilder.Services.TryAddSingleton<IBlockStorageService, InMemoryBlockStorageService>();
            }
        }

        private static void ConfigureGrainCollection(ISiloBuilder siloBuilder, OrleansOptions orleansOptions)
        {
            siloBuilder.Configure<GrainCollectionOptions>(options =>
            {
                options.CollectionAge = orleansOptions.GrainCollectionAge;
                options.CollectionQuantum = TimeSpan.FromMinutes(1);
            });
        }

        private static void ConfigureClusterOptions(ISiloBuilder siloBuilder, OrleansOptions orleansOptions)
        {
            siloBuilder.Configure<ClusterOptions>(options =>
            {
                options.ClusterId = orleansOptions.ClusterId;
                options.ServiceId = orleansOptions.ServiceId;
            });
        }

        private static void ConfigureTransport(ISiloBuilder siloBuilder, P2POptions p2POptions, IProtocolSettings settings)
        {
            siloBuilder.Services.TryAddSingleton<TcpTransportService>(sp =>
            {
                var logger = sp.GetService<ILogger<TcpTransportService>>();
                return new TcpTransportService(
                    connectTimeout: TimeSpan.FromSeconds(10),
                    sendTimeout: TimeSpan.FromSeconds(5),
                    grainFactory: sp.GetRequiredService<IGrainFactory>(),
                    logger: logger);
            });
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
        }

        private static string? ExpandStoragePath(string? template, uint network)
        {
            if (string.IsNullOrWhiteSpace(template)) return null;
            if (!template.Contains("{0}", StringComparison.Ordinal)) return template;
            return string.Format(CultureInfo.InvariantCulture, template, network);
        }
    }
}
