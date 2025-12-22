// Copyright (C) 2015-2025 The Neo Project.
//
// ValidateAddressRpcMethod.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Json;
using Neo.RPC;
using Neo.Wallets;
using System;
using System.Threading.Tasks;

namespace Neo.Node.Rpc
{
    public sealed class ValidateAddressRpcMethod : IRpcMethod
    {
        private readonly NeoSystemNode _node;

        public string Name => "validateaddress";

        public ValidateAddressRpcMethod(NeoSystemNode node)
        {
            _node = node ?? throw new ArgumentNullException(nameof(node));
        }

        public Task<JToken?> ProcessAsync(JArray? parameters)
        {
            if (parameters is null || parameters.Count == 0 || parameters[0] is null)
                throw new RpcException(RpcError.InvalidParams.Code, "Missing address.");

            var address = parameters[0]!.AsString();
            if (string.IsNullOrWhiteSpace(address))
                throw new RpcException(RpcError.InvalidParams.Code, "Invalid address.");

            var isValid = true;
            try
            {
                _ = address.ToScriptHash(_node.System.Settings.AddressVersion);
            }
            catch
            {
                isValid = false;
            }

            return Task.FromResult<JToken?>(new JObject
            {
                ["address"] = address,
                ["isvalid"] = isValid
            });
        }
    }
}
