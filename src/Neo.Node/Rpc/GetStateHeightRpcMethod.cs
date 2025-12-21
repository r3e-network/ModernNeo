// Copyright (C) 2015-2025 The Neo Project.
//
// GetStateHeightRpcMethod.cs file belongs to the neo project and is free
// software distributed under the MIT software license.

using Neo.Json;
using System.Threading.Tasks;
using Neo.RPC;
using Neo.SmartContract.Native;
using System;

namespace Neo.Node.Rpc;

public sealed class GetStateHeightRpcMethod : IRpcMethod
{
    private readonly NeoSystemNode _node;

    public string Name => "getstateheight";

    public GetStateHeightRpcMethod(NeoSystemNode node)
    {
        _node = node ?? throw new ArgumentNullException(nameof(node));
    }

    public Task<JToken?> ProcessAsync(JArray? parameters)
    {
        var height = NativeContract.Ledger.CurrentIndex(_node.System.StoreView);
        return Task.FromResult<JToken?>(new JObject
        {
            ["local"] = new JNumber(height),
            ["validated"] = new JNumber(height)
        });
    }
}
