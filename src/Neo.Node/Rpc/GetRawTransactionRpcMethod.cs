// Copyright (C) 2015-2025 The Neo Project.
//
// GetRawTransactionRpcMethod.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo;
using Neo.Extensions;
using Neo.Json;
using Neo.Network.P2P.Payloads;
using Neo.RPC;
using Neo.SmartContract.Native;
using System;
using System.Threading.Tasks;

namespace Neo.Node.Rpc
{
    public sealed class GetRawTransactionRpcMethod : IRpcMethod
    {
        private readonly NeoSystemNode _node;

        public string Name => "getrawtransaction";

        public GetRawTransactionRpcMethod(NeoSystemNode node)
        {
            _node = node ?? throw new ArgumentNullException(nameof(node));
        }

        public Task<JToken?> ProcessAsync(JArray? parameters)
        {
            if (!RpcParameterParser.TryGetUInt256(parameters, 0, out var hash))
                throw new RpcException(RpcError.InvalidParams.Code, "Invalid transaction hash.");

            var verbose = false;
            if (RpcParameterParser.TryGetBoolean(parameters, 1, out var verboseParam))
                verbose = verboseParam;

            Transaction? tx = null;
            TransactionState? state = null;

            if (_node.System.MemPool.TryGetValue(hash, out var memTx))
            {
                tx = memTx;
            }
            else
            {
                state = NativeContract.Ledger.GetTransactionState(_node.System.StoreView, hash);
                tx = state?.Transaction;
            }

            if (tx is null)
                return Task.FromResult<JToken?>(JToken.Null);

            if (!verbose)
                return Task.FromResult<JToken?>(new JString(tx.ToArray().ToHexString()));

            var json = tx.ToJson(_node.System.Settings);

            if (state is not null)
            {
                var currentIndex = NativeContract.Ledger.CurrentIndex(_node.System.StoreView);
                if (currentIndex >= state.BlockIndex)
                    json["confirmations"] = new JNumber(currentIndex - state.BlockIndex + 1);

                var blockHash = NativeContract.Ledger.GetBlockHash(_node.System.StoreView, state.BlockIndex);
                if (blockHash is not null)
                {
                    json["blockhash"] = blockHash.ToString();
                    var trimmed = NativeContract.Ledger.GetTrimmedBlock(_node.System.StoreView, blockHash);
                    if (trimmed is not null)
                        json["blocktime"] = new JNumber(trimmed.Header.Timestamp);
                }

                json["vmstate"] = state.State.ToString();
            }

            return Task.FromResult<JToken?>(json);
        }
    }
}
