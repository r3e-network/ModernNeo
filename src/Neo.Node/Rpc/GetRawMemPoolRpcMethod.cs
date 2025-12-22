// Copyright (C) 2015-2025 The Neo Project.
//
// GetRawMemPoolRpcMethod.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Json;
using Neo.RPC;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Neo.Node.Rpc
{
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
}
