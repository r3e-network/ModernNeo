// Copyright (C) 2015-2025 The Neo Project.
//
// GetValidatorsRpcMethod.cs file belongs to the neo project and is free
// software distributed under the MIT software license.

using Neo.Json;
using System.Threading.Tasks;
using Neo.RPC;
using Neo.SmartContract.Native;
using System;
using System.Linq;

namespace Neo.Node.Rpc;

public sealed class GetValidatorsRpcMethod : IRpcMethod
{
    private readonly NeoSystemNode _node;

    public string Name => "getvalidators";

    public GetValidatorsRpcMethod(NeoSystemNode node)
    {
        _node = node ?? throw new ArgumentNullException(nameof(node));
    }

    public Task<JToken?> ProcessAsync(JArray? parameters)
    {
        var validators = NativeContract.NEO.GetNextBlockValidators(
            _node.System.StoreView,
            _node.System.Settings.ValidatorsCount);

        var items = validators.Select(point => new JString(point.ToString())).ToArray();
        return Task.FromResult<JToken?>(new JArray(items));
    }
}
