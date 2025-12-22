// Copyright (C) 2015-2025 The Neo Project.
//
// GetStateHeightRpcMethod.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Json;
using Neo.RPC;
using Neo.SmartContract.Native;
using System;
using System.Threading.Tasks;

namespace Neo.Node.Rpc
{
    public sealed class GetStateHeightRpcMethod : IRpcMethod
    {
        private readonly NeoSystemNode _node;

        public string Name => "getstateheight";

        public GetStateHeightRpcMethod(NeoSystemNode node)
        {
            _node = node ?? throw new ArgumentNullException(nameof(node));
        }

        public Task<JToken?> ProcessAsync(JArray? parameters)
        {
            var height = NativeContract.Ledger.CurrentIndex(_node.System.StoreView);
            return Task.FromResult<JToken?>(new JObject
            {
                ["local"] = new JNumber(height),
                ["validated"] = new JNumber(height)
            });
        }
    }
}
