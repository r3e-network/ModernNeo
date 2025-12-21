// Copyright (C) 2015-2025 The Neo Project.
//
// NeoSiloBuilderExtensions.cs file belongs to the neo project and is free
// software distributed under the MIT software license.

using Microsoft.Extensions.DependencyInjection;
using Orleans.Configuration;

namespace Neo.Orleans.Hosting;

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

        // Configure grain storage providers
        ConfigureStorage(siloBuilder, config);

        // Configure serialization for Neo types
        ConfigureSerialization(siloBuilder);

        // Configure grain collection (deactivation)
        ConfigureGrainCollection(siloBuilder, config);

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
        }
        else
        {
            // For production, use configured storage provider
            // This can be extended to support RocksDB, Azure, etc.
            siloBuilder.Services.AddSingleton(config);
        }
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
}

/// <summary>
/// Configuration options for Neo Orleans.
/// </summary>
public class NeoOrleansOptions
{
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
    /// Default: 10
    /// </summary>
    public int MaxConnections { get; set; } = 10;

    /// <summary>
    /// Maximum transactions in memory pool.
    /// Default: 50000
    /// </summary>
    public int MaxMemoryPoolSize { get; set; } = 50000;

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
