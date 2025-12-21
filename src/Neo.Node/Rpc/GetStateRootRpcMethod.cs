// Copyright (C) 2015-2025 The Neo Project.
//
// GetStateRootRpcMethod.cs file belongs to the neo project and is free
// software distributed under the MIT software license.

using Neo.Json;
using System.Threading.Tasks;
using Neo.RPC;
using Neo.SmartContract.Native;
using System;

namespace Neo.Node.Rpc;

public sealed class GetStateRootRpcMethod : IRpcMethod
{
    private readonly NeoSystemNode _node;

    public string Name => "getstateroot";

    public GetStateRootRpcMethod(NeoSystemNode node)
    {
        _node = node ?? throw new ArgumentNullException(nameof(node));
    }

    public Task<JToken?> ProcessAsync(JArray? parameters)
    {
        if (parameters is null || parameters.Count == 0 || parameters[0] is null)
            throw new RpcException(RpcError.InvalidParams.Code, "Missing block index or hash.");

        bool blockExists;
        if (RpcParameterParser.TryGetUInt32(parameters, 0, out var index))
        {
            blockExists = NativeContract.Ledger.GetBlock(_node.System.StoreView, index) is not null;
        }
        else if (RpcParameterParser.TryGetUInt256(parameters, 0, out var hash))
        {
            blockExists = NativeContract.Ledger.GetBlock(_node.System.StoreView, hash) is not null;
        }
        else
        {
            throw new RpcException(RpcError.InvalidParams.Code, "Invalid block index or hash.");
        }

        if (!blockExists)
            return Task.FromResult<JToken?>(JToken.Null);

        // State roots are not yet persisted in this refactor path.
        return Task.FromResult<JToken?>(JToken.Null);
    }
}
