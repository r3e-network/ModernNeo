// Copyright (C) 2015-2025 The Neo Project.
//
// SendToAddressRpcMethod.cs file belongs to the neo project and is free
// software distributed under the MIT software license.

using Neo.Json;
using System.Threading.Tasks;
using Neo.Network.P2P.Payloads;
using Neo.RPC;
using Neo.Wallets;
using System;

namespace Neo.Node.Rpc;

public sealed class SendToAddressRpcMethod : IRpcMethod
{
    private readonly WalletManager _walletManager;
    private readonly NeoSystemNode _node;

    public string Name => "sendtoaddress";

    public SendToAddressRpcMethod(WalletManager walletManager, NeoSystemNode node)
    {
        _walletManager = walletManager ?? throw new ArgumentNullException(nameof(walletManager));
        _node = node ?? throw new ArgumentNullException(nameof(node));
    }

    public async Task<JToken?> ProcessAsync(JArray? parameters)
    {
        if (parameters is null || parameters.Count < 3)
            throw new RpcException(RpcError.InvalidParams.Code, "Missing parameters.");

        var wallet = WalletRpcHelper.GetWalletOrThrow(_walletManager);
        var settings = _node.System.Settings;
        var snapshot = _node.System.StoreView;

        var asset = WalletRpcHelper.ParseScriptHash(parameters[0]!, settings, "asset");
        var to = WalletRpcHelper.ParseScriptHash(parameters[1]!, settings, "to");
        var amount = WalletRpcHelper.ParseAmount(parameters[2]!, snapshot, settings, asset);
        var data = parameters.Count > 3 ? WalletRpcHelper.ParseTransferData(parameters[3]) : null;

        var outputs = new[]
        {
            new TransferOutput
            {
                AssetId = asset,
                ScriptHash = to,
                Value = amount,
                Data = data
            }
        };

        Transaction tx;
        try
        {
            tx = wallet.MakeTransaction(snapshot, outputs);
        }
        catch (Exception ex)
        {
            throw new RpcException(RpcError.InternalError.Code, "Failed to create transaction.", new JString(ex.Message));
        }

        tx = await WalletTransactionHelper.SignAndRelayAsync(_node, wallet, tx);
        return tx.ToJson(settings);
    }
}
