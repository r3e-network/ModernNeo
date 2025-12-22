// Copyright (C) 2015-2025 The Neo Project.
//
// NeoOrleansSystem.cs file belongs to the neo project and is free
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
using Neo.Orleans.Bridge;
using Neo.Orleans.Hosting;
using Neo.Orleans.Interfaces;

namespace Neo.Orleans
{
    /// <summary>
    /// Orleans-based implementation of the Neo system.
    /// Replaces Akka.NET actors with Orleans Grains for improved cloud-native support.
    /// </summary>
    public class NeoOrleansSystem : IAsyncDisposable
    {
        private readonly IHost _host;
        private readonly IGrainFactory _grainFactory;
        private readonly OrleansActorBridge _bridge;
        private bool _isStarted;
        private bool _isDisposed;

        /// <summary>
        /// Gets the Orleans actor bridge for accessing Grains.
        /// </summary>
        public IActorBridge ActorBridge => _bridge;

        /// <summary>
        /// Gets the blockchain grain interface.
        /// </summary>
        public IBlockchainGrain Blockchain => _grainFactory.GetGrain<IBlockchainGrain>(0);

        /// <summary>
        /// Gets the memory pool grain interface.
        /// </summary>
        public IMemoryPoolGrain MemoryPool => _grainFactory.GetGrain<IMemoryPoolGrain>(0);

        /// <summary>
        /// Gets the local node grain interface.
        /// </summary>
        public ILocalNodeGrain LocalNode => _grainFactory.GetGrain<ILocalNodeGrain>(0);

        /// <summary>
        /// Gets the consensus grain interface.
        /// </summary>
        public IConsensusGrain Consensus => _grainFactory.GetGrain<IConsensusGrain>(0);

        /// <summary>
        /// Gets the task manager grain interface.
        /// </summary>
        public ITaskManagerGrain TaskManager => _grainFactory.GetGrain<ITaskManagerGrain>(0);

        /// <summary>
        /// Gets the transaction router grain interface.
        /// </summary>
        public ITxRouterGrain TxRouter => _grainFactory.GetGrain<ITxRouterGrain>(0);

        /// <summary>
        /// Gets whether the system is currently running.
        /// </summary>
        public bool IsRunning => _isStarted && !_isDisposed;

        /// <summary>
        /// Creates a new NeoOrleansSystem with the specified host.
        /// </summary>
        private NeoOrleansSystem(IHost host)
        {
            _host = host;
            _grainFactory = host.Services.GetRequiredService<IGrainFactory>();
            _bridge = new OrleansActorBridge(host);
        }

        /// <summary>
        /// Creates a development NeoOrleansSystem with default settings.
        /// Uses localhost clustering and in-memory storage.
        /// </summary>
        public static NeoOrleansSystem CreateDevelopment()
        {
            var host = NeoOrleansHostBuilder.CreateDevelopmentHost();
            return new NeoOrleansSystem(host);
        }

        /// <summary>
        /// Creates a NeoOrleansSystem with custom configuration.
        /// Uses localhost clustering by default for development scenarios.
        /// </summary>
        public static NeoOrleansSystem Create(Action<NeoOrleansOptions> configure)
        {
            var host = new NeoOrleansHostBuilder()
                .UseDevelopment() // Enable localhost clustering
                .Configure(configure)
                .Build();
            return new NeoOrleansSystem(host);
        }

        /// <summary>
        /// Creates a NeoOrleansSystem with a pre-built host.
        /// </summary>
        public static NeoOrleansSystem Create(IHost host)
        {
            return new NeoOrleansSystem(host);
        }

        /// <summary>
        /// Starts the Orleans system.
        /// </summary>
        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            if (_isStarted)
                throw new InvalidOperationException("System is already started.");

            if (_isDisposed)
                throw new ObjectDisposedException(nameof(NeoOrleansSystem));

            await _host.StartAsync(cancellationToken);
            _isStarted = true;
        }

        /// <summary>
        /// Starts the local node with the specified configuration.
        /// </summary>
        public async Task StartNodeAsync(LocalNodeConfig config, CancellationToken cancellationToken = default)
        {
            if (!_isStarted)
                await StartAsync(cancellationToken);

            await LocalNode.InitializeAsync(config);
            await LocalNode.StartAsync();
        }

        /// <summary>
        /// Stops the local node.
        /// </summary>
        public async Task StopNodeAsync()
        {
            if (_isStarted)
            {
                await LocalNode.StopAsync();
            }
        }

        /// <summary>
        /// Gets the current blockchain height.
        /// </summary>
        public Task<uint> GetBlockchainHeightAsync() => Blockchain.GetHeightAsync();

        /// <summary>
        /// Gets the current header height.
        /// </summary>
        public Task<uint> GetHeaderHeightAsync() => Blockchain.GetHeaderHeightAsync();

        /// <summary>
        /// Gets the count of verified transactions in the memory pool.
        /// </summary>
        public Task<int> GetMemoryPoolCountAsync() => MemoryPool.GetVerifiedCountAsync();

        /// <summary>
        /// Gets the count of connected peers.
        /// </summary>
        public Task<int> GetConnectedPeerCountAsync() => LocalNode.GetConnectedPeerCountAsync();

        /// <summary>
        /// Gets a block by its index.
        /// </summary>
        public Task<IBlockData?> GetBlockAsync(uint index) => Blockchain.GetBlockByIndexAsync(index);

        /// <summary>
        /// Gets a block by its hash.
        /// </summary>
        public Task<IBlockData?> GetBlockAsync(byte[] hash) => Blockchain.GetBlockByHashAsync(hash);

        /// <summary>
        /// Persists a block to the blockchain.
        /// </summary>
        public Task<BlockVerifyResult> PersistBlockAsync(IBlockData block) =>
            Blockchain.PersistBlockAsync(block);

        /// <summary>
        /// Adds a transaction to the memory pool.
        /// </summary>
        public Task<MemoryPoolAddResult> AddTransactionAsync(ITransactionData transaction) =>
            MemoryPool.AddTransactionAsync(transaction);

        /// <summary>
        /// Relays an inventory item to connected peers.
        /// </summary>
        public Task RelayAsync(byte[] hash, byte inventoryType) =>
            LocalNode.RelayAsync(hash, inventoryType);

        /// <summary>
        /// Broadcasts a message to all connected peers.
        /// </summary>
        public Task BroadcastAsync(byte[] message) =>
            LocalNode.BroadcastAsync(message);

        /// <summary>
        /// Gets the blockchain state summary.
        /// </summary>
        public Task<BlockchainStateSummary> GetBlockchainStateAsync() =>
            Blockchain.GetStateSummaryAsync();

        /// <summary>
        /// Gets the local node state summary.
        /// </summary>
        public Task<LocalNodeStateSummary> GetLocalNodeStateAsync() =>
            LocalNode.GetStateSummaryAsync();

        /// <summary>
        /// Stops the Orleans system.
        /// </summary>
        public async Task StopAsync(CancellationToken cancellationToken = default)
        {
            if (!_isStarted || _isDisposed)
                return;

            await StopNodeAsync();
            await _host.StopAsync(cancellationToken);
            _isStarted = false;
        }

        /// <summary>
        /// Disposes the Orleans system.
        /// </summary>
        public async ValueTask DisposeAsync()
        {
            if (_isDisposed)
                return;

            await StopAsync();
            await _bridge.DisposeAsync();
            _host.Dispose();
            _isDisposed = true;

            GC.SuppressFinalize(this);
        }
    }
}
