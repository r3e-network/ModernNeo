// Copyright (C) 2015-2025 The Neo Project.
//
// InvokeResultBuilder.cs file belongs to the neo project and is free
// software distributed under the MIT software license.

using Neo.Extensions;
using System.Threading.Tasks;
using Neo.Json;
using Neo.Network.P2P.Payloads;
using Neo.SmartContract;
using Neo.VM.Types;
using System;

namespace Neo.Node.Rpc;

internal static class InvokeResultBuilder
{
    public static JObject ToJson(ApplicationEngine engine, Transaction tx, ReadOnlyMemory<byte> script)
    {
        var json = new JObject
        {
            ["script"] = script.ToArray().ToHexString(),
            ["state"] = engine.State.ToString(),
            ["gasconsumed"] = engine.FeeConsumed.ToString(),
            ["exception"] = engine.FaultException?.Message is null
                ? JToken.Null
                : new JString(engine.FaultException.Message)
        };

        var stack = new JArray();
        foreach (StackItem item in engine.ResultStack)
            stack.Add(item.ToJson());
        json["stack"] = stack;

        if (engine.Notifications.Count > 0)
        {
            var notifications = new JArray();
            foreach (var notification in engine.Notifications)
            {
                notifications.Add(new JObject
                {
                    ["contract"] = notification.ScriptHash.ToString(),
                    ["eventname"] = notification.EventName,
                    ["state"] = notification.State.ToJson()
                });
            }
            json["notifications"] = notifications;
        }

        json["tx"] = tx.ToArray().ToHexString();

        return json;
    }
}
