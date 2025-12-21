// Copyright (C) 2015-2025 The Neo Project.
//
// InvokeFunctionsRpcMethod.cs file belongs to the neo project and is free
// software distributed under the MIT software license.

using Neo.Extensions;
using System.Threading.Tasks;
using Neo.Json;
using Neo.Network.P2P.Payloads;
using Neo.RPC;
using Neo.SmartContract;
using Neo.VM;
using System;
using System.Linq;

namespace Neo.Node.Rpc;

public sealed class InvokeFunctionsRpcMethod : IRpcMethod
{
    private readonly NeoSystemNode _node;

    public string Name => "invokefunctions";

    public InvokeFunctionsRpcMethod(NeoSystemNode node)
    {
        _node = node ?? throw new ArgumentNullException(nameof(node));
    }

    public Task<JToken?> ProcessAsync(JArray? parameters)
    {
        if (parameters is null || parameters.Count < 2 || parameters[0] is null || parameters[1] is null)
            throw new RpcException(RpcError.InvalidParams.Code, "Missing contract or calls.");

        var scriptHash = RpcInvocationHelper.ResolveContractHash(_node.System, parameters[0]!);

        if (parameters[1] is not JArray calls)
            throw new RpcException(RpcError.InvalidParams.Code, "Invalid calls format.");

        using var sb = new ScriptBuilder();
        foreach (var entry in calls)
        {
            if (entry is not JObject call)
                throw new RpcException(RpcError.InvalidParams.Code, "Invalid call entry.");

            var operation = call["operation"]?.AsString();
            if (string.IsNullOrWhiteSpace(operation))
                throw new RpcException(RpcError.InvalidParams.Code, "Invalid call operation.");

            var argsToken = call.ContainsProperty("args") ? call["args"] : call["params"];
            var args = RpcInvocationHelper.ParseParameters(argsToken);
            var callFlags = RpcInvocationHelper.ParseCallFlags(call["callflags"]);

            var argsArray = args.Count == 0 ? Array.Empty<object?>() : args.Cast<object?>().ToArray();
            sb.EmitDynamicCall(scriptHash, operation, callFlags, argsArray);
        }

        var script = sb.ToArray();

        var signerToken = parameters.Count > 2 ? parameters[2] : null;
        var signers = RpcInvocationHelper.ParseSigners(signerToken);

        var snapshot = _node.System.StoreView.CloneCache();
        var tx = RpcInvocationHelper.CreateInvocationTransaction(_node.System, script, signers);

        using var engine = ApplicationEngine.Run(script, snapshot, tx, settings: _node.System.Settings);
        return Task.FromResult<JToken?>(InvokeResultBuilder.ToJson(engine, tx, script));
    }
}
