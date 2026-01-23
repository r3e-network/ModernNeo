// Copyright (C) 2015-2025 The Neo Project.
//
// Program.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Neo.Network.P2P;
using Neo.Persistence;
using Neo.Persistence.Providers;
using Neo.RPC;
using Neo.SmartContract.Native;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Neo.Node
{
    internal static class Program
    {
        private const string DefaultConfigFileName = "config.json";
        private const string ServiceName = "Neo.Node";

        public static async Task<int> Main(string[] args)
        {
            var configArg = TryGetArgValue(args, "--config") ?? DefaultConfigFileName;
            var configPath = ProtocolSettings.FindFile(configArg, Environment.CurrentDirectory);

            if (configPath is null)
            {
                Console.Error.WriteLine($"Config file not found: '{configArg}'.");
                Console.Error.WriteLine($"Searched in: '{Environment.CurrentDirectory}' and '{AppContext.BaseDirectory}'.");
                return 2;
            }

            var builder = WebApplication.CreateBuilder(args);

            // Load Neo configuration
            builder.Configuration
                .AddJsonFile(configPath, optional: false, reloadOnChange: false)
                .AddEnvironmentVariables(prefix: "NEO_")
                .AddCommandLine(args);

            // Configure structured logging
            ConfigureLogging(builder);

            // Configure health checks
            builder.Services.AddHealthChecks()
                .AddCheck<NeoSystemHealthCheck>("neo_system");

            // Configure OpenTelemetry metrics
            ConfigureOpenTelemetry(builder);

            // Register Neo system as singleton
            builder.Services.AddSingleton<NeoSystemNode>(sp =>
            {
                var config = sp.GetRequiredService<IConfiguration>();
                return NeoSystemNodeFactory.Create(config);
            });

            // Register health check
            builder.Services.AddSingleton<NeoSystemHealthCheck>();

            // Configure Kestrel for management endpoint
            var managementPort = builder.Configuration.GetValue("ApplicationConfiguration:Management:Port", 5000);
            builder.Services.Configure<KestrelServerOptions>(options =>
            {
                options.ListenAnyIP(managementPort);
            });

            var app = builder.Build();

            // Map health endpoint
            app.MapHealthChecks("/health", new HealthCheckOptions
            {
                ResponseWriter = WriteHealthCheckResponse
            });

            // Map metrics endpoint (Prometheus format)
            app.MapPrometheusScrapingEndpoint("/metrics");

            // Map ready endpoint
            app.MapGet("/ready", () => Results.Ok(new { status = "ready", timestamp = DateTimeOffset.UtcNow }));

            // Node information endpoint (quick status)
            app.MapGet("/info", (IServiceProvider sp) =>
            {
                var node = sp.GetRequiredService<NeoSystemNode>();
                var system = node.System;

                var info = new Dictionary<string, object?>
                {
                    ["network"] = system.Settings.Network,
                    ["p2p_port"] = node.ChannelsConfig.Tcp?.Port,
                    ["mempool_count"] = system.MemPool.Count,
                    ["mempool_verified"] = system.MemPool.VerifiedCount,
                    ["mempool_unverified"] = system.MemPool.UnVerifiedCount,
                    ["block_height"] = NativeContract.Ledger.CurrentIndex(system.StoreView),
                    // Note: Peer counts not available in Neo.Node
                    // Use Neo.Orleans for peer management in production
                    ["peers_connected"] = 0,
                    ["peers_unconnected"] = 0
                };

                return Results.Ok(info);
            });

            // WebSocket P2P endpoint - Neo.Node is a lightweight node without P2P networking.
            // For full P2P support, use Neo.Orleans which provides distributed consensus via Orleans grains.
            // See: src/Neo.Orleans/Grains/LocalNodeGrain.cs for P2P implementation
            app.Map("/p2p", context =>
            {
                context.Response.StatusCode = 501; // Not Implemented
                return context.Response.WriteAsync("WebSocket P2P not available in Neo.Node. Use Neo.Orleans for full P2P networking support.");
            });

            // QUIC P2P - Available in Neo.Orleans via QuicServerConnection and QuicTransport
            // See: src/Neo.Network/P2P/Transport/QuicTransport.cs

            // Start Neo system
            var node = app.Services.GetRequiredService<NeoSystemNode>();
            var logger = app.Services.GetRequiredService<ILogger<NeoSystemNode>>();

            // Bridge Neo logging to ILogger
            ConfigureNeoLogging(logger, builder.Configuration);

            node.Start();
            logger.LogInformation("Neo.Node started on P2P port {Port}", node.ChannelsConfig.Tcp?.Port ?? 0);
            logger.LogInformation("Management endpoints available at http://localhost:{Port}", managementPort);
            logger.LogWarning("P2P networking disabled in Neo.Node. Use Neo.Orleans for full P2P support.");

            // Optionally start JSON-RPC server
            var rpcEnabled = builder.Configuration.GetValue("ApplicationConfiguration:Rpc:Enabled", false);
            HttpRpcServer? rpcServer = null;
            if (rpcEnabled)
            {
                var rpcEndpoint = builder.Configuration.GetValue("ApplicationConfiguration:Rpc:ListenAddress", "http://localhost:10332/");
                rpcServer = new HttpRpcServer(new HttpRpcServerOptions { ListenAddress = rpcEndpoint });

                // Register RPC methods dynamically from Neo.Node.Rpc namespace
                var rpcTypes = Assembly.GetExecutingAssembly()
                    .GetTypes()
                    .Where(t => typeof(IRpcMethod).IsAssignableFrom(t) && !t.IsAbstract && t.IsClass)
                    .ToList();

                foreach (var t in rpcTypes)
                {
                    try
                    {
                        IRpcMethod? method = null;
                        // Prefer constructor with NeoSystemNode
                        var ctor = t.GetConstructor(new[] { typeof(NeoSystemNode) });
                        if (ctor != null)
                        {
                            method = (IRpcMethod)ctor.Invoke(new object[] { node });
                        }
                        else if (t.GetConstructor(Type.EmptyTypes) is { } defaultCtor)
                        {
                            method = (IRpcMethod)defaultCtor.Invoke(null);
                        }

                        if (method != null)
                            rpcServer.RegisterMethod(method);
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "Failed to register RPC method {Type}", t.FullName);
                    }
                }

                await rpcServer.StartAsync();
                logger.LogInformation("JSON-RPC server listening at {Endpoint}", rpcServer.Endpoint);

                app.Lifetime.ApplicationStopping.Register(() =>
                {
                    try { rpcServer.StopAsync().GetAwaiter().GetResult(); }
                    catch { }
                    rpcServer.Dispose();
                });
            }

            await app.RunAsync();

            return 0;
        }

        private static void ConfigureLogging(WebApplicationBuilder builder)
        {
            builder.Logging.ClearProviders();
            builder.Logging.AddConsole(options =>
            {
                options.FormatterName = "json";
            });

            var minLevelText = builder.Configuration.GetValue("ApplicationConfiguration:Logger:LogLevel", "Information");
            if (Enum.TryParse<Microsoft.Extensions.Logging.LogLevel>(minLevelText, ignoreCase: true, out var minLevel))
            {
                builder.Logging.SetMinimumLevel(minLevel);
            }
        }

        private static void ConfigureOpenTelemetry(WebApplicationBuilder builder)
        {
            builder.Services.AddOpenTelemetry()
                .ConfigureResource(resource => resource.AddService(ServiceName))
                .WithMetrics(metrics =>
                {
                    metrics.AddRuntimeInstrumentation();
                    metrics.AddMeter("Neo.SmartContract");
                    metrics.AddMeter("Neo.Network");
                    metrics.AddMeter("Neo.Ledger");
                    metrics.AddPrometheusExporter();
                });
        }

        private static void ConfigureNeoLogging(ILogger logger, IConfiguration configuration)
        {
            var enabled = configuration.GetValue("ApplicationConfiguration:Logger:ConsoleOutput", true);
            if (!enabled) return;

            var minLevelText = configuration.GetValue("ApplicationConfiguration:Logger:LogLevel", "Info");
            if (Enum.TryParse<LogLevel>(minLevelText, ignoreCase: true, out var minLevel))
                Utility.LogLevel = minLevel;

            Utility.Logging += (source, level, message) =>
            {
                var logLevel = level switch
                {
                    LogLevel.Debug => Microsoft.Extensions.Logging.LogLevel.Debug,
                    LogLevel.Info => Microsoft.Extensions.Logging.LogLevel.Information,
                    LogLevel.Warning => Microsoft.Extensions.Logging.LogLevel.Warning,
                    LogLevel.Error => Microsoft.Extensions.Logging.LogLevel.Error,
                    LogLevel.Fatal => Microsoft.Extensions.Logging.LogLevel.Critical,
                    _ => Microsoft.Extensions.Logging.LogLevel.Information
                };
                logger.Log(logLevel, "[{Source}] {Message}", source, message);
            };
        }

        private static async Task WriteHealthCheckResponse(HttpContext context, HealthReport report)
        {
            context.Response.ContentType = "application/json";

            var response = new
            {
                status = report.Status.ToString(),
                totalDuration = report.TotalDuration.TotalMilliseconds,
                checks = report.Entries.Select(e => new
                {
                    name = e.Key,
                    status = e.Value.Status.ToString(),
                    duration = e.Value.Duration.TotalMilliseconds,
                    description = e.Value.Description,
                    data = e.Value.Data
                })
            };

            await context.Response.WriteAsync(JsonSerializer.Serialize(response, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true
            }));
        }

        private static string? TryGetArgValue(string[] args, string name)
        {
            for (var i = 0; i < args.Length; i++)
            {
                var arg = args[i];

                if (arg.Equals(name, StringComparison.OrdinalIgnoreCase))
                    return i + 1 < args.Length ? args[i + 1] : null;

                if (arg.StartsWith(name + "=", StringComparison.OrdinalIgnoreCase))
                    return arg[(name.Length + 1)..];
            }

            return null;
        }
    }

    /// <summary>
    /// Factory for creating NeoSystemNode instances from configuration.
    /// </summary>
    public static class NeoSystemNodeFactory
    {
        /// <summary>
        /// Creates a new NeoSystemNode from the provided configuration.
        /// </summary>
        public static NeoSystemNode Create(IConfiguration configuration)
        {
            var protocolSettings = ProtocolSettings.Load(configuration.GetSection("ProtocolConfiguration"));

            var engine = configuration.GetValue<string>("ApplicationConfiguration:Storage:Engine");
            if (string.IsNullOrWhiteSpace(engine))
                engine = nameof(MemoryStore);

            var provider = StoreFactory.GetStoreProvider(engine);
            if (provider is null)
            {
                var providers = string.Join(", ", StoreFactory.GetProviderNames());
                throw new InvalidOperationException($"Unknown storage engine '{engine}'. Available: {providers}");
            }

            var storagePathTemplate = configuration.GetValue<string>("ApplicationConfiguration:Storage:Path");
            var storagePath = ExpandStoragePath(storagePathTemplate, protocolSettings.Network);

            var channelsConfig = ChannelsConfigFactory.Create(configuration);
            var system = new NeoSystem(protocolSettings, provider, storagePath);

            return new NeoSystemNode(system, channelsConfig);
        }

        private static string? ExpandStoragePath(string? template, uint network)
        {
            if (string.IsNullOrWhiteSpace(template)) return template;
            if (!template.Contains("{0}", StringComparison.Ordinal)) return template;
            return string.Format(CultureInfo.InvariantCulture, template, network);
        }
    }

    /// <summary>
    /// Factory for creating ChannelsConfig from configuration.
    /// </summary>
    public static class ChannelsConfigFactory
    {
        /// <summary>
        /// Creates a ChannelsConfig from the provided configuration.
        /// </summary>
        public static ChannelsConfig Create(IConfiguration configuration)
        {
            var p2p = configuration.GetSection("ApplicationConfiguration").GetSection("P2P");
            var port = p2p.GetValue("Port", 0);
            var bindAddress = p2p.GetValue<string>("BindAddress") ?? p2p.GetValue<string>("ListenAddress");

            IPEndPoint? tcp = null;
            if (port > 0)
            {
                var address = ParseAddressOrAny(bindAddress);
                tcp = new IPEndPoint(address, port);
            }

            return new ChannelsConfig
            {
                Tcp = tcp,
                EnableCompression = p2p.GetValue("EnableCompression", ChannelsConfig.DefaultEnableCompression),
                MinDesiredConnections = p2p.GetValue("MinDesiredConnections", ChannelsConfig.DefaultMinDesiredConnections),
                MaxConnections = p2p.GetValue("MaxConnections", ChannelsConfig.DefaultMaxConnections),
                MaxConnectionsPerAddress = p2p.GetValue("MaxConnectionsPerAddress", ChannelsConfig.DefaultMaxConnectionsPerAddress),
                MaxKnownHashes = p2p.GetValue("MaxKnownHashes", ChannelsConfig.DefaultMaxKnownHashes),
            };
        }

        private static IPAddress ParseAddressOrAny(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return IPAddress.Any;
            return IPAddress.TryParse(value, out var parsed) ? parsed : IPAddress.Any;
        }
    }

    /// <summary>
    /// Wrapper around NeoSystem providing lifecycle management.
    /// </summary>
    public sealed class NeoSystemNode : IDisposable
    {
        private readonly NeoSystem _system;
        private readonly ChannelsConfig _channelsConfig;
        private bool _started;

        /// <summary>
        /// Initializes a new instance of the NeoSystemNode class.
        /// </summary>
        public NeoSystemNode(NeoSystem system, ChannelsConfig channelsConfig)
        {
            _system = system;
            _channelsConfig = channelsConfig;
        }

        /// <summary>
        /// Gets the underlying NeoSystem instance.
        /// </summary>
        public NeoSystem System => _system;

        /// <summary>
        /// Gets the channels configuration.
        /// </summary>
        public ChannelsConfig ChannelsConfig => _channelsConfig;

        /// <summary>
        /// Gets whether the node has been started.
        /// </summary>
        public bool IsStarted => _started;

        /// <summary>
        /// Starts the Neo node.
        /// </summary>
        public void Start()
        {
            if (_started) return;
            _system.StartNode(_channelsConfig);
            _started = true;
        }

        /// <summary>
        /// Disposes the Neo system.
        /// </summary>
        public void Dispose()
        {
            _system.Dispose();
        }
    }

    /// <summary>
    /// Health check for the Neo system.
    /// </summary>
    public class NeoSystemHealthCheck : IHealthCheck
    {
        private readonly NeoSystemNode? _node;

        /// <summary>
        /// Initializes a new instance of the NeoSystemHealthCheck class.
        /// </summary>
        public NeoSystemHealthCheck(NeoSystemNode? node = null)
        {
            _node = node;
        }

        /// <summary>
        /// Checks the health of the Neo system.
        /// </summary>
        public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            if (_node is null)
            {
                return Task.FromResult(HealthCheckResult.Unhealthy("Neo system not initialized"));
            }

            if (!_node.IsStarted)
            {
                return Task.FromResult(HealthCheckResult.Degraded("Neo system not started"));
            }

            var data = new Dictionary<string, object>
            {
                ["network"] = _node.System.Settings.Network,
                ["mempool_count"] = _node.System.MemPool.Count,
                ["mempool_verified"] = _node.System.MemPool.VerifiedCount,
                ["mempool_unverified"] = _node.System.MemPool.UnVerifiedCount
            };

            return Task.FromResult(HealthCheckResult.Healthy("Neo system is running", data));
        }
    }
}
