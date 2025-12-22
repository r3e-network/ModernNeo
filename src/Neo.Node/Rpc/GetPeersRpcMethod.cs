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

using Akka.Actor;
using Neo.Json;
using Neo.Network.P2P;
using Neo.RPC;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Neo.Node.Rpc
{
    public sealed class GetPeersRpcMethod : IRpcMethod
    {
        private static readonly TimeSpan DefaultAskTimeout = TimeSpan.FromSeconds(5);

        private readonly NeoSystemNode _node;

        public string Name => "getpeers";

        public GetPeersRpcMethod(NeoSystemNode node)
        {
            _node = node ?? throw new ArgumentNullException(nameof(node));
        }

        public async Task<JToken?> ProcessAsync(JArray? parameters)
        {
            try
            {
                var localNode = await _node.System.LocalNode.Ask<LocalNode>(new LocalNode.GetInstance(), DefaultAskTimeout);
                var connected = localNode.GetRemoteNodes()
                    .Select(node => new JObject
                    {
                        ["address"] = node.Listener.Address.ToString(),
                        ["port"] = node.Listener.Port,
                        ["lastblockindex"] = node.LastBlockIndex,
                        ["isfullnode"] = node.IsFullNode
                    })
                    .ToArray();

                var unconnected = localNode.GetUnconnectedPeers()
                    .Select(endPoint => new JObject
                    {
                        ["address"] = endPoint.Address.ToString(),
                        ["port"] = endPoint.Port
                    })
                    .ToArray();

                return new JObject
                {
                    ["unconnected"] = new JArray(unconnected),
                    ["connected"] = new JArray(connected),
                    ["bad"] = new JArray()
                };
            }
            catch
            {
                return EmptyPeers();
            }
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
