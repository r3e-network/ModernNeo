// Copyright (C) 2015-2025 The Neo Project.
//
// OrleansActorBridge.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Neo.Core.Interfaces;
using Neo.IO;
using Neo.Network.P2P.Payloads;
using Neo.Orleans.Hosting;
using Neo.Orleans.Interfaces;

namespace Neo.Orleans.Bridge
{
    /// <summary>
    /// Orleans implementation of the actor bridge.
    /// Provides access to Orleans Grains through the unified bridge interface.
    /// </summary>
    public class OrleansActorBridge : IActorBridge
    {
        private readonly IHost _host;
        private readonly IGrainFactory _grainFactory;
        private readonly OrleansBlockchainBridge _blockchain;
        private readonly OrleansMemoryPoolBridge _memoryPool;
        private readonly OrleansLocalNodeBridge _localNode;
        private bool _isStarted;

        public IBlockchainBridge Blockchain => _blockchain;
        public IMemoryPoolBridge MemoryPool => _memoryPool;
        public ILocalNodeBridge LocalNode => _localNode;
        public bool IsOrleans => true;

        /// <summary>
        /// Creates a new Orleans actor bridge with the specified host.
        /// </summary>
        public OrleansActorBridge(IHost host)
        {
            _host = host;
            _grainFactory = host.Services.GetRequiredService<IGrainFactory>();
            _blockchain = new OrleansBlockchainBridge(_grainFactory);
            _memoryPool = new OrleansMemoryPoolBridge(_grainFactory);
            _localNode = new OrleansLocalNodeBridge(_grainFactory);
        }

        /// <summary>
        /// Creates a development Orleans actor bridge with default settings.
        /// </summary>
        public static OrleansActorBridge CreateDevelopment()
        {
            var host = NeoOrleansHostBuilder.CreateDevelopmentHost();
            return new OrleansActorBridge(host);
        }

        /// <summary>
        /// Creates an Orleans actor bridge with custom configuration.
        /// </summary>
        public static OrleansActorBridge Create(Action<NeoOrleansOptions> configure)
        {
            var host = new NeoOrleansHostBuilder()
                .Configure(configure)
                .Build();
            return new OrleansActorBridge(host);
        }

        public async Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            // Orleans host initialization happens on start
            await Task.CompletedTask;
        }

        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            if (_isStarted) return;

            await _host.StartAsync(cancellationToken);
            _isStarted = true;
        }

        public async Task StopAsync(CancellationToken cancellationToken = default)
        {
            if (!_isStarted) return;

            await _host.StopAsync(cancellationToken);
            _isStarted = false;
        }

        public async ValueTask DisposeAsync()
        {
            await StopAsync();
            _host.Dispose();
        }
    }

    /// <summary>
    /// Orleans implementation of blockchain bridge.
    /// </summary>
    internal class OrleansBlockchainBridge : IBlockchainBridge
    {
        private readonly IGrainFactory _grainFactory;

        public OrleansBlockchainBridge(IGrainFactory grainFactory)
        {
            _grainFactory = grainFactory;
        }

        private IBlockchainGrain GetGrain() => _grainFactory.GetGrain<IBlockchainGrain>(0);

        public Task<uint> GetHeightAsync() => GetGrain().GetHeightAsync();

        public Task<uint> GetHeaderHeightAsync() => GetGrain().GetHeaderHeightAsync();

        public async Task<bool> PersistBlockAsync(IBlockData block)
        {
            var result = await GetGrain().PersistBlockAsync(block);
            return result == BlockVerifyResult.Succeed;
        }

        public Task<IBlockData?> GetBlockByHashAsync(byte[] hash) =>
            GetGrain().GetBlockByHashAsync(hash);

        public Task<IBlockData?> GetBlockByIndexAsync(uint index) =>
            GetGrain().GetBlockByIndexAsync(index);

        public Task<bool> ContainsTransactionAsync(byte[] hash) =>
            GetGrain().ContainsTransactionAsync(hash);
    }

    /// <summary>
    /// Orleans implementation of memory pool bridge.
    /// </summary>
    internal class OrleansMemoryPoolBridge : IMemoryPoolBridge
    {
        private readonly IGrainFactory _grainFactory;

        public OrleansMemoryPoolBridge(IGrainFactory grainFactory)
        {
            _grainFactory = grainFactory;
        }

        private IMemoryPoolGrain GetGrain() => _grainFactory.GetGrain<IMemoryPoolGrain>(0);

        public async Task<bool> AddTransactionAsync(ITransactionData transaction)
        {
            var result = await GetGrain().AddTransactionAsync(OrleansTransactionData.From(transaction));
            return result == MemoryPoolAddResult.Succeed;
        }

        public async Task<IEnumerable<ITransactionData>> GetVerifiedTransactionsAsync(int maxCount)
        {
            var items = await GetGrain().GetVerifiedTransactionsAsync(maxCount);
            if (items.Count == 0)
                return Array.Empty<ITransactionData>();

            var result = new List<ITransactionData>(items.Count);
            foreach (var item in items)
            {
                if (item.SerializedData.Length == 0)
                    continue;

                try
                {
                    var reader = new MemoryReader(item.SerializedData);
                    result.Add(reader.ReadSerializable<Transaction>());
                }
                catch
                {
                    // Ignore invalid serialized payloads.
                }
            }

            return result;
        }

        public Task<int> GetVerifiedCountAsync() => GetGrain().GetVerifiedCountAsync();

        public Task<int> GetUnverifiedCountAsync() => GetGrain().GetUnverifiedCountAsync();

        public Task<bool> ContainsKeyAsync(byte[] hash) => GetGrain().ContainsAsync(hash);
    }

    /// <summary>
    /// Orleans implementation of local node bridge.
    /// </summary>
    internal class OrleansLocalNodeBridge : ILocalNodeBridge
    {
        private readonly IGrainFactory _grainFactory;

        public OrleansLocalNodeBridge(IGrainFactory grainFactory)
        {
            _grainFactory = grainFactory;
        }

        private ILocalNodeGrain GetGrain() => _grainFactory.GetGrain<ILocalNodeGrain>(0);

        public async Task StartAsync(LocalNodeStartConfig config)
        {
            var grainConfig = new LocalNodeConfig(
                Nonce: (uint)Random.Shared.Next(),
                UserAgent: "/Neo:4.0.0/",
                SeedList: Array.Empty<string>(),
                MaxConnections: config.MaxConnections,
                ListenerPort: config.TcpPort,
                NetworkMagic: 0x4F454E, // NEO magic
                ProtocolVersion: 0);

            await GetGrain().InitializeAsync(grainConfig);
            await GetGrain().StartAsync();
        }

        public Task StopAsync() => GetGrain().StopAsync();

        public Task<int> GetConnectedPeerCountAsync() => GetGrain().GetConnectedPeerCountAsync();

        public Task<int> GetUnconnectedPeerCountAsync() => GetGrain().GetUnconnectedPeerCountAsync();

        public Task RelayAsync(byte[] hash, byte inventoryType) =>
            GetGrain().RelayAsync(hash, inventoryType);

        public Task BroadcastAsync(byte[] message) => GetGrain().BroadcastAsync(message);
    }
}
