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
using System.IO;
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
        private const string ServiceName = "Neo.Node";
        private const string DefaultConfigFileName = "config.json";

        public static async Task<int> Main(string[] args)
        {
            var network = "mainnet";
            var configFile = DefaultConfigFileName;
            var outputConfig = false;

            // Parse CLI arguments
            for (int i = 0; i < args.Length; i++)
            {
                var arg = args[i].ToLowerInvariant();
                switch (arg)
                {
                    case "--testnet":
                        network = "testnet";
                        configFile = "config.testnet.json";
                        break;
                    case "--mainnet":
                        network = "mainnet";
                        configFile = "config.json";
                        break;
                    case "--config":
                        if (i + 1 < args.Length)
                        {
                            configFile = args[++i];
                            network = "custom";
                        }
                        break;
                    case "--output-config":
                        outputConfig = true;
                        break;
                    case "-h":
                    case "--help":
                        ShowHelp();
                        return 0;
                    case "--version":
                        ShowVersion();
                        return 0;
                }
            }

            Console.WriteLine($@"╔═══════════════════════════════════════════════════════════════╗
║                    ModernNeo Node v3.9.2                  ║
╚═══════════════════════════════════════════════════════════════╝");
            Console.WriteLine();

            var configPath = ProtocolSettings.FindFile(configFile, Environment.CurrentDirectory);

            if (configPath is null)
            {
                Console.Error.WriteLine($"[ERROR] Config file not found: '{configFile}'");
                Console.Error.WriteLine($"        Searched in: '{Environment.CurrentDirectory}'");
                Console.Error.WriteLine();
                Console.Error.WriteLine("Usage:");
                Console.Error.WriteLine("  dotnet run                    # Mainnet (default)");
                Console.Error.WriteLine("  dotnet run --testnet          # Testnet");
                Console.Error.WriteLine("  dotnet run --config custom.json");
                Console.Error.WriteLine();
                return 2;
            }

            Console.WriteLine($"[INFO] Network: {network}");
            Console.WriteLine($"[INFO] Config: {Path.GetFileName(configPath)}");

            // Load configuration
            var builder = WebApplication.CreateBuilder(args);
            builder.Configuration
                .AddJsonFile(configPath, optional: false, reloadOnChange: false)
                .AddEnvironmentVariables(prefix: "NEO_")
                .AddCommandLine(args);

            var protocolConfig = builder.Configuration.GetSection("ProtocolConfiguration");
            var netMagic = protocolConfig.GetValue<uint>("Network");
            Console.WriteLine($"[INFO] Network Magic: {netMagic}");

            // Output config template if requested
            if (outputConfig)
            {
                GenerateConfigTemplate(network, netMagic);
                return 0;
            }

            // Configure logging
            ConfigureLogging(builder);

            // Health checks
            builder.Services.AddHealthChecks()
                .AddCheck<NeoSystemHealthCheck>("neo_system");

            // OpenTelemetry
            ConfigureOpenTelemetry(builder);

            // Register Neo system
            builder.Services.AddSingleton<NeoSystemNode>(sp =>
            {
                var config = sp.GetRequiredService<IConfiguration>();
                return NeoSystemNodeFactory.Create(config);
            });
            builder.Services.AddSingleton<NeoSystemHealthCheck>();

            // Management port
            var managementPort = builder.Configuration.GetValue("ApplicationConfiguration:Management:Port", 5001);
            builder.Services.Configure<KestrelServerOptions>(options =>
            {
                options.ListenAnyIP(managementPort);
            });

            var app = builder.Build();

            // Health endpoint
            app.MapHealthChecks("/health", new HealthCheckOptions
            {
                ResponseWriter = WriteHealthCheckResponse
            });

            // Metrics endpoint
            app.MapPrometheusScrapingEndpoint("/metrics");

            // Ready endpoint
            app.MapGet("/ready", () => Results.Ok(new { status = "ready", timestamp = DateTimeOffset.UtcNow }));

            // Info endpoint
            app.MapGet("/info", (IServiceProvider sp) =>
            {
                var node = sp.GetRequiredService<NeoSystemNode>();
                var system = node.System;

                uint blockHeight = 0;
                try
                {
                    blockHeight = NativeContract.Ledger.CurrentIndex(system.StoreView);
                }
                catch
                {
                    // Blockchain not initialized yet
                }

                return Results.Ok(new Dictionary<string, object?>
                {
                    ["network"] = system.Settings.Network,
                    ["network_magic"] = netMagic,
                    ["p2p_port"] = node.ChannelsConfig.Tcp?.Port ?? 0,
                    ["block_height"] = blockHeight,
                    ["mempool_count"] = system.MemPool.Count,
                    ["mempool_verified"] = system.MemPool.VerifiedCount,
                    ["mempool_unverified"] = system.MemPool.UnVerifiedCount,
                    ["peers_connected"] = 0,
                    ["peers_unconnected"] = 0
                });
            });

            // Start node
            var node = app.Services.GetRequiredService<NeoSystemNode>();
            var logger = app.Services.GetRequiredService<ILogger<NeoSystemNode>>();
            ConfigureNeoLogging(logger, builder.Configuration);

            node.Start();

            Console.WriteLine();
            Console.WriteLine("═══════════════════════════════════════════════════════════════");
            Console.WriteLine($"  ModernNeo RPC Node started successfully!");
            Console.WriteLine("═══════════════════════════════════════════════════════════════");
            Console.WriteLine();
            Console.WriteLine($"[NOTE] This is an RPC/API node. For full P2P networking,");
            Console.WriteLine($"      block sync, and consensus, use Neo.Orleans instead:");
            Console.WriteLine($"      cd src/Neo.Orleans && dotnet run -- --testnet");
            Console.WriteLine();
            Console.WriteLine($"[STATUS] P2P Port: {node.ChannelsConfig.Tcp?.Port ?? 0} (listening only)");
            Console.WriteLine($"[STATUS] Management: http://localhost:{managementPort}");
            Console.WriteLine($"[STATUS] Health: http://localhost:{managementPort}/health");
            Console.WriteLine($"[STATUS] Metrics: http://localhost:{managementPort}/metrics");
            Console.WriteLine();
            uint blockHeight = 0;
            try
            {
                blockHeight = NativeContract.Ledger.CurrentIndex(node.System.StoreView);
            }
            catch { }
            Console.WriteLine($"[INFO] Block Height: {blockHeight}");
            Console.WriteLine($"[INFO] MemPool: {node.System.MemPool.Count} transactions");
            Console.WriteLine();

            // Optional RPC
            var rpcEnabled = builder.Configuration.GetValue("ApplicationConfiguration:Rpc:Enabled", false);
            if (rpcEnabled)
            {
                var rpcEndpoint = builder.Configuration.GetValue("ApplicationConfiguration:Rpc:ListenAddress", "http://localhost:10332/");
                Console.WriteLine($"[STATUS] RPC: {rpcEndpoint}");
            }

            Console.WriteLine();
            Console.WriteLine("Press Ctrl+C to stop...");

            await app.RunAsync();

            return 0;
        }

        private static void ShowHelp()
        {
            Console.WriteLine($@"ModernNeo Node v3.9.2

Usage: dotnet run [OPTIONS]

Options:
  --testnet          Connect to Neo Testnet (default: mainnet)
  --mainnet          Connect to Neo Mainnet
  --config <file>    Use custom config file
  --output-config    Generate config template and exit
  --version          Show version information
  -h, --help         Show this help message

Examples:
  dotnet run                    # Mainnet RPC node
  dotnet run --testnet          # Testnet RPC node
  dotnet run --config net.json  # Start with custom config

NOTE: Neo.Node provides RPC/API endpoints only. For full P2P networking
      and block synchronization, use Neo.Orleans instead:

      cd src/Neo.Orleans && dotnet run -- --testnet

      Neo.Orleans provides:
      - Full P2P protocol with peer discovery
      - Block synchronization from seed nodes
      - Transaction propagation
      - Consensus participation

Configuration:
  Config files are searched in:
    - Current directory
    - AppContext.BaseDirectory

Default config files:
  - config.json (mainnet)
  - config.testnet.json (testnet)
");
        }

        private static void ShowVersion()
        {
            var version = typeof(Program).Assembly.GetName().Version?.ToString(3) ?? "3.9.2";
            Console.WriteLine($"ModernNeo Node v{version}");
        }

        private static void GenerateConfigTemplate(string network, uint netMagic)
        {
            var fileName = network == "testnet" ? "config.testnet.json" : "config.json";
            var seedList = network == "testnet"
                ? "seed1t.neo.org:20333, seed2t.neo.org:20333, seed3t.neo.org:20333"
                : "seed1.neo.org:10333, seed2.neo.org:10333, seed3.neo.org:10333";

            var config = $@"{{
  ""ApplicationConfiguration"": {{
    ""Logger"": {{
      ""Path"": ""Logs"",
      ""ConsoleOutput"": true,
      ""LogLevel"": ""Info"",
      ""Active"": true
    }},
    ""Storage"": {{
      ""Engine"": ""MemoryStore"",
      ""Path"": ""Data_{{Network}}""
    }},
    ""P2P"": {{
      ""Port"": {(network == "testnet" ? "20333" : "10333")},
      ""EnableCompression"": true,
      ""MinDesiredConnections"": 10,
      ""MaxConnections"": 40
    }},
    ""Rpc"": {{
      ""Enabled"": true,
      ""ListenAddress"": ""http://localhost:{(network == "testnet" ? "20332" : "10332")}/""
    }}
  }},
  ""ProtocolConfiguration"": {{
    ""Network"": {netMagic},
    ""AddressVersion"": 53,
    ""MillisecondsPerBlock"": 15000,
    ""MemoryPoolMaxTransactions"": 50000,
    ""SeedList"": [
      ""{seedList}""
    ]
  }}
}}";
            Console.WriteLine($"Generated config: {fileName}");
            Console.WriteLine();
            Console.WriteLine(config);
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

    public static class NeoSystemNodeFactory
    {
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

    public static class ChannelsConfigFactory
    {
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

    public sealed class NeoSystemNode : IDisposable
    {
        private readonly NeoSystem _system;
        private readonly ChannelsConfig _channelsConfig;
        private bool _started;

        public NeoSystemNode(NeoSystem system, ChannelsConfig channelsConfig)
        {
            _system = system;
            _channelsConfig = channelsConfig;
        }

        public NeoSystem System => _system;
        public ChannelsConfig ChannelsConfig => _channelsConfig;
        public bool IsStarted => _started;

        public void Start()
        {
            if (_started) return;
            _system.StartNode(_channelsConfig);
            _started = true;
        }

        public void Dispose()
        {
            _system.Dispose();
        }
    }

    public class NeoSystemHealthCheck : IHealthCheck
    {
        private readonly NeoSystemNode? _node;

        public NeoSystemHealthCheck(NeoSystemNode? node = null)
        {
            _node = node;
        }

        public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellation = default)
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
