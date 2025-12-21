// Copyright (C) 2015-2025 The Neo Project.
//
// GetRawMemPoolRpcMethod.cs file belongs to the neo project and is free
// software distributed under the MIT software license.

using Neo.Json;
using System.Threading.Tasks;
using Neo.RPC;
using System;
using System.Linq;

namespace Neo.Node.Rpc;

public sealed class GetRawMemPoolRpcMethod : IRpcMethod
{
    private readonly NeoSystemNode _node;

    public string Name => "getrawmempool";

    public GetRawMemPoolRpcMethod(NeoSystemNode node)
    {
        _node = node ?? throw new ArgumentNullException(nameof(node));
    }

    public Task<JToken?> ProcessAsync(JArray? parameters)
    {
        var verbose = false;
        if (RpcParameterParser.TryGetBoolean(parameters, 0, out var verboseParam))
            verbose = verboseParam;

        _node.System.MemPool.GetVerifiedAndUnverifiedTransactions(out var verified, out var unverified);
        var verifiedHashes = verified.Select(tx => tx.Hash.ToString()).ToArray();
        var unverifiedHashes = unverified.Select(tx => tx.Hash.ToString()).ToArray();

        if (!verbose)
        {
            var all = verifiedHashes.Concat(unverifiedHashes).Select(p => new JString(p)).ToArray();
            return Task.FromResult<JToken?>(new JArray(all));
        }

        var result = new JObject
        {
            ["verified"] = new JArray(verifiedHashes.Select(p => new JString(p)).ToArray()),
            ["unverified"] = new JArray(unverifiedHashes.Select(p => new JString(p)).ToArray())
        };

        return Task.FromResult<JToken?>(result);
    }
}
