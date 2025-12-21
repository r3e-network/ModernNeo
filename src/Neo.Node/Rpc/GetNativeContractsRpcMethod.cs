// Copyright (C) 2015-2025 The Neo Project.
//
// GetNativeContractsRpcMethod.cs file belongs to the neo project and is free
// software distributed under the MIT software license.

using Neo.Json;
using System.Threading.Tasks;
using Neo.RPC;
using Neo.SmartContract.Native;
using System;
using System.Linq;

namespace Neo.Node.Rpc;

public sealed class GetNativeContractsRpcMethod : IRpcMethod
{
    private readonly NeoSystemNode _node;

    public string Name => "getnativecontracts";

    public GetNativeContractsRpcMethod(NeoSystemNode node)
    {
        _node = node ?? throw new ArgumentNullException(nameof(node));
    }

    public Task<JToken?> ProcessAsync(JArray? parameters)
    {
        var snapshot = _node.System.StoreView;
        var contracts = NativeContract.Contracts
            .Select(contract => NativeContract.ContractManagement.GetContract(snapshot, contract.Hash))
            .Where(contract => contract is not null)
            .Select(contract => contract!.ToJson())
            .ToArray();

        return Task.FromResult<JToken?>(new JArray(contracts));
    }
}
