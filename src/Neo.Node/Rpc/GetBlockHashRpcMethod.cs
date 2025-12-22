// Copyright (C) 2015-2025 The Neo Project.
//
// GetBlockHashRpcMethod.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Json;
using Neo.RPC;
using Neo.SmartContract.Native;
using System;
using System.Threading.Tasks;

namespace Neo.Node.Rpc
{
    public sealed class GetBlockHashRpcMethod : IRpcMethod
    {
        private readonly NeoSystemNode _node;

        public string Name => "getblockhash";

        public GetBlockHashRpcMethod(NeoSystemNode node)
        {
            _node = node ?? throw new ArgumentNullException(nameof(node));
        }

        public Task<JToken?> ProcessAsync(JArray? parameters)
        {
            if (!RpcParameterParser.TryGetUInt32(parameters, 0, out var index))
                throw new RpcException(RpcError.InvalidParams.Code, "Invalid block index.");

            var hash = NativeContract.Ledger.GetBlockHash(_node.System.StoreView, index);
            return Task.FromResult<JToken?>(hash is null ? JToken.Null : new JString(hash.ToString()));
        }
    }
}
