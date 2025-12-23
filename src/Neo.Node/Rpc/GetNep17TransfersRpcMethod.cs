// Copyright (C) 2015-2025 The Neo Project.
//
// GetNep17TransfersRpcMethod.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo;
using Neo.Core;
using Neo.Json;
using Neo.Plugins;
using Neo.RPC;
using Neo.Wallets;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Neo.Node.Rpc
{
    public sealed class GetNep17TransfersRpcMethod : IRpcMethod
    {
        private readonly NeoSystemNode _node;

        public string Name => "getnep17transfers";

        public GetNep17TransfersRpcMethod(NeoSystemNode node)
        {
            _node = node ?? throw new ArgumentNullException(nameof(node));
        }

        public Task<JToken?> ProcessAsync(JArray? parameters)
        {
            if (parameters is not null && parameters.Count > 3)
                throw new RpcException(RpcError.InvalidParams.Code, "Too many parameters.");

            var account = ParseAccount(parameters);

            var from = 0UL;
            if (parameters is not null && parameters.Count > 1 && parameters[1] is not null)
            {
                if (!RpcParameterParser.TryGetUInt64(parameters, 1, out from))
                    throw new RpcException(RpcError.InvalidParams.Code, "Invalid from timestamp.");
            }

            var to = ulong.MaxValue;
            if (parameters is not null && parameters.Count > 2 && parameters[2] is not null)
            {
                if (!RpcParameterParser.TryGetUInt64(parameters, 2, out to))
                    throw new RpcException(RpcError.InvalidParams.Code, "Invalid to timestamp.");
            }

            if (to < from)
                throw new RpcException(RpcError.InvalidParams.Code, "Invalid time range.");

            var tracker = Plugin.Plugins.OfType<INep17Tracker>().FirstOrDefault();
            if (tracker is null)
                throw new RpcException(RpcError.InternalError.Code, "RpcNep17Tracker plugin is not loaded.");

            var payload = tracker.GetNep17Transfers(account, from, to);
            if (string.IsNullOrWhiteSpace(payload))
                return Task.FromResult<JToken?>(JToken.Null);

            try
            {
                return Task.FromResult<JToken?>(JToken.Parse(payload));
            }
            catch (Exception ex)
            {
                throw new RpcException(RpcError.InternalError.Code, "Invalid NEP17 transfers payload.", new JString(ex.Message));
            }
        }

        private UInt160 ParseAccount(JArray? parameters)
        {
            if (parameters is null || parameters.Count == 0 || parameters[0] is null)
                throw new RpcException(RpcError.InvalidParams.Code, "Missing address.");

            var address = parameters[0]!.AsString();
            if (string.IsNullOrWhiteSpace(address))
                throw new RpcException(RpcError.InvalidParams.Code, "Invalid address.");

            if (UInt160.TryParse(address, out var parsed) && parsed is not null)
                return parsed;

            try
            {
                return address.ToScriptHash(_node.System.Settings.AddressVersion);
            }
            catch (Exception ex)
            {
                throw new RpcException(RpcError.InvalidParams.Code, "Invalid address.", new JString(ex.Message));
            }
        }
    }
}
