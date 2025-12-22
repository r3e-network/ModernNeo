// Copyright (C) 2015-2025 The Neo Project.
//
// GetTransactionHeightRpcMethod.cs file belongs to the neo project and is free
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
    public sealed class GetTransactionHeightRpcMethod : IRpcMethod
    {
        private readonly NeoSystemNode _node;

        public string Name => "gettransactionheight";

        public GetTransactionHeightRpcMethod(NeoSystemNode node)
        {
            _node = node ?? throw new ArgumentNullException(nameof(node));
        }

        public Task<JToken?> ProcessAsync(JArray? parameters)
        {
            if (!RpcParameterParser.TryGetUInt256(parameters, 0, out var hash))
                throw new RpcException(RpcError.InvalidParams.Code, "Invalid transaction hash.");

            var state = NativeContract.Ledger.GetTransactionState(_node.System.StoreView, hash);
            return Task.FromResult<JToken?>(state is null ? JToken.Null : new JNumber(state.BlockIndex));
        }
    }
}
