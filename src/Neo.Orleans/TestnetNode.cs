// Copyright (C) 2015-2025 The Neo Project.
//
// TestnetNode.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Microsoft.Extensions.Configuration;
using Neo.Orleans;
using System;
using System.Threading.Tasks;

namespace Neo.Orleans.TestnetNode
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("Starting ModernNeo Orleans Testnet Node...");

            var config = new ConfigurationBuilder()
                .AddJsonFile("../Neo.Node/config.testnet.json")
                .Build();

            var host = new Neo.Orleans.Hosting.NeoOrleansHostBuilder()
                .UseDevelopment()
                .ConfigureFromConfiguration(config)
                .Build();

            await using var system = Neo.Orleans.NeoOrleansSystem.Create(host);

            Console.WriteLine("Starting Orleans system...");
            await system.StartAsync();

            Console.WriteLine("ModernNeo Orleans node started successfully!");
            Console.WriteLine("Press Ctrl+C to stop...");

            // Keep running
            await Task.Delay(-1);
        }
    }
}
