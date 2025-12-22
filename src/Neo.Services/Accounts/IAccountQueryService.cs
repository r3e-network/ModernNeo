// Copyright (C) 2015-2025 The Neo Project.
//
// IAccountQueryService.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using System.Numerics;

namespace Neo.Services.Accounts
{
    /// <summary>
    /// Service for querying account balances and information.
    /// </summary>
    public interface IAccountQueryService
    {
        /// <summary>
        /// Gets the NEO balance of an account.
        /// </summary>
        /// <param name="addressOrHash">Account address or script hash (hex).</param>
        /// <returns>NEO balance in whole units.</returns>
        BigInteger GetNeoBalance(string addressOrHash);

        /// <summary>
        /// Gets the GAS balance of an account.
        /// </summary>
        /// <param name="addressOrHash">Account address or script hash (hex).</param>
        /// <returns>GAS balance in datoshi (1 GAS = 10^8 datoshi).</returns>
        BigInteger GetGasBalance(string addressOrHash);

        /// <summary>
        /// Gets the unclaimed GAS for an account.
        /// </summary>
        /// <param name="addressOrHash">Account address or script hash (hex).</param>
        /// <returns>Unclaimed GAS in datoshi.</returns>
        BigInteger GetUnclaimedGas(string addressOrHash);

        /// <summary>
        /// Checks if an address/hash is valid.
        /// </summary>
        /// <param name="addressOrHash">Account address or script hash (hex).</param>
        /// <returns>True if valid, false otherwise.</returns>
        bool IsValidAddress(string addressOrHash);

        /// <summary>
        /// Converts address to script hash.
        /// </summary>
        /// <param name="address">Neo address (e.g., NXV7ZhHiyM1aHXwpVsRZC6BwNFP2jghXAq).</param>
        /// <returns>Script hash as hex string, or null if invalid.</returns>
        string? AddressToScriptHash(string address);

        /// <summary>
        /// Converts script hash to address.
        /// </summary>
        /// <param name="scriptHash">Script hash (hex, with or without 0x prefix).</param>
        /// <returns>Neo address, or null if invalid.</returns>
        string? ScriptHashToAddress(string scriptHash);
    }
}
