// Copyright (C) 2015-2025 The Neo Project.
//
// SendRawTransactionRpcMethod.cs file belongs to the neo project and is free
// software distributed under the MIT software license.

using Akka.Actor;
using System.Threading.Tasks;
using Neo.Extensions;
using Neo.IO;
using Neo.Json;
using Neo.Ledger;
using Neo.Network.P2P.Payloads;
// VerifyResult is in Neo.Ledger namespace (from Neo.Protocol assembly)
using Neo.RPC;
using System;

namespace Neo.Node.Rpc;

public sealed class SendRawTransactionRpcMethod : IRpcMethod
{
    private static readonly TimeSpan DefaultAskTimeout = TimeSpan.FromSeconds(30);

    private readonly NeoSystemNode _node;

    public string Name => "sendrawtransaction";

    public SendRawTransactionRpcMethod(NeoSystemNode node)
    {
        _node = node ?? throw new ArgumentNullException(nameof(node));
    }

    public async Task<JToken?> ProcessAsync(JArray? parameters)
    {
        if (parameters is null || parameters.Count == 0 || parameters[0] is null)
            throw new RpcException(RpcError.InvalidParams.Code, "Missing transaction data.");

        var hex = parameters[0]!.AsString();
        if (string.IsNullOrWhiteSpace(hex))
            throw new RpcException(RpcError.InvalidParams.Code, "Invalid transaction data.");

        Transaction tx;
        try
        {
            var payload = hex.AsSpan().TrimStartIgnoreCase("0x").HexToBytes();
            tx = payload.AsSerializable<Transaction>();
        }
        catch (Exception ex)
        {
            throw new RpcException(RpcError.InvalidParams.Code, "Invalid transaction data.", new JString(ex.Message));
        }

        var result = await _node.System.Blockchain.Ask<Neo.Ledger.Blockchain.RelayResult>(tx, DefaultAskTimeout);
        if (result.Result != VerifyResult.Succeed)
            throw new RpcException(-500, result.Result.ToString());

        return new JObject
        {
            ["hash"] = tx.Hash.ToString()
        };
    }
}
