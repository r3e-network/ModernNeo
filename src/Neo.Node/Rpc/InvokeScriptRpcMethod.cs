// Copyright (C) 2015-2025 The Neo Project.
//
// InvokeScriptRpcMethod.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Json;
using Neo.RPC;
using Neo.SmartContract;
using System;
using System.Threading.Tasks;

namespace Neo.Node.Rpc
{
    public sealed class InvokeScriptRpcMethod : IRpcMethod
    {
        private readonly NeoSystemNode _node;

        public string Name => "invokescript";

        public InvokeScriptRpcMethod(NeoSystemNode node)
        {
            _node = node ?? throw new ArgumentNullException(nameof(node));
        }

        public Task<JToken?> ProcessAsync(JArray? parameters)
        {
            if (!RpcParameterParser.TryGetHexBytes(parameters, 0, out var script))
                throw new RpcException(RpcError.InvalidParams.Code, "Invalid script.");

            var signerToken = parameters is not null && parameters.Count > 1 ? parameters[1] : null;
            var signers = RpcInvocationHelper.ParseSigners(signerToken);

            var snapshot = _node.System.StoreView.CloneCache();
            var tx = RpcInvocationHelper.CreateInvocationTransaction(_node.System, script, signers);

            using var engine = ApplicationEngine.Run(script, snapshot, tx, settings: _node.System.Settings);
            return Task.FromResult<JToken?>(InvokeResultBuilder.ToJson(engine, tx, script));
        }
    }
}
