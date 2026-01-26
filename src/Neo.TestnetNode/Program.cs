using Microsoft.Extensions.Configuration;
using Neo.Orleans;
using System;
using System.Threading.Tasks;

namespace Neo.TestnetNode
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("╔═══════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║          ModernNeo Orleans Testnet Node v3.9.2               ║");
            Console.WriteLine("╚═══════════════════════════════════════════════════════════════╝");
            Console.WriteLine();

            var configArg = "--config";
            var configFile = "config.testnet.json";

            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == configArg)
                {
                    configFile = args[i + 1];
                    break;
                }
                if (args[i].StartsWith("--config="))
                {
                    configFile = args[i].Split('=')[1];
                    break;
                }
            }

            Console.WriteLine($"[INFO] Loading configuration from: {configFile}");

            var config = new ConfigurationBuilder()
                .AddJsonFile(configFile, optional: false, reloadOnChange: false)
                .AddEnvironmentVariables(prefix: "NEO_")
                .AddCommandLine(args)
                .Build();

            var protocolSection = config.GetSection("ProtocolConfiguration");
            var network = protocolSection.GetValue<uint>("Network");
            Console.WriteLine($"[INFO] Network ID: {network}");

            Console.WriteLine();
            Console.WriteLine("[INFO] Initializing Orleans silo...");

            var host = new Neo.Orleans.Hosting.NeoOrleansHostBuilder()
                .UseDevelopment()
                .ConfigureFromConfiguration(config)
                .Build();

            await using var system = Neo.Orleans.NeoOrleansSystem.Create(host);

            Console.WriteLine("[INFO] Starting Orleans system...");
            await system.StartAsync();

            var initialHeight = await system.Blockchain.GetHeightAsync();
            Console.WriteLine($"[INFO] Block height: {initialHeight}");

            Console.WriteLine();
            Console.WriteLine("═══════════════════════════════════════════════════════════════");
            Console.WriteLine("  ModernNeo Orleans testnet node started successfully!");
            Console.WriteLine("═══════════════════════════════════════════════════════════════");
            Console.WriteLine();
            Console.WriteLine("[STATUS] P2P: Listening for connections on port 20333");
            Console.WriteLine("[STATUS] Ready to sync blocks from testnet");
            Console.WriteLine();
            Console.WriteLine("Press Ctrl+C to stop...");
            Console.WriteLine();

            await MonitorNodeStatusAsync(system);

            await Task.Delay(-1);
        }

        private static async Task MonitorNodeStatusAsync(Neo.Orleans.NeoOrleansSystem system)
        {
            while (true)
            {
                try
                {
                    await Task.Delay(10000);
                    var height = await system.Blockchain.GetHeightAsync();
                    var mempoolCount = await system.MemoryPool.GetCountAsync();
                    var peersConnected = await system.LocalNode.GetConnectedPeerCountAsync();
                    Console.WriteLine($"[SYNC] Block: {height,8} | MemPool: {mempoolCount,5} | Peers: {peersConnected}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ERROR] {ex.Message}");
                }
            }
        }
    }
}
