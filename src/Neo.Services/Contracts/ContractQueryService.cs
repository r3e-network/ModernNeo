// Copyright (C) 2015-2025 The Neo Project.
//
// ContractQueryService.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.SmartContract;
using Neo.SmartContract.Native;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Neo.Services.Contracts
{
    /// <summary>
    /// Service for querying deployed smart contracts.
    /// </summary>
    public sealed class ContractQueryService : IContractQueryService
    {
        private readonly NeoSystem _system;

        public ContractQueryService(NeoSystem system)
        {
            _system = system ?? throw new ArgumentNullException(nameof(system));
        }

        public ContractState? GetContract(string hashHex)
        {
            var hash = ParseScriptHash(hashHex);
            if (hash is null) return null;

            return NativeContract.ContractManagement.GetContract(_system.StoreView, hash);
        }

        public ContractState? GetContractById(int id)
        {
            return NativeContract.ContractManagement.GetContractById(_system.StoreView, id);
        }

        public bool ContractExists(string hashHex)
        {
            var hash = ParseScriptHash(hashHex);
            if (hash is null) return false;

            return NativeContract.ContractManagement.GetContract(_system.StoreView, hash) is not null;
        }

        public bool HasMethod(string hashHex, string method, int parameterCount)
        {
            var hash = ParseScriptHash(hashHex);
            if (hash is null) return false;

            return NativeContract.ContractManagement.HasMethod(_system.StoreView, hash, method, parameterCount);
        }

        public IEnumerable<ContractState> ListContracts(int skip, int take)
        {
            if (skip < 0) skip = 0;
            if (take <= 0) return Enumerable.Empty<ContractState>();
            if (take > 100) take = 100; // Limit to 100

            return NativeContract.ContractManagement
                .ListContracts(_system.StoreView)
                .Skip(skip)
                .Take(take);
        }

        public int GetContractCount()
        {
            return NativeContract.ContractManagement
                .ListContracts(_system.StoreView)
                .Count();
        }

        private static UInt160? ParseScriptHash(string hashHex)
        {
            if (string.IsNullOrWhiteSpace(hashHex)) return null;

            if (hashHex.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                hashHex = hashHex[2..];

            try
            {
                var bytes = Convert.FromHexString(hashHex);
                if (bytes.Length != 20) return null;
                return new UInt160(bytes);
            }
            catch
            {
                return null;
            }
        }
    }
}
