// Copyright (C) 2015-2025 The Neo Project.
//
// GetNativeContractsRpcMethod.cs file belongs to the neo project and is free
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
using System.Linq;
using System.Threading.Tasks;

namespace Neo.Node.Rpc
{
    public sealed class GetNativeContractsRpcMethod : IRpcMethod
    {
        private readonly NeoSystemNode _node;

        public string Name => "getnativecontracts";

        public GetNativeContractsRpcMethod(NeoSystemNode node)
        {
            _node = node ?? throw new ArgumentNullException(nameof(node));
        }

        public Task<JToken?> ProcessAsync(JArray? parameters)
        {
            var snapshot = _node.System.StoreView;
            var contracts = NativeContract.Contracts
                .Select(contract => NativeContract.ContractManagement.GetContract(snapshot, contract.Hash))
                .Where(contract => contract is not null)
                .Select(contract => contract!.ToJson())
                .ToArray();

            return Task.FromResult<JToken?>(new JArray(contracts));
        }
    }
}
