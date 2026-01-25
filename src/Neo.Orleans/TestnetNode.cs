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
