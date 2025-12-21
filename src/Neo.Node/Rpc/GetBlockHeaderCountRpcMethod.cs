// Copyright (C) 2015-2025 The Neo Project.
//
// GetBlockHeaderCountRpcMethod.cs file belongs to the neo project and is free
// software distributed under the MIT software license.

using Neo.Json;
using System.Threading.Tasks;
using Neo.RPC;
using Neo.SmartContract.Native;
using System;

namespace Neo.Node.Rpc;

public sealed class GetBlockHeaderCountRpcMethod : IRpcMethod
{
    private readonly NeoSystemNode _node;

    public string Name => "getblockheadercount";

    public GetBlockHeaderCountRpcMethod(NeoSystemNode node)
    {
        _node = node ?? throw new ArgumentNullException(nameof(node));
    }

    public Task<JToken?> ProcessAsync(JArray? parameters)
    {
        var currentIndex = NativeContract.Ledger.CurrentIndex(_node.System.StoreView);
        var headerHeight = _node.System.HeaderCache.Last?.Index ?? currentIndex;
        return Task.FromResult<JToken?>(new JNumber(headerHeight + 1));
    }
}
