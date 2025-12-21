// Copyright (C) 2015-2025 The Neo Project.
//
// GetWalletBalanceRpcMethod.cs file belongs to the neo project and is free
// software distributed under the MIT software license.

using Neo.Json;
using System.Threading.Tasks;
using Neo.RPC;
using System;

namespace Neo.Node.Rpc;

public sealed class GetWalletBalanceRpcMethod : IRpcMethod
{
    private readonly WalletManager _walletManager;
    private readonly NeoSystemNode _node;

    public string Name => "getwalletbalance";

    public GetWalletBalanceRpcMethod(WalletManager walletManager, NeoSystemNode node)
    {
        _walletManager = walletManager ?? throw new ArgumentNullException(nameof(walletManager));
        _node = node ?? throw new ArgumentNullException(nameof(node));
    }

    public Task<JToken?> ProcessAsync(JArray? parameters)
    {
        if (parameters is null || parameters.Count == 0 || parameters[0] is null)
            throw new RpcException(RpcError.InvalidParams.Code, "Missing asset hash.");

        var wallet = WalletRpcHelper.GetWalletOrThrow(_walletManager);
        var assetHash = WalletRpcHelper.ParseScriptHash(parameters[0]!, _node.System.Settings, "asset hash");
        var balance = wallet.GetAvailable(_node.System.StoreView, assetHash);

        return Task.FromResult<JToken?>(new JObject
        {
            ["balance"] = balance.Value.ToString()
        });
    }
}
