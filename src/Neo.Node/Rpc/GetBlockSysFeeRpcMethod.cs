// Copyright (C) 2015-2025 The Neo Project.
//
// GetBlockSysFeeRpcMethod.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Json;
using Neo.Network.P2P.Payloads;
using Neo.RPC;
using Neo.SmartContract.Native;
using System;
using System.Numerics;
using System.Threading.Tasks;

namespace Neo.Node.Rpc
{
    public sealed class GetBlockSysFeeRpcMethod : IRpcMethod
    {
        private readonly NeoSystemNode _node;

        public string Name => "getblocksysfee";

        public GetBlockSysFeeRpcMethod(NeoSystemNode node)
        {
            _node = node ?? throw new ArgumentNullException(nameof(node));
        }

        public Task<JToken?> ProcessAsync(JArray? parameters)
        {
            if (parameters is null || parameters.Count == 0)
                throw new RpcException(RpcError.InvalidParams.Code, "Missing block index or hash.");

            Block? block = null;
            if (RpcParameterParser.TryGetUInt32(parameters, 0, out var index))
            {
                block = NativeContract.Ledger.GetBlock(_node.System.StoreView, index);
            }
            else if (RpcParameterParser.TryGetUInt256(parameters, 0, out var hash))
            {
                block = NativeContract.Ledger.GetBlock(_node.System.StoreView, hash);
            }
            else
            {
                throw new RpcException(RpcError.InvalidParams.Code, "Invalid block index or hash.");
            }

            if (block is null)
                return Task.FromResult<JToken?>(JToken.Null);

            BigInteger sysFee = BigInteger.Zero;
            foreach (var tx in block.Transactions)
                sysFee += tx.SystemFee;

            return Task.FromResult<JToken?>(new JString(sysFee.ToString()));
        }
    }
}
