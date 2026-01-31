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
using Neo.Extensions.Factories;
using Neo.IO;
using Neo.Network.P2P.Payloads;
using Neo.Orleans.Hosting;
using Neo.Orleans.Interfaces;
using Neo.Orleans.Options;
using Neo.Orleans.Services;

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
        private readonly IP2PListener? _listener;
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
            var options = host.Services.GetService<IOrleansOptions>() ?? new OrleansOptions();
            _listener = host.Services.GetService<IP2PListener>();
            _blockchain = new OrleansBlockchainBridge(_grainFactory);
            _memoryPool = new OrleansMemoryPoolBridge(_grainFactory);
            _localNode = new OrleansLocalNodeBridge(_grainFactory, options, _listener);
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
        public static OrleansActorBridge Create(Action<IOrleansOptions> configure)
        {
            var host = new NeoOrleansHostBuilder()
                .UseDevelopment()
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

        public async Task<bool> PersistBlockAsync(Block block)
        {
            var result = await GetGrain().PersistBlockAsync(block);
            return result == BlockVerifyResult.Succeed;
        }

        public Task<Block?> GetBlockByHashAsync(byte[] hash) =>
            GetGrain().GetBlockByHashAsync(hash);

        public Task<Block?> GetBlockByIndexAsync(uint index) =>
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
        private readonly IOrleansOptions _options;
        private readonly IP2PListener? _listener;

        public OrleansLocalNodeBridge(IGrainFactory grainFactory, IOrleansOptions options, IP2PListener? listener)
        {
            _grainFactory = grainFactory;
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _listener = listener;
        }

        private ILocalNodeGrain GetGrain() => _grainFactory.GetGrain<ILocalNodeGrain>(0);

        public async Task StartAsync(LocalNodeStartConfig config)
        {
            var mutableOptions = (OrleansOptions)_options;
            var tcpPort = config.TcpPort > 0 ? config.TcpPort : _options.TcpPort;
            if (tcpPort > 0)
                mutableOptions.TcpPort = tcpPort;
            var wsPort = config.WsPort > 0 ? config.WsPort : _options.WsPort;
            if (wsPort > 0)
            {
                mutableOptions.WsPort = wsPort;
                mutableOptions.WsEnabled = true;
            }
            if (config.MinDesiredConnections > 0)
                mutableOptions.MinDesiredConnections = config.MinDesiredConnections;

            if (_listener != null)
                await _listener.StartAsync(tcpPort, mutableOptions.TcpBindAddress);

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
    }
}
