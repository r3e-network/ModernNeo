// Copyright (C) 2015-2025 The Neo Project.
//
// OrleansSystemRuntime.cs file belongs to the neo project and is free
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
using Neo.Extensions.Factories;
using Neo.IO;
using Neo.Network.P2P;
using Neo.Network.P2P.Payloads;
using Neo.Orleans.Hosting;
using Neo.Orleans.Interfaces;
using Neo.Orleans.Services;

namespace Neo.Orleans.Bridge
{
    /// <summary>
    /// Orleans implementation of INeoSystemRuntime.
    /// Provides unified runtime interface for Orleans-based Neo node.
    /// </summary>
    public class OrleansSystemRuntime : INeoSystemRuntime
    {
        private readonly IHost _host;
        private readonly IGrainFactory _grainFactory;
        private readonly OrleansBlockchainRuntime _blockchain;
        private readonly OrleansMemoryPoolRuntime _memoryPool;
        private readonly OrleansLocalNodeRuntime _localNode;
        private readonly OrleansTaskManagerRuntime _taskManager;
        private readonly OrleansConsensusRuntime _consensus;
        private readonly IP2PListener? _listener;
        private readonly NeoOrleansOptions _options;
        private bool _isStarted;

        public IBlockchainRuntime Blockchain => _blockchain;
        public IMemoryPoolRuntime MemoryPool => _memoryPool;
        public ILocalNodeRuntime LocalNode => _localNode;
        public ITaskManagerRuntime TaskManager => _taskManager;
        public IConsensusRuntime? Consensus => _consensus;
        public bool IsOrleans => true;

        /// <summary>
        /// Creates a new Orleans system runtime with the specified host.
        /// </summary>
        public OrleansSystemRuntime(IHost host)
        {
            _host = host ?? throw new ArgumentNullException(nameof(host));
            _grainFactory = host.Services.GetRequiredService<IGrainFactory>();
            _options = host.Services.GetService<NeoOrleansOptions>() ?? new NeoOrleansOptions();
            _listener = host.Services.GetService<IP2PListener>();
            _blockchain = new OrleansBlockchainRuntime(_grainFactory);
            _memoryPool = new OrleansMemoryPoolRuntime(_grainFactory);
            _localNode = new OrleansLocalNodeRuntime(_grainFactory, _options, _listener);
            _taskManager = new OrleansTaskManagerRuntime(_grainFactory, _options);
            _consensus = new OrleansConsensusRuntime(_grainFactory);
        }

        /// <summary>
        /// Creates a development Orleans runtime with default settings.
        /// </summary>
        public static OrleansSystemRuntime CreateDevelopment()
        {
            var host = NeoOrleansHostBuilder.CreateDevelopmentHost();
            return new OrleansSystemRuntime(host);
        }

        /// <summary>
        /// Creates an Orleans runtime with custom configuration.
        /// </summary>
        public static OrleansSystemRuntime Create(Action<NeoOrleansOptions> configure)
        {
            var host = new NeoOrleansHostBuilder()
                .UseDevelopment()
                .Configure(configure)
                .Build();
            return new OrleansSystemRuntime(host);
        }

        public Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            // Orleans host initialization happens on start
            return Task.CompletedTask;
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

            if (_listener != null)
                await _listener.StopAsync(cancellationToken);

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
    /// Orleans implementation of blockchain runtime.
    /// </summary>
    internal class OrleansBlockchainRuntime : IBlockchainRuntime
    {
        private readonly IGrainFactory _grainFactory;

        public OrleansBlockchainRuntime(IGrainFactory grainFactory)
        {
            _grainFactory = grainFactory;
        }

        private IBlockchainGrain GetGrain() => _grainFactory.GetGrain<IBlockchainGrain>(0);

        public Task<uint> GetHeightAsync() => GetGrain().GetHeightAsync();

        public Task<uint> GetHeaderHeightAsync() => GetGrain().GetHeaderHeightAsync();

        public async Task<bool> PersistBlockAsync(IBlockData block)
        {
            if (block is not Block blockPayload)
                throw new ArgumentException("Block must be of type Neo.Network.P2P.Payloads.Block", nameof(block));

            var result = await GetGrain().PersistBlockAsync(blockPayload);
            return result == BlockVerifyResult.Succeed;
        }

        public async Task<IBlockData?> GetBlockByHashAsync(byte[] hash)
        {
            return await GetGrain().GetBlockByHashAsync(hash);
        }

        public async Task<IBlockData?> GetBlockByIndexAsync(uint index)
        {
            return await GetGrain().GetBlockByIndexAsync(index);
        }

        public async Task<byte[]?> GetBlockHashByIndexAsync(uint index)
        {
            return await GetGrain().GetBlockHashByIndexAsync(index);
        }

        public Task<bool> ContainsBlockAsync(byte[] hash) =>
            GetGrain().ContainsBlockAsync(hash);

        public Task<bool> ContainsTransactionAsync(byte[] hash) =>
            GetGrain().ContainsTransactionAsync(hash);

