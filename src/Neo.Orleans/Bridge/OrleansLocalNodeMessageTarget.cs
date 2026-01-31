// Copyright (C) 2015-2025 The Neo Project.
//
// OrleansLocalNodeMessageTarget.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo;
using Neo.Extensions.Factories;
using Neo.Network.P2P;
using Neo.Network.P2P.Payloads;
using Neo.Orleans.Hosting;
using Neo.Orleans.Interfaces;
using Neo.Orleans.Options;
using Orleans;
using System;
using System.Threading.Tasks;

namespace Neo.Orleans.Bridge
{
    internal sealed class OrleansLocalNodeMessageTarget : ISystemMessageTarget
    {
        private readonly IGrainFactory _grainFactory;
        private readonly IProtocolSettings _settings;
        private readonly OrleansOptions _options;
        private readonly uint _nonce;
        private bool _enableCompression;

        public OrleansLocalNodeMessageTarget(IGrainFactory grainFactory, IProtocolSettings settings, OrleansOptions options)
        {
            _grainFactory = grainFactory;
            _settings = settings;
            _options = options;
            _nonce = RandomNumberFactory.NextUInt32();
            _enableCompression = true;
        }

        public void Tell(object message)
        {
            _ = HandleAsync(message);
        }

        public Task<TResponse> Ask<TResponse>(object message, TimeSpan? timeout = null)
        {
            throw new NotSupportedException("Ask pattern is not supported for Orleans local node.");
        }

        private async Task HandleAsync(object message)
        {
            switch (message)
            {
                case ChannelsConfig config:
                    await StartLocalNodeAsync(config);
                    break;
                case LocalNode.RelayDirectly relay:
                    await RelayDirectlyAsync(relay.Inventory);
                    break;
                case LocalNode.SendDirectly send:
                    await SendDirectlyAsync(send.Inventory);
                    break;
                case Message msg:
                    await BroadcastMessageAsync(msg);
                    break;
            }
        }

        private async Task StartLocalNodeAsync(ChannelsConfig config)
        {
            var localNode = _grainFactory.GetGrain<ILocalNodeGrain>(0);
            var port = config.Tcp?.Port ?? _options.TcpPort;
            _enableCompression = config.EnableCompression;

            var localConfig = new LocalNodeConfig(
                _nonce,
                _options.UserAgent,
                _options.SeedList,
                config.MaxConnections,
                port,
                _settings.Network,
                _options.ProtocolVersion)
            {
                MaxConnectionsPerAddress = config.MaxConnectionsPerAddress,
                MinDesiredConnections = config.MinDesiredConnections,
                EnableCompression = config.EnableCompression
            };

            await localNode.InitializeAsync(localConfig);
            await localNode.StartAsync();
        }

        private Task RelayDirectlyAsync(IInventory inventory)
        {
            var localNode = _grainFactory.GetGrain<ILocalNodeGrain>(0);
            if (inventory is Block block)
                return localNode.RelayBlockAsync(block.Hash.GetSpan().ToArray(), block.Index);

            return localNode.RelayAsync(inventory.Hash.GetSpan().ToArray(), (byte)inventory.InventoryType);
        }

        private Task SendDirectlyAsync(IInventory inventory)
        {
            var message = Message.Create((MessageCommand)inventory.InventoryType, inventory);
            return BroadcastMessageAsync(message);
        }

        private Task BroadcastMessageAsync(Message message)
        {
            var localNode = _grainFactory.GetGrain<ILocalNodeGrain>(0);
            return localNode.BroadcastAsync(message.ToArray(_enableCompression));
        }
    }
}
#pragma warning restore CS0618
