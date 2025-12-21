// Copyright (C) 2015-2025 The Neo Project.
//
// SendManyRpcMethod.cs file belongs to the neo project and is free
// software distributed under the MIT software license.

using Neo.Json;
using System.Threading.Tasks;
using Neo.Network.P2P.Payloads;
using Neo.RPC;
using Neo.Wallets;
using System;
using System.Collections.Generic;

namespace Neo.Node.Rpc;

public sealed class SendManyRpcMethod : IRpcMethod
{
    private readonly WalletManager _walletManager;
    private readonly NeoSystemNode _node;

    public string Name => "sendmany";

    public SendManyRpcMethod(WalletManager walletManager, NeoSystemNode node)
    {
        _walletManager = walletManager ?? throw new ArgumentNullException(nameof(walletManager));
        _node = node ?? throw new ArgumentNullException(nameof(node));
    }

    public async Task<JToken?> ProcessAsync(JArray? parameters)
    {
        if (parameters is null || parameters.Count < 2)
            throw new RpcException(RpcError.InvalidParams.Code, "Missing parameters.");

        var wallet = WalletRpcHelper.GetWalletOrThrow(_walletManager);
        var settings = _node.System.Settings;
        var snapshot = _node.System.StoreView;

        var from = WalletRpcHelper.ParseScriptHash(parameters[0]!, settings, "from");
        if (parameters[1] is not JArray outputArray)
            throw new RpcException(RpcError.InvalidParams.Code, "Invalid outputs.");

        var outputs = new List<TransferOutput>();
        foreach (var outputToken in outputArray)
        {
            if (outputToken is not JObject output)
                throw new RpcException(RpcError.InvalidParams.Code, "Invalid output entry.");

            if (output["asset"] is null || output["value"] is null || output["address"] is null)
                throw new RpcException(RpcError.InvalidParams.Code, "Invalid output entry.");

            var asset = WalletRpcHelper.ParseScriptHash(output["asset"]!, settings, "asset");
            var amount = WalletRpcHelper.ParseAmount(output["value"]!, snapshot, settings, asset);
            var to = WalletRpcHelper.ParseScriptHash(output["address"]!, settings, "address");
            var data = output["data"] is null ? null : WalletRpcHelper.ParseTransferData(output["data"]);

            outputs.Add(new TransferOutput
            {
                AssetId = asset,
                ScriptHash = to,
                Value = amount,
                Data = data
            });
        }

        if (outputs.Count == 0)
            throw new RpcException(RpcError.InvalidParams.Code, "No outputs specified.");

        Transaction tx;
        try
        {
            tx = wallet.MakeTransaction(snapshot, outputs.ToArray(), from);
        }
        catch (Exception ex)
        {
            throw new RpcException(RpcError.InternalError.Code, "Failed to create transaction.", new JString(ex.Message));
        }

        tx = await WalletTransactionHelper.SignAndRelayAsync(_node, wallet, tx);
        return tx.ToJson(settings);
    }
}