        public void Tell<T>(T message) where T : class
        {
            // Orleans uses async patterns; fire-and-forget via Task.Run
            // In production, consider using Orleans streams for pub/sub
            _ = Task.Run(async () =>
            {
                // Handle specific message types that need processing
                if (message is Block block)
                {
                    await GetGrain().PersistBlockAsync(block);
                }
            });
        }

        public async Task<TResponse> AskAsync<TRequest, TResponse>(TRequest message, CancellationToken cancellationToken = default)
            where TRequest : class
            where TResponse : class
        {
            // Map request types to grain operations
            // This is a simplified implementation; extend as needed
            throw new NotSupportedException($"Ask pattern for {typeof(TRequest).Name} not implemented in Orleans runtime");
        }
    }

    /// <summary>
    /// Orleans implementation of memory pool runtime.
    /// </summary>
    internal class OrleansMemoryPoolRuntime : IMemoryPoolRuntime
    {
        private readonly IGrainFactory _grainFactory;

        public OrleansMemoryPoolRuntime(IGrainFactory grainFactory)
        {
            _grainFactory = grainFactory;
        }

        private IMemoryPoolGrain GetGrain() => _grainFactory.GetGrain<IMemoryPoolGrain>(0);

        public async Task<bool> AddTransactionAsync(ITransactionData transaction)
        {
            var result = await GetGrain().AddTransactionAsync(transaction);
            return result == MemoryPoolAddResult.Succeed;
        }

        public async Task<IEnumerable<ITransactionData>> GetVerifiedTransactionsAsync(int maxCount)
        {
            var items = await GetGrain().GetVerifiedTransactionsAsync(maxCount);
            if (items.Count == 0)
                return Array.Empty<ITransactionData>();

            var results = new List<ITransactionData>(items.Count);
            foreach (var item in items)
            {
                if (item.SerializedData.Length == 0)
                    continue;

                try
                {
                    var reader = new MemoryReader(item.SerializedData);
                    results.Add(reader.ReadSerializable<Transaction>());
                }
                catch (FormatException)
                {
                    // Skip malformed entries to avoid failing the entire batch.
                }
            }

            return results;
        }

        public Task<int> GetVerifiedCountAsync() => GetGrain().GetVerifiedCountAsync();

        public Task<int> GetUnverifiedCountAsync() => GetGrain().GetUnverifiedCountAsync();

        public Task<bool> ContainsKeyAsync(byte[] hash) => GetGrain().ContainsAsync(hash);
    }

    /// <summary>
    /// Orleans implementation of local node runtime.
    /// </summary>
    internal class OrleansLocalNodeRuntime : ILocalNodeRuntime
    {
        private readonly IGrainFactory _grainFactory;
        private readonly NeoOrleansOptions _options;
        private readonly IP2PListener? _listener;

        public OrleansLocalNodeRuntime(IGrainFactory grainFactory, NeoOrleansOptions options, IP2PListener? listener)
        {
            _grainFactory = grainFactory;
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _listener = listener;
        }

        private ILocalNodeGrain GetGrain() => _grainFactory.GetGrain<ILocalNodeGrain>(0);

        public async Task StartAsync(RuntimeNodeConfig config)
        {
            var tcpPort = config.TcpPort > 0 ? config.TcpPort : _options.TcpPort;
            if (tcpPort > 0)
                _options.TcpPort = tcpPort;
            var wsPort = config.WsPort > 0 ? config.WsPort : _options.WsPort;
            if (wsPort > 0)
            {
                _options.WsPort = wsPort;
                _options.WsEnabled = true;
            }
            if (config.MinDesiredConnections > 0)
                _options.MinDesiredConnections = config.MinDesiredConnections;

            if (_listener != null)
                await _listener.StartAsync(tcpPort, _options.TcpBindAddress);

            var maxConnections = config.MaxConnections > 0 ? config.MaxConnections : _options.MaxConnections;
            var maxConnectionsPerAddress = config.MaxConnectionsPerAddress > 0
                ? config.MaxConnectionsPerAddress
                : _options.MaxConnectionsPerAddress;
            var minDesiredConnections = config.MinDesiredConnections > 0
                ? config.MinDesiredConnections
                : _options.MinDesiredConnections;
            minDesiredConnections = Math.Min(minDesiredConnections, maxConnections);
            var grainConfig = new LocalNodeConfig(
                Nonce: RandomNumberFactory.NextUInt32(),
                UserAgent: _options.UserAgent,
                SeedList: _options.SeedList,
                MaxConnections: maxConnections,
                ListenerPort: tcpPort,
                NetworkMagic: _options.NetworkMagic,
                ProtocolVersion: _options.ProtocolVersion)
            {
                MaxConnectionsPerAddress = maxConnectionsPerAddress,
                MinDesiredConnections = minDesiredConnections,
                EnableCompression = _options.EnableCompression
            };

            await GetGrain().InitializeAsync(grainConfig);
            await GetGrain().StartAsync();
        }

