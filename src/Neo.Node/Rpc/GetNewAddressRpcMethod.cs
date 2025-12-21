// Copyright (C) 2015-2025 The Neo Project.
//
// GetNewAddressRpcMethod.cs file belongs to the neo project and is free
// software distributed under the MIT software license.

using Neo.Json;
using System.Threading.Tasks;
using Neo.RPC;
using System;

namespace Neo.Node.Rpc;

public sealed class GetNewAddressRpcMethod : IRpcMethod
{
    private readonly WalletManager _walletManager;

    public string Name => "getnewaddress";

    public GetNewAddressRpcMethod(WalletManager walletManager)
    {
        _walletManager = walletManager ?? throw new ArgumentNullException(nameof(walletManager));
    }

    public Task<JToken?> ProcessAsync(JArray? parameters)
    {
        if (parameters is not null && parameters.Count > 0)
            throw new RpcException(RpcError.InvalidParams.Code, "No parameters expected.");

        var wallet = WalletRpcHelper.GetWalletOrThrow(_walletManager);
        var account = wallet.CreateAccount();
        wallet.Save();

        return Task.FromResult<JToken?>(new JString(account.Address));
    }
}
