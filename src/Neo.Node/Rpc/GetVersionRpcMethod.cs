// Copyright (C) 2015-2025 The Neo Project.
//
// GetVersionRpcMethod.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Extensions.Factories;
using Neo.Json;
using Neo.RPC;
using System;
using System.Reflection;
using System.Threading.Tasks;

namespace Neo.Node.Rpc
{
    public sealed class GetVersionRpcMethod : IRpcMethod
    {
        // Static nonce and user agent (previously from LocalNode)
        private static readonly uint Nonce = RandomNumberFactory.NextUInt32();
        private static readonly string UserAgent = $"/{Assembly.GetExecutingAssembly().GetName().Name}:{Assembly.GetExecutingAssembly().GetName().Version?.ToString(3)}/";

        private readonly NeoSystemNode _node;

        public string Name => "getversion";

        public GetVersionRpcMethod(NeoSystemNode node)
        {
            _node = node ?? throw new ArgumentNullException(nameof(node));
        }

        public Task<JToken?> ProcessAsync(JArray? parameters)
        {
            var settings = _node.System.Settings;
            var tcpPort = _node.ChannelsConfig.Tcp?.Port ?? 0;

            var protocol = new JObject
            {
                ["addressversion"] = new JNumber(settings.AddressVersion),
                ["network"] = new JNumber(settings.Network),
                ["msperblock"] = new JNumber(settings.MillisecondsPerBlock),
                ["maxtraceableblocks"] = new JNumber(settings.MaxTraceableBlocks),
                ["maxvaliduntilblockincrement"] = new JNumber(settings.MaxValidUntilBlockIncrement),
                ["maxtransactionsperblock"] = new JNumber(settings.MaxTransactionsPerBlock),
                ["memorypoolmaxtransactions"] = new JNumber(settings.MemoryPoolMaxTransactions),
                ["validatorscount"] = new JNumber(settings.ValidatorsCount)
            };

            var result = new JObject
            {
                ["tcpport"] = new JNumber(tcpPort),
                ["wsport"] = new JNumber(0),
                ["nonce"] = new JNumber(Nonce),
                ["useragent"] = new JString(UserAgent),
                ["protocol"] = protocol
            };

            return Task.FromResult<JToken?>(result);
        }
    }
}
