// Copyright (C) 2015-2025 The Neo Project.
//
// GetContractStateRpcMethod.cs file belongs to the neo project and is free
// software distributed under the MIT software license.

using Neo.Json;
using System.Threading.Tasks;
using Neo.RPC;
using Neo.SmartContract.Native;
using System;

namespace Neo.Node.Rpc;

public sealed class GetContractStateRpcMethod : IRpcMethod
{
    private readonly NeoSystemNode _node;

    public string Name => "getcontractstate";

    public GetContractStateRpcMethod(NeoSystemNode node)
    {
        _node = node ?? throw new ArgumentNullException(nameof(node));
    }

    public Task<JToken?> ProcessAsync(JArray? parameters)
    {
        if (parameters is null || parameters.Count == 0 || parameters[0] is null)
            throw new RpcException(RpcError.InvalidParams.Code, "Missing contract hash or id.");

        if (RpcParameterParser.TryGetUInt160(parameters, 0, out var hash))
        {
            var contract = NativeContract.ContractManagement.GetContract(_node.System.StoreView, hash);
            return Task.FromResult<JToken?>(contract is null ? JToken.Null : contract.ToJson());
        }

        if (RpcParameterParser.TryGetInt32(parameters, 0, out var id))
        {
            var contract = NativeContract.ContractManagement.GetContractById(_node.System.StoreView, id);
            return Task.FromResult<JToken?>(contract is null ? JToken.Null : contract.ToJson());
        }

        throw new RpcException(RpcError.InvalidParams.Code, "Invalid contract hash or id.");
    }
}
