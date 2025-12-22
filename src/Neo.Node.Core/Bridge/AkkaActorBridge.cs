// Copyright (C) 2015-2025 The Neo Project.
//
// AkkaActorBridge.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Akka.Actor;
using Akka.IO;
using Neo.Core.Interfaces;
using Neo.Ledger;
using Neo.Network.P2P;
using Neo.Network.P2P.Payloads;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Neo.Bridge
{
    /// <summary>
    /// Akka.NET implementation of the runtime bridge.
    /// Wraps existing Akka actors in the unified runtime interface.
    /// </summary>
    public class AkkaActorBridge : INeoSystemRuntime
    {
        private readonly NeoSystem _system;
        private readonly AkkaBlockchainRuntime _blockchain;
        private readonly AkkaMemoryPoolRuntime _memoryPool;
        private readonly AkkaLocalNodeRuntime _localNode;
        private readonly AkkaTaskManagerRuntime _taskManager;
        private bool _isStarted;

        public IBlockchainRuntime Blockchain => _blockchain;
        public IMemoryPoolRuntime MemoryPool => _memoryPool;
        public ILocalNodeRuntime LocalNode => _localNode;
        public ITaskManagerRuntime TaskManager => _taskManager;
        public IConsensusRuntime? Consensus => null; // Consensus is managed separately in Akka via ConsensusService
        public bool IsOrleans => false;

        /// <summary>
        /// Gets the underlying Akka ActorSystem.
        /// </summary>
        public ActorSystem ActorSystem => _system.ActorSystem;

        /// <summary>
        /// Gets the Blockchain actor reference.
        /// </summary>
        public IActorRef BlockchainActor => _system.Blockchain;

        /// <summary>
        /// Gets the LocalNode actor reference.
        /// </summary>
        public IActorRef LocalNodeActor => _system.LocalNode;

        /// <summary>
        /// Gets the TaskManager actor reference.
        /// </summary>
        public IActorRef TaskManagerActor => _system.TaskManager;

        /// <summary>
        /// Gets the TxRouter actor reference.
        /// </summary>
        public IActorRef TxRouterActor => _system.TxRouter;

        /// <summary>
        /// Creates a new Akka actor bridge for the specified NeoSystem.
        /// </summary>
        /// <param name="system">The NeoSystem instance.</param>
        public AkkaActorBridge(NeoSystem system)
        {
            _system = system ?? throw new ArgumentNullException(nameof(system));
            _blockchain = new AkkaBlockchainRuntime(system);
            _memoryPool = new AkkaMemoryPoolRuntime(system);
            _localNode = new AkkaLocalNodeRuntime(system);
            _taskManager = new AkkaTaskManagerRuntime(system);
        }

        public Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            // Akka actors are initialized in NeoSystem constructor
            return Task.CompletedTask;
        }

        public Task StartAsync(CancellationToken cancellationToken = default)
        {
            if (_isStarted) return Task.CompletedTask;
            _isStarted = true;
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken = default)
        {
            if (!_isStarted) return Task.CompletedTask;
            _isStarted = false;
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            // NeoSystem handles actor disposal
            return ValueTask.CompletedTask;
        }
    }

    /// <summary>
    /// Akka implementation of blockchain runtime.
    /// </summary>
    internal class AkkaBlockchainRuntime : IBlockchainRuntime
    {
        private static readonly TimeSpan DefaultAskTimeout = TimeSpan.FromSeconds(30);

        private readonly NeoSystem _system;

        public AkkaBlockchainRuntime(NeoSystem system)
        {
            _system = system;
        }

        public Task<uint> GetHeightAsync()
        {
            // Height is accessed via NativeContract.Ledger through StoreView
            var height = SmartContract.Native.NativeContract.Ledger.CurrentIndex(_system.StoreView);
            return Task.FromResult(height);
        }

        public Task<uint> GetHeaderHeightAsync()
        {
            // Header height accessed via HeaderCache
            var height = _system.HeaderCache.Count > 0
                ? _system.HeaderCache.Last?.Index ?? 0
                : SmartContract.Native.NativeContract.Ledger.CurrentIndex(_system.StoreView);
            return Task.FromResult(height);
        }

        public async Task<bool> PersistBlockAsync(IBlockData block)
        {
            if (block is not Block neoBlock)
                return false;

            try
            {
                // Sending a Block message to the Blockchain actor persists it (without relaying).
                var result = await _system.Blockchain.Ask<Blockchain.RelayResult>(neoBlock, DefaultAskTimeout);
                return result.Result == VerifyResult.Succeed;
            }
            catch
            {
                return false;
            }
        }

        public Task<IBlockData?> GetBlockByHashAsync(byte[] hash)
        {
            // Block retrieval via StoreView
            var uint256Hash = new UInt256(hash);
            var block = SmartContract.Native.NativeContract.Ledger.GetBlock(_system.StoreView, uint256Hash);
            return Task.FromResult<IBlockData?>(block);
        }

        public Task<IBlockData?> GetBlockByIndexAsync(uint index)
        {
            // Block retrieval via StoreView
            var block = SmartContract.Native.NativeContract.Ledger.GetBlock(_system.StoreView, index);
            return Task.FromResult<IBlockData?>(block);
        }

        public Task<byte[]?> GetBlockHashByIndexAsync(uint index)
        {
            var hash = SmartContract.Native.NativeContract.Ledger.GetBlockHash(_system.StoreView, index);
            return Task.FromResult(hash?.GetSpan().ToArray());
        }

        public Task<bool> ContainsBlockAsync(byte[] hash)
        {
            var uint256Hash = new UInt256(hash);
            var result = SmartContract.Native.NativeContract.Ledger.ContainsBlock(_system.StoreView, uint256Hash);
            return Task.FromResult(result);
        }

        public Task<bool> ContainsTransactionAsync(byte[] hash)
        {
            var uint256Hash = new UInt256(hash);
            var result = _system.ContainsTransaction(uint256Hash);
            return Task.FromResult(result != ContainsTransactionType.NotExist);
        }

        public void Tell<T>(T message) where T : class
        {
            _system.Blockchain.Tell(message);
        }

        public async Task<TResponse> AskAsync<TRequest, TResponse>(TRequest message, CancellationToken cancellationToken = default)
            where TRequest : class
            where TResponse : class
        {
            return await _system.Blockchain.Ask<TResponse>(message, DefaultAskTimeout, cancellationToken);
        }
    }

    /// <summary>
    /// Akka implementation of memory pool runtime.
    /// </summary>
    internal class AkkaMemoryPoolRuntime : IMemoryPoolRuntime
    {
        private static readonly TimeSpan DefaultAskTimeout = TimeSpan.FromSeconds(30);

        private readonly NeoSystem _system;

        public AkkaMemoryPoolRuntime(NeoSystem system)
        {
            _system = system;
        }

        public async Task<bool> AddTransactionAsync(ITransactionData transaction)
        {
            if (transaction is not Transaction tx)
                return false;

            try
            {
                // Go through the TxRouter to perform state-independent verification first.
                // Relay is set to false to match the intent of "add to pool" (no broadcast side effects).
                var result = await _system.TxRouter.Ask<Blockchain.RelayResult>(new PreverifyTransaction(tx, Relay: false), DefaultAskTimeout);
                return result.Result is VerifyResult.Succeed or VerifyResult.AlreadyInPool;
            }
            catch
            {
                return false;
            }
        }

        public Task<IEnumerable<ITransactionData>> GetVerifiedTransactionsAsync(int maxCount)
        {
            var transactions = _system.MemPool.GetVerifiedTransactions()
                .Take(maxCount)
                .Cast<ITransactionData>();
            return Task.FromResult(transactions);
        }

        public Task<int> GetVerifiedCountAsync()
        {
            return Task.FromResult(_system.MemPool.VerifiedCount);
        }

        public Task<int> GetUnverifiedCountAsync()
        {
            return Task.FromResult(_system.MemPool.UnVerifiedCount);
        }

        public Task<bool> ContainsKeyAsync(byte[] hash)
        {
            var uint256Hash = new UInt256(hash);
            return Task.FromResult(_system.MemPool.ContainsKey(uint256Hash));
        }
    }

    /// <summary>
    /// Akka implementation of local node runtime.
    /// </summary>
    internal class AkkaLocalNodeRuntime : ILocalNodeRuntime
    {
        private static readonly TimeSpan DefaultAskTimeout = TimeSpan.FromSeconds(5);

        private readonly NeoSystem _system;

        public AkkaLocalNodeRuntime(NeoSystem system)
        {
            _system = system;
        }

        public Task StartAsync(RuntimeNodeConfig config)
        {
            var channelsConfig = new ChannelsConfig
            {
                Tcp = new System.Net.IPEndPoint(System.Net.IPAddress.Any, config.TcpPort),
                MinDesiredConnections = config.MinDesiredConnections,
                MaxConnections = config.MaxConnections,
                MaxConnectionsPerAddress = config.MaxConnectionsPerAddress
            };
            _system.StartNode(channelsConfig);
            return Task.CompletedTask;
        }

        public Task StopAsync()
        {
            // LocalNode stopping is handled by NeoSystem.Dispose()
            return Task.CompletedTask;
        }

        public Task<int> GetConnectedPeerCountAsync()
        {
            return GetLocalNodeCountAsync(static node => node.ConnectedCount);
        }

        public Task<int> GetUnconnectedPeerCountAsync()
        {
            return GetLocalNodeCountAsync(static node => node.UnconnectedCount);
        }

        public Task RelayAsync(byte[] hash, byte inventoryType)
        {
            if (hash is null || hash.Length != UInt256.Length)
                return Task.CompletedTask;

            try
            {
                var key = new UInt256(hash);
                if (((INeoSystemContext)_system).TryGetRelay(key, out var cached))
                {
                    _system.LocalNode.Tell(new LocalNode.RelayDirectly(cached));
                    return Task.CompletedTask;
                }

                IInventory? inventory = (InventoryType)inventoryType switch
                {
                    InventoryType.TX => TryGetTransaction(key),
                    InventoryType.Block => SmartContract.Native.NativeContract.Ledger.GetBlock(_system.StoreView, key),
                    InventoryType.Extensible => null,
                    _ => null
                };

                if (inventory is null)
                    return Task.CompletedTask;

                _system.LocalNode.Tell(new LocalNode.RelayDirectly(inventory));
            }
            catch
            {
                // Best-effort relay: ignore failures.
            }

            return Task.CompletedTask;
        }

        public Task BroadcastAsync(byte[] message)
        {
            if (message is null || message.Length == 0)
                return Task.CompletedTask;

            try
            {
                // Best-effort: interpret the input as a serialized Neo P2P Message and broadcast it.
                // If deserialization fails, ignore to keep this API non-throwing.
                if (Message.TryDeserialize(ByteString.FromBytes(message), out var msg) > 0 && msg is not null)
                {
                    _system.LocalNode.Tell(msg);
                }
            }
            catch
            {
                // Best-effort broadcast: ignore failures.
            }

            return Task.CompletedTask;
        }

        public void Tell<T>(T message) where T : class
        {
            _system.LocalNode.Tell(message);
        }

        private async Task<int> GetLocalNodeCountAsync(Func<LocalNode, int> selector)
        {
            try
            {
                var node = await _system.LocalNode.Ask<LocalNode>(new LocalNode.GetInstance(), DefaultAskTimeout);
                return selector(node);
            }
            catch
            {
                return 0;
            }
        }

        private Transaction? TryGetTransaction(UInt256 hash)
        {
            if (_system.MemPool.TryGetValue(hash, out var tx))
                return tx;

            return SmartContract.Native.NativeContract.Ledger.GetTransaction(_system.StoreView, hash);
        }
    }

    /// <summary>
    /// Akka implementation of task manager runtime.
    /// </summary>
    internal class AkkaTaskManagerRuntime : ITaskManagerRuntime
    {
        private readonly NeoSystem _system;

        public AkkaTaskManagerRuntime(NeoSystem system)
        {
            _system = system;
        }

        public Task RegisterNodeAsync(string nodeId)
        {
            // Node registration is handled internally by TaskManager actor
            // when RemoteNode actors connect
            return Task.CompletedTask;
        }

        public Task UnregisterNodeAsync(string nodeId)
        {
            // Node unregistration is handled internally by TaskManager actor
            // when RemoteNode actors disconnect
            return Task.CompletedTask;
        }

        public Task RequestBlocksAsync(uint startIndex, int count)
        {
            // Block requests are typically initiated via TaskManager messages
            // This is coordinated internally by TaskManager actor
            return Task.CompletedTask;
        }

        public Task RequestHeadersAsync(uint startIndex)
        {
            // Header requests are typically initiated via TaskManager messages
            // This is coordinated internally by TaskManager actor
            return Task.CompletedTask;
        }

        public Task<int> GetPendingTaskCountAsync()
        {
            // TaskManager maintains internal task tracking
            // Would require Ask pattern to query pending tasks
            return Task.FromResult(0);
        }

        public Task NotifyHeadersReceivedAsync(int headerCount)
        {
            // Headers notification is handled via Tell pattern
            return Task.CompletedTask;
        }

        public void Tell<T>(T message) where T : class
        {
            _system.TaskManager.Tell(message);
        }
    }
}
