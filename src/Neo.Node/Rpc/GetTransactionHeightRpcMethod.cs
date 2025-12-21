// Copyright (C) 2015-2025 The Neo Project.
//
// GetTransactionHeightRpcMethod.cs file belongs to the neo project and is free
// software distributed under the MIT software license.

using Neo.Json;
using System.Threading.Tasks;
using Neo.RPC;
using Neo.SmartContract.Native;
using System;

namespace Neo.Node.Rpc;

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
