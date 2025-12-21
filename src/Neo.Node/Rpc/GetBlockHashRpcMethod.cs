// Copyright (C) 2015-2025 The Neo Project.
//
// GetBlockHashRpcMethod.cs file belongs to the neo project and is free
// software distributed under the MIT software license.

using Neo.Json;
using System.Threading.Tasks;
using Neo.RPC;
using Neo.SmartContract.Native;
using System;

namespace Neo.Node.Rpc;

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
