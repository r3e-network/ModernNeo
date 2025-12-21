// Copyright (C) 2015-2025 The Neo Project.
//
// GetBlockHeaderRpcMethod.cs file belongs to the neo project and is free
// software distributed under the MIT software license.

using Neo;
using System.Threading.Tasks;
using Neo.Extensions;
using Neo.Json;
using Neo.RPC;
using Neo.SmartContract.Native;
using System;

namespace Neo.Node.Rpc;

public sealed class GetBlockHeaderRpcMethod : IRpcMethod
{
    private readonly NeoSystemNode _node;

    public string Name => "getblockheader";

    public GetBlockHeaderRpcMethod(NeoSystemNode node)
    {
        _node = node ?? throw new ArgumentNullException(nameof(node));
    }

    public Task<JToken?> ProcessAsync(JArray? parameters)
    {
        if (parameters is null || parameters.Count == 0 || parameters[0] is null)
            throw new RpcException(RpcError.InvalidParams.Code, "Missing block index or hash.");

        var token = parameters[0]!;
        var header = ResolveHeader(token);
        if (header is null)
            return Task.FromResult<JToken?>(JToken.Null);

        var verbose = true;
        if (RpcParameterParser.TryGetBoolean(parameters, 1, out var verboseParam))
            verbose = verboseParam;

        if (!verbose)
            return Task.FromResult<JToken?>(new JString(header.ToArray().ToHexString()));

        var json = header.ToJson(_node.System.Settings);
        AddBlockMetadata(json, header.Index);
        return Task.FromResult<JToken?>(json);
    }

    private Neo.Network.P2P.Payloads.Header? ResolveHeader(JToken token)
    {
        if (RpcParameterParser.TryGetUInt32(new JArray(token), 0, out var index))
            return NativeContract.Ledger.GetHeader(_node.System.StoreView, index);

        var text = token.AsString();
        if (UInt256.TryParse(text, out var hash))
            return NativeContract.Ledger.GetHeader(_node.System.StoreView, hash);

        return null;
    }

    private void AddBlockMetadata(JObject json, uint index)
    {
        var currentIndex = NativeContract.Ledger.CurrentIndex(_node.System.StoreView);
        if (currentIndex >= index)
            json["confirmations"] = new JNumber(currentIndex - index + 1);

        if (index < currentIndex)
        {
            var nextHash = NativeContract.Ledger.GetBlockHash(_node.System.StoreView, index + 1);
            if (nextHash is not null)
                json["nextblockhash"] = nextHash.ToString();
        }
    }
}