        public async Task StopAsync()
        {
            if (_listener != null)
                await _listener.StopAsync();

            await GetGrain().StopAsync();
        }

        public Task<int> GetConnectedPeerCountAsync() => GetGrain().GetConnectedPeerCountAsync();

        public Task<int> GetUnconnectedPeerCountAsync() => GetGrain().GetUnconnectedPeerCountAsync();

        public Task RelayAsync(byte[] hash, byte inventoryType) =>
            GetGrain().RelayAsync(hash, inventoryType);

        public Task BroadcastAsync(byte[] message) => GetGrain().BroadcastAsync(message);

        public void Tell<T>(T message) where T : class
        {
            // Orleans uses async patterns; fire-and-forget
            _ = Task.Run(async () =>
            {
                if (message is byte[] data)
                {
                    await GetGrain().BroadcastAsync(data);
                }
            });
        }
    }

    /// <summary>
    /// Orleans implementation of task manager runtime.
    /// </summary>
    internal class OrleansTaskManagerRuntime : ITaskManagerRuntime
    {
        private readonly IGrainFactory _grainFactory;
        private readonly NeoOrleansOptions _options;

        public OrleansTaskManagerRuntime(IGrainFactory grainFactory, NeoOrleansOptions options)
        {
            _grainFactory = grainFactory;
            _options = options ?? throw new ArgumentNullException(nameof(options));
        }

        private ITaskManagerGrain GetGrain() => _grainFactory.GetGrain<ITaskManagerGrain>(0);

        public Task RegisterNodeAsync(string nodeId) =>
            GetGrain().RegisterSessionAsync(nodeId, 0, _options.UserAgent);

        public Task UnregisterNodeAsync(string nodeId) =>
            GetGrain().UnregisterSessionAsync(nodeId);

        public async Task RequestBlocksAsync(uint startIndex, int count)
        {
            if (count <= 0)
                return;

            var blockchain = _grainFactory.GetGrain<IBlockchainGrain>(0);
            var hashes = new List<byte[]>();
            for (uint i = 0; i < count; i++)
            {
                var hash = await blockchain.GetBlockHashByIndexAsync(startIndex + i);
                if (hash != null && hash.Length > 0)
                    hashes.Add(hash);
            }

            if (hashes.Count == 0)
                return;

            await GetGrain().AddTasksAsync(hashes, (byte)InventoryType.Block);
        }

        public async Task RequestHeadersAsync(uint startIndex)
        {
            var payload = GetBlockByIndexPayload.Create(startIndex);
            var message = Message.Create(MessageCommand.GetHeaders, payload).ToArray(_options.EnableCompression);
            var localNode = _grainFactory.GetGrain<ILocalNodeGrain>(0);
            await localNode.BroadcastAsync(message);
        }

        public async Task<int> GetPendingTaskCountAsync()
        {
            var summary = await GetGrain().GetStateSummaryAsync();
            return summary.PendingTaskCount;
        }

        public Task NotifyHeadersReceivedAsync(int headerCount)
        {
            // Headers received notification - could trigger additional sync logic
            return Task.CompletedTask;
        }

        public void Tell<T>(T message) where T : class
        {
            // Orleans uses async patterns; fire-and-forget
            // Specific message handling would be implemented based on message types
        }
    }

    /// <summary>
    /// Orleans implementation of consensus runtime.
    /// </summary>
    internal class OrleansConsensusRuntime : IConsensusRuntime
    {
        private readonly IGrainFactory _grainFactory;

        public OrleansConsensusRuntime(IGrainFactory grainFactory)
        {
            _grainFactory = grainFactory;
        }

        private IConsensusGrain GetGrain() => _grainFactory.GetGrain<IConsensusGrain>(0);

        public Task InitializeAsync(int myIndex, int validatorCount) =>
            GetGrain().InitializeAsync(myIndex, validatorCount);

        public Task StartAsync() => GetGrain().StartAsync();

        public Task StopAsync() => GetGrain().StopAsync();

        public Task OnMessageAsync(byte[] message, string senderAddress) =>
            GetGrain().OnConsensusMessageAsync(message, senderAddress);

        public Task<byte> GetViewNumberAsync() => GetGrain().GetViewNumberAsync();

        public async Task<uint> GetBlockIndexAsync()
        {
            var state = await GetGrain().GetStateAsync();
            return state.BlockIndex;
        }

        public Task<bool> IsPrimaryAsync() => GetGrain().IsPrimaryAsync();

        public async Task<bool> IsRunningAsync()
        {
            var state = await GetGrain().GetStateAsync();
            return state.Phase != Interfaces.ConsensusPhase.Initial;
        }

        public void Tell<T>(T message) where T : class
        {
            // Orleans uses async patterns; fire-and-forget
            _ = Task.Run(async () =>
            {
                if (message is byte[] data)
                {
                    // Consensus messages typically include sender info
                    // For fire-and-forget, use empty sender
                    await GetGrain().OnConsensusMessageAsync(data, string.Empty);
                }
            });
        }
    }
}
