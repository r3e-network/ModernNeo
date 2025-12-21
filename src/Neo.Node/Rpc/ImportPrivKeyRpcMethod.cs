// Copyright (C) 2015-2025 The Neo Project.
//
// ImportPrivKeyRpcMethod.cs file belongs to the neo project and is free
// software distributed under the MIT software license.

using Neo.Json;
using System.Threading.Tasks;
using Neo.RPC;
using System;

namespace Neo.Node.Rpc;

public sealed class ImportPrivKeyRpcMethod : IRpcMethod
{
    private readonly WalletManager _walletManager;

    public string Name => "importprivkey";

    public ImportPrivKeyRpcMethod(WalletManager walletManager)
    {
        _walletManager = walletManager ?? throw new ArgumentNullException(nameof(walletManager));
    }

    public Task<JToken?> ProcessAsync(JArray? parameters)
    {
        if (parameters is null || parameters.Count == 0 || parameters[0] is null)
            throw new RpcException(RpcError.InvalidParams.Code, "Missing WIF key.");

        var wif = parameters[0]!.AsString();
        if (string.IsNullOrWhiteSpace(wif))
            throw new RpcException(RpcError.InvalidParams.Code, "Invalid WIF key.");

        var wallet = WalletRpcHelper.GetWalletOrThrow(_walletManager);
        try
        {
            var account = wallet.Import(wif);
            wallet.Save();
            return Task.FromResult<JToken?>(WalletRpcHelper.WalletAccountToJson(account));
        }
        catch (Exception ex)
        {
            throw new RpcException(RpcError.InvalidParams.Code, "Failed to import private key.", new JString(ex.Message));
        }
    }
}
