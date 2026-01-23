// Copyright (C) 2015-2025 The Neo Project.
//
// GetStateRootRpcMethod.cs file belongs to the neo project and is free
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
    public sealed class GetStateRootRpcMethod : IRpcMethod
    {
        private readonly NeoSystemNode _node;

        public string Name => "getstateroot";

        public GetStateRootRpcMethod(NeoSystemNode node)
        {
            _node = node ?? throw new ArgumentNullException(nameof(node));
        }

        public Task<JToken?> ProcessAsync(JArray? parameters)
        {
            if (parameters is null || parameters.Count == 0 || parameters[0] is null)
                throw new RpcException(RpcError.InvalidParams.Code, "Missing block index or hash.");

            bool blockExists;
            if (RpcParameterParser.TryGetUInt32(parameters, 0, out var index))
            {
                blockExists = NativeContract.Ledger.GetBlock(_node.System.StoreView, index) is not null;
            }
            else if (RpcParameterParser.TryGetUInt256(parameters, 0, out var hash))
            {
                blockExists = NativeContract.Ledger.GetBlock(_node.System.StoreView, hash) is not null;
            }
            else
            {
                throw new RpcException(RpcError.InvalidParams.Code, "Invalid block index or hash.");
            }

            if (!blockExists)
                return Task.FromResult<JToken?>(JToken.Null);

            throw new RpcException(RpcError.InternalError.Code, "State roots are not available in this build.");
        }
    }
}
