// Copyright (C) 2015-2025 The Neo Project.
//
// NeoOrleansHostBuilder.cs file belongs to the neo project and is free
// software distributed under the MIT software license.

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace Neo.Orleans.Hosting;

/// <summary>
/// Builder for creating Neo Orleans host instances.
/// Provides a simplified API for common deployment scenarios.
/// </summary>
public class NeoOrleansHostBuilder
{
    private readonly HostApplicationBuilder _builder;
    private NeoOrleansOptions _options = new();
    private bool _isDevelopment;
    private int _siloPort = 11111;
    private int _gatewayPort = 30000;

    /// <summary>
    /// Creates a new Neo Orleans host builder.
    /// </summary>
    public NeoOrleansHostBuilder()
    {
        _builder = Host.CreateApplicationBuilder();
    }

    /// <summary>
    /// Configures the host for development mode with localhost clustering.
    /// </summary>
    public NeoOrleansHostBuilder UseDevelopment()
    {
        _isDevelopment = true;
        _options.UseMemoryStorage = true;
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
                opts.UseMemoryStorage = _options.UseMemoryStorage;
                opts.StorageConnectionString = _options.StorageConnectionString;
                opts.GrainCollectionAge = _options.GrainCollectionAge;
                opts.MaxConnections = _options.MaxConnections;
                opts.MaxMemoryPoolSize = _options.MaxMemoryPoolSize;
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
}
