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

using GraphQL;
using GraphQL.MicrosoftDI;
using GraphQL.Server;
using GraphQL.SystemTextJson;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Neo;
using Neo.GraphQL.Types;
using Neo.Observability.Health;
using Neo.Persistence;
using Neo.Persistence.Providers;
using System;
using System.Globalization;

namespace Neo.GraphQL
{
    public class Program
    {
        private const string DefaultConfigFileName = "config.json";

        public static int Main(string[] args)
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
            builder.Configuration
                .AddJsonFile(configPath, optional: false, reloadOnChange: false)
                .AddEnvironmentVariables(prefix: "NEO_")
                .AddCommandLine(args);

            builder.Services
                .AddSingleton(sp =>
                {
                    var config = sp.GetRequiredService<IConfiguration>();
                    var protocolSettings = ProtocolSettings.Load(config.GetSection("ProtocolConfiguration"));

                    var engine = config.GetValue<string>("ApplicationConfiguration:Storage:Engine");
                    if (string.IsNullOrWhiteSpace(engine))
                        engine = nameof(MemoryStore);

                    var provider = StoreFactory.GetStoreProvider(engine);
                    if (provider is null)
                    {
                        var providers = string.Join(", ", StoreFactory.GetProviderNames());
                        throw new InvalidOperationException($"Unknown storage engine '{engine}'. Available: {providers}");
                    }

                    var storagePathTemplate = config.GetValue<string>("ApplicationConfiguration:Storage:Path");
                    var storagePath = ExpandStoragePath(storagePathTemplate, protocolSettings.Network);

                    return new NeoSystem(protocolSettings, provider, storagePath);
                })
                .AddSingleton<NeoSchema>()
                // Node and Block services
                .AddSingleton<Neo.Services.NodeInfo.INodeInfoService, Neo.Services.NodeInfo.NodeInfoService>()
                .AddSingleton<Neo.Services.Blocks.IBlockQueryService, Neo.Services.Blocks.BlockQueryService>()
                .AddSingleton<Neo.Services.Transactions.ITransactionQueryService, Neo.Services.Transactions.TransactionQueryService>()
                // Account and Contract services
                .AddSingleton<Neo.Services.Accounts.IAccountQueryService, Neo.Services.Accounts.AccountQueryService>()
                .AddSingleton<Neo.Services.Contracts.IContractQueryService, Neo.Services.Contracts.ContractQueryService>()
                // Event service for subscriptions
                .AddSingleton<Neo.Services.Events.IBlockchainEventService>(sp =>
                {
                    var system = sp.GetRequiredService<NeoSystem>();
                    return new Neo.Services.Events.BlockchainEventService(system.MemPool);
                })
                // Health check service
                .AddSingleton<IHealthCheckService, HealthCheckService>()
                // Register GraphQL types
                .AddSingleton<BlockType>()
                .AddSingleton<TransactionType>()
                .AddSingleton<SignerType>()
                .AddSingleton<WitnessType>()
                .AddSingleton<AccountBalanceType>()
                .AddSingleton<ContractType>()
                .AddSingleton<SubscriptionType>()
                .AddSingleton<TransactionRemovedType>()
                .AddSingleton<HealthReportType>()
                .AddSingleton<HealthCheckEntryType>()
                .AddSingleton<HealthStatusEnumType>()
                .AddGraphQL(b => b
                    .AddSystemTextJson()
                    .AddSchema<NeoSchema>());

            var app = builder.Build();

            // Enable WebSocket support for GraphQL subscriptions
            app.UseWebSockets();
            app.MapGraphQL("/graphql");

            app.Run();
            return 0;
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

        private static string? ExpandStoragePath(string? template, uint network)
        {
            if (string.IsNullOrWhiteSpace(template)) return template;
            if (!template.Contains("{0}", StringComparison.Ordinal)) return template;
            return string.Format(CultureInfo.InvariantCulture, template, network);
        }
    }
}
