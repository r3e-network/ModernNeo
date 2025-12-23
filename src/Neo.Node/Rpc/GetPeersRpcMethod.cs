// Copyright (C) 2015-2025 The Neo Project.
//
// GetPeersRpcMethod.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Json;
using Neo.RPC;
using System;
using System.Threading.Tasks;

namespace Neo.Node.Rpc
{
    public sealed class GetPeersRpcMethod : IRpcMethod
    {
        private readonly NeoSystemNode _node;

        public string Name => "getpeers";

        public GetPeersRpcMethod(NeoSystemNode node)
        {
            _node = node ?? throw new ArgumentNullException(nameof(node));
        }

        public Task<JToken?> ProcessAsync(JArray? parameters)
        {
            // P2P networking disabled - Akka LocalNode removed
            // Use Orleans-based node for peer management
            return Task.FromResult<JToken?>(EmptyPeers());
        }

        private static JObject EmptyPeers()
        {
            return new JObject
            {
                ["unconnected"] = new JArray(),
                ["connected"] = new JArray(),
                ["bad"] = new JArray()
            };
        }
    }
}
