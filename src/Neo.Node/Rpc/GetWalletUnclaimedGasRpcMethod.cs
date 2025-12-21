// Copyright (C) 2015-2025 The Neo Project.
//
// GetWalletUnclaimedGasRpcMethod.cs file belongs to the neo project and is free
// software distributed under the MIT software license.

using Neo.Json;
using System.Threading.Tasks;
using Neo.RPC;
using Neo.SmartContract.Native;
using System;
using System.Numerics;

namespace Neo.Node.Rpc;

public sealed class GetWalletUnclaimedGasRpcMethod : IRpcMethod
{
    private readonly WalletManager _walletManager;
    private readonly NeoSystemNode _node;

    public string Name => "getwalletunclaimedgas";

    public GetWalletUnclaimedGasRpcMethod(WalletManager walletManager, NeoSystemNode node)
    {
        _walletManager = walletManager ?? throw new ArgumentNullException(nameof(walletManager));
        _node = node ?? throw new ArgumentNullException(nameof(node));
    }

    public Task<JToken?> ProcessAsync(JArray? parameters)
    {
        if (parameters is not null && parameters.Count > 0)
            throw new RpcException(RpcError.InvalidParams.Code, "No parameters expected.");

        var wallet = WalletRpcHelper.GetWalletOrThrow(_walletManager);
        var snapshot = _node.System.StoreView;
        var currentIndex = NativeContract.Ledger.CurrentIndex(snapshot);
        var end = currentIndex == uint.MaxValue ? currentIndex : currentIndex + 1;

        BigInteger total = BigInteger.Zero;
        foreach (var account in wallet.GetAccounts())
        {
            if (account.WatchOnly)
                continue;
            total += NativeContract.NEO.UnclaimedGas(snapshot, account.ScriptHash, end);
        }

        return Task.FromResult<JToken?>(new JString(total.ToString()));
    }
}
