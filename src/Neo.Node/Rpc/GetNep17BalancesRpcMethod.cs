// Copyright (C) 2015-2025 The Neo Project.
//
// GetNep17BalancesRpcMethod.cs file belongs to the neo project and is free
// software distributed under the MIT software license.

using Neo;
using System.Threading.Tasks;
using Neo.Json;
using Neo.Plugins;
using Neo.RPC;
using Neo.Wallets;
using System;
using System.Linq;

namespace Neo.Node.Rpc;

public sealed class GetNep17BalancesRpcMethod : IRpcMethod
{
    private readonly NeoSystemNode _node;

    public string Name => "getnep17balances";

    public GetNep17BalancesRpcMethod(NeoSystemNode node)
    {
        _node = node ?? throw new ArgumentNullException(nameof(node));
    }

    public Task<JToken?> ProcessAsync(JArray? parameters)
    {
        var account = ParseAccount(parameters);

        var tracker = Plugin.Plugins.OfType<INep17Tracker>().FirstOrDefault();
        if (tracker is null)
            throw new RpcException(RpcError.InternalError.Code, "RpcNep17Tracker plugin is not loaded.");

        var payload = tracker.GetNep17Balances(account);
        if (string.IsNullOrWhiteSpace(payload))
            return Task.FromResult<JToken?>(JToken.Null);

        try
        {
            return Task.FromResult<JToken?>(JToken.Parse(payload));
        }
        catch (Exception ex)
        {
            throw new RpcException(RpcError.InternalError.Code, "Invalid NEP17 balances payload.", new JString(ex.Message));
        }
    }

    private UInt160 ParseAccount(JArray? parameters)
    {
        if (parameters is null || parameters.Count == 0 || parameters[0] is null)
            throw new RpcException(RpcError.InvalidParams.Code, "Missing address.");

        var address = parameters[0]!.AsString();
        if (string.IsNullOrWhiteSpace(address))
            throw new RpcException(RpcError.InvalidParams.Code, "Invalid address.");

        if (UInt160.TryParse(address, out var parsed) && parsed is not null)
            return parsed;

        try
        {
            return address.ToScriptHash(_node.System.Settings.AddressVersion);
        }
        catch (Exception ex)
        {
            throw new RpcException(RpcError.InvalidParams.Code, "Invalid address.", new JString(ex.Message));
        }
    }
}
