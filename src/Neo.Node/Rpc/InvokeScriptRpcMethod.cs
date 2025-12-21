// Copyright (C) 2015-2025 The Neo Project.
//
// InvokeScriptRpcMethod.cs file belongs to the neo project and is free
// software distributed under the MIT software license.

using Neo.Json;
using System.Threading.Tasks;
using Neo.RPC;
using Neo.SmartContract;
using System;

namespace Neo.Node.Rpc;

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
