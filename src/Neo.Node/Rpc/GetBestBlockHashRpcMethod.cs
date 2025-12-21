// Copyright (C) 2015-2025 The Neo Project.
//
// GetBestBlockHashRpcMethod.cs file belongs to the neo project and is free
// software distributed under the MIT software license.

using Neo.Json;
using System.Threading.Tasks;
using Neo.RPC;
using Neo.SmartContract.Native;
using System;

namespace Neo.Node.Rpc;

public sealed class GetBestBlockHashRpcMethod : IRpcMethod
{
    private readonly NeoSystemNode _node;

    public string Name => "getbestblockhash";

    public GetBestBlockHashRpcMethod(NeoSystemNode node)
    {
        _node = node ?? throw new ArgumentNullException(nameof(node));
    }

    public Task<JToken?> ProcessAsync(JArray? parameters)
    {
        var currentIndex = NativeContract.Ledger.CurrentIndex(_node.System.StoreView);
        var hash = NativeContract.Ledger.GetBlockHash(_node.System.StoreView, currentIndex);
        return Task.FromResult<JToken?>(hash is null ? JToken.Null : new JString(hash.ToString()));
    }
}
