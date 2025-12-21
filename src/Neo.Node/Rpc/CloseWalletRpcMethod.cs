// Copyright (C) 2015-2025 The Neo Project.
//
// CloseWalletRpcMethod.cs file belongs to the neo project and is free
// software distributed under the MIT software license.

using Neo.Json;
using System.Threading.Tasks;
using Neo.RPC;
using System;

namespace Neo.Node.Rpc;

public sealed class CloseWalletRpcMethod : IRpcMethod
{
    private readonly WalletManager _walletManager;

    public string Name => "closewallet";

    public CloseWalletRpcMethod(WalletManager walletManager)
    {
        _walletManager = walletManager ?? throw new ArgumentNullException(nameof(walletManager));
    }

    public Task<JToken?> ProcessAsync(JArray? parameters)
    {
        if (parameters is not null && parameters.Count > 0)
            throw new RpcException(RpcError.InvalidParams.Code, "No parameters expected.");

        _walletManager.Close();
        return Task.FromResult<JToken?>(new JBoolean(true));
    }
}
