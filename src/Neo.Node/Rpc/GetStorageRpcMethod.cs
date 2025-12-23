// Copyright (C) 2015-2025 The Neo Project.
//
// GetStorageRpcMethod.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo;
using Neo.Core;
using Neo.Extensions;
using Neo.Json;
using Neo.RPC;
using Neo.SmartContract;
using Neo.SmartContract.Native;
using Neo.Wallets;
using System;
using System.Threading.Tasks;

namespace Neo.Node.Rpc
{
    public sealed class GetStorageRpcMethod : IRpcMethod
    {
        private readonly NeoSystemNode _node;

        public string Name => "getstorage";

        public GetStorageRpcMethod(NeoSystemNode node)
        {
            _node = node ?? throw new ArgumentNullException(nameof(node));
        }

        public Task<JToken?> ProcessAsync(JArray? parameters)
        {
            if (parameters is null || parameters.Count < 2 || parameters[0] is null || parameters[1] is null)
                throw new RpcException(RpcError.InvalidParams.Code, "Missing contract and key parameters.");

            var scriptHash = ParseScriptHash(parameters[0]!.AsString());
            var keyBytes = ParseKeyBytes(parameters[1]!.AsString());

            var snapshot = _node.System.StoreView;
            var contract = NativeContract.ContractManagement.GetContract(snapshot, scriptHash);
            if (contract is null)
                return Task.FromResult<JToken?>(JToken.Null);

            var storageKey = new StorageKey
            {
                Id = contract.Id,
                Key = keyBytes
            };

            if (!snapshot.TryGet(storageKey, out var item) || item is null)
                return Task.FromResult<JToken?>(JToken.Null);

            return Task.FromResult<JToken?>(new JString(item.Value.Span.ToHexString()));
        }

        private UInt160 ParseScriptHash(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new RpcException(RpcError.InvalidParams.Code, "Invalid contract hash.");

            if (UInt160.TryParse(value, out var parsed) && parsed is not null)
                return parsed;

            try
            {
                return value.ToScriptHash(_node.System.Settings.AddressVersion);
            }
            catch (Exception ex)
            {
                throw new RpcException(RpcError.InvalidParams.Code, "Invalid contract hash.", new JString(ex.Message));
            }
        }

        private static byte[] ParseKeyBytes(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new RpcException(RpcError.InvalidParams.Code, "Invalid storage key.");

            try
            {
                return value.AsSpan().TrimStartIgnoreCase("0x").HexToBytes();
            }
            catch (Exception ex)
            {
                throw new RpcException(RpcError.InvalidParams.Code, "Invalid storage key.", new JString(ex.Message));
            }
        }
    }
}
