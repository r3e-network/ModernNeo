// Copyright (C) 2015-2025 The Neo Project.
//
// InvokeFunctionRpcMethod.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Extensions;
using Neo.Json;
using Neo.Network.P2P.Payloads;
using Neo.RPC;
using Neo.SmartContract;
using Neo.VM;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Neo.Node.Rpc
{
    public sealed class InvokeFunctionRpcMethod : IRpcMethod
    {
        private readonly NeoSystemNode _node;

        public string Name => "invokefunction";

        public InvokeFunctionRpcMethod(NeoSystemNode node)
        {
            _node = node ?? throw new ArgumentNullException(nameof(node));
        }

        public Task<JToken?> ProcessAsync(JArray? parameters)
        {
            if (parameters is null || parameters.Count < 2 || parameters[0] is null || parameters[1] is null)
                throw new RpcException(RpcError.InvalidParams.Code, "Missing contract or operation.");

            var scriptHash = RpcInvocationHelper.ResolveContractHash(_node.System, parameters[0]!);
            var operation = parameters[1]?.AsString();
            if (string.IsNullOrWhiteSpace(operation))
                throw new RpcException(RpcError.InvalidParams.Code, "Invalid operation.");

            var argsToken = parameters.Count > 2 ? parameters[2] : null;
            var args = RpcInvocationHelper.ParseParameters(argsToken);

            var signers = Array.Empty<Signer>();
            var callFlags = CallFlags.All;

            if (parameters.Count > 3 && parameters[3] is not null)
            {
                if (parameters[3] is JArray)
                    signers = RpcInvocationHelper.ParseSigners(parameters[3]);
                else
                    callFlags = RpcInvocationHelper.ParseCallFlags(parameters[3]);
            }

            if (parameters.Count > 4 && parameters[4] is not null)
                callFlags = RpcInvocationHelper.ParseCallFlags(parameters[4]);

            using var sb = new ScriptBuilder();
            var argsArray = args.Count == 0 ? Array.Empty<object?>() : args.Cast<object?>().ToArray();
            sb.EmitDynamicCall(scriptHash, operation, callFlags, argsArray);
            var script = sb.ToArray();

            var snapshot = _node.System.StoreView.CloneCache();
            var tx = RpcInvocationHelper.CreateInvocationTransaction(_node.System, script, signers);

            using var engine = ApplicationEngine.Run(script, snapshot, tx, settings: _node.System.Settings);
            return Task.FromResult<JToken?>(InvokeResultBuilder.ToJson(engine, tx, script));
        }
    }
}
