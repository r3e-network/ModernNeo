// Copyright (C) 2015-2025 The Neo Project.
//
// GetConnectionCountRpcMethod.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Akka.Actor;
using Neo.Json;
using Neo.Network.P2P;
using Neo.RPC;
using System;
using System.Threading.Tasks;

namespace Neo.Node.Rpc
{
    public sealed class GetConnectionCountRpcMethod : IRpcMethod
    {
        private static readonly TimeSpan DefaultAskTimeout = TimeSpan.FromSeconds(5);

        private readonly NeoSystemNode _node;

        public string Name => "getconnectioncount";

        public GetConnectionCountRpcMethod(NeoSystemNode node)
        {
            _node = node ?? throw new ArgumentNullException(nameof(node));
        }

        public async Task<JToken?> ProcessAsync(JArray? parameters)
        {
            try
            {
                var localNode = await _node.System.LocalNode.Ask<LocalNode>(new LocalNode.GetInstance(), DefaultAskTimeout);
                var count = localNode.ConnectedCount;
                return new JNumber(count);
            }
            catch
            {
                return new JNumber(0);
            }
        }
    }
}
