// Copyright (C) 2015-2025 The Neo Project.
//
// GetUnclaimedGasRpcMethod.cs file belongs to the neo project and is free
// software distributed under the MIT software license.

using Neo;
using System.Threading.Tasks;
using Neo.Json;
using Neo.RPC;
using Neo.SmartContract.Native;
using Neo.Wallets;
using System;

namespace Neo.Node.Rpc;

public sealed class GetUnclaimedGasRpcMethod : IRpcMethod
{
    private readonly NeoSystemNode _node;

    public string Name => "getunclaimedgas";

    public GetUnclaimedGasRpcMethod(NeoSystemNode node)
    {
        _node = node ?? throw new ArgumentNullException(nameof(node));
    }

    public Task<JToken?> ProcessAsync(JArray? parameters)
    {
        if (parameters is null || parameters.Count == 0 || parameters[0] is null)
            throw new RpcException(RpcError.InvalidParams.Code, "Missing address.");

        var address = parameters[0]!.AsString();
        if (string.IsNullOrWhiteSpace(address))
            throw new RpcException(RpcError.InvalidParams.Code, "Invalid address.");

        UInt160 account;
        try
        {
            account = address.ToScriptHash(_node.System.Settings.AddressVersion);
        }
        catch (Exception ex)
        {
            throw new RpcException(RpcError.InvalidParams.Code, "Invalid address.", new JString(ex.Message));
        }

        var snapshot = _node.System.StoreView;
        var currentIndex = NativeContract.Ledger.CurrentIndex(snapshot);
        var end = currentIndex == uint.MaxValue ? currentIndex : currentIndex + 1;
        var unclaimed = NativeContract.NEO.UnclaimedGas(snapshot, account, end);

        return Task.FromResult<JToken?>(new JObject
        {
            ["unclaimed"] = unclaimed.ToString(),
            ["address"] = address
        });
    }
}
