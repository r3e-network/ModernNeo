// Copyright (C) 2015-2025 The Neo Project.
//
// AccountQueryService.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.SmartContract;
using Neo.SmartContract.Native;
using Neo.Wallets;
using System;
using System.Numerics;

namespace Neo.Services.Accounts
{
    /// <summary>
    /// Service for querying account balances and information.
    /// </summary>
    public sealed class AccountQueryService : IAccountQueryService
    {
        private readonly NeoSystem _system;

        public AccountQueryService(NeoSystem system)
        {
            _system = system ?? throw new ArgumentNullException(nameof(system));
        }

        public BigInteger GetNeoBalance(string addressOrHash)
        {
            var account = ParseAccount(addressOrHash);
            if (account is null) return BigInteger.Zero;

            return NativeContract.NEO.BalanceOf(_system.StoreView, account);
        }

        public BigInteger GetGasBalance(string addressOrHash)
        {
            var account = ParseAccount(addressOrHash);
            if (account is null) return BigInteger.Zero;

            return NativeContract.GAS.BalanceOf(_system.StoreView, account);
        }

        public BigInteger GetUnclaimedGas(string addressOrHash)
        {
            var account = ParseAccount(addressOrHash);
            if (account is null) return BigInteger.Zero;

            var snapshot = _system.StoreView;
            var height = NativeContract.Ledger.CurrentIndex(snapshot) + 1;
            using var engine = ApplicationEngine.Create(TriggerType.Application, null, snapshot, settings: _system.Settings);
            return NativeContract.NEO.UnclaimedGas(engine, account, height);
        }

        public bool IsValidAddress(string addressOrHash)
        {
            return ParseAccount(addressOrHash) is not null;
        }

        public string? AddressToScriptHash(string address)
        {
            if (string.IsNullOrWhiteSpace(address)) return null;

            try
            {
                var scriptHash = address.ToScriptHash(_system.Settings.AddressVersion);
                return scriptHash.ToString();
            }
            catch
            {
                return null;
            }
        }

        public string? ScriptHashToAddress(string scriptHash)
        {
            if (string.IsNullOrWhiteSpace(scriptHash)) return null;

            try
            {
                var hash = ParseScriptHash(scriptHash);
                return hash?.ToAddress(_system.Settings.AddressVersion);
            }
            catch
            {
                return null;
            }
        }

        private UInt160? ParseAccount(string addressOrHash)
        {
            if (string.IsNullOrWhiteSpace(addressOrHash)) return null;

            // Try as address first
            try
            {
                return addressOrHash.ToScriptHash(_system.Settings.AddressVersion);
            }
            catch
            {
                // Not a valid address, try as script hash
            }

            // Try as script hash
            return ParseScriptHash(addressOrHash);
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
