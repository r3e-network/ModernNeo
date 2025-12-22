// Copyright (C) 2015-2025 The Neo Project.
//
// GetApplicationLogRpcMethod.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Json;
using Neo.Plugins;
using Neo.RPC;
using Neo.SmartContract;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Neo.Node.Rpc
{
    public sealed class GetApplicationLogRpcMethod : IRpcMethod
    {
        public string Name => "getapplicationlog";

        public Task<JToken?> ProcessAsync(JArray? parameters)
        {
            if (!RpcParameterParser.TryGetUInt256(parameters, 0, out var hash))
                throw new RpcException(RpcError.InvalidParams.Code, "Invalid transaction or block hash.");

            TriggerType? trigger = null;
            if (parameters is not null && parameters.Count > 1 && parameters[1] is not null)
            {
                var token = parameters[1]!;
                var text = token.AsString();
                if (!string.IsNullOrWhiteSpace(text) &&
                    Enum.TryParse<TriggerType>(text, ignoreCase: true, out var parsed))
                {
                    trigger = parsed;
                }
                else
                {
                    var number = token.AsNumber();
                    if (double.IsNaN(number) || number < 0 || number > byte.MaxValue)
                        throw new RpcException(RpcError.InvalidParams.Code, "Invalid trigger.");

                    trigger = (TriggerType)(byte)number;
                }
            }

            var provider = Plugin.Plugins.OfType<IApplicationLogProvider>().FirstOrDefault();
            if (provider is null)
                throw new RpcException(RpcError.InternalError.Code, "ApplicationLogs plugin is not loaded.");

            var payload = provider.GetApplicationLog(hash, trigger);
            if (string.IsNullOrWhiteSpace(payload))
                return Task.FromResult<JToken?>(JToken.Null);

            try
            {
                return Task.FromResult<JToken?>(JToken.Parse(payload));
            }
            catch (Exception ex)
            {
                throw new RpcException(RpcError.InternalError.Code, "Invalid application log payload.", new JString(ex.Message));
            }
        }
    }
}
