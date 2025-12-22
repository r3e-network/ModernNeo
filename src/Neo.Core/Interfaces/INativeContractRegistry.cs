// Copyright (C) 2015-2025 The Neo Project.
//
// INativeContractRegistry.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using System.Collections.Generic;

namespace Neo.Core.Interfaces
{
    /// <summary>
    /// Abstraction for native contract registry operations.
    /// Enables dependency injection and testing of native contract access.
    /// </summary>
    public interface INativeContractRegistry
    {
        /// <summary>
        /// Gets all registered native contracts.
        /// </summary>
        IReadOnlyCollection<INativeContractData> Contracts { get; }

        /// <summary>
        /// Gets a native contract by its script hash.
        /// </summary>
        /// <param name="hash">The script hash of the contract.</param>
        /// <returns>The native contract, or null if not found.</returns>
        INativeContractData? GetContract(byte[] hash);

        /// <summary>
        /// Gets a native contract by its ID.
        /// </summary>
        /// <param name="id">The ID of the contract.</param>
        /// <returns>The native contract, or null if not found.</returns>
        INativeContractData? GetContractById(int id);

        /// <summary>
        /// Checks if a contract with the specified hash is a native contract.
        /// </summary>
        /// <param name="hash">The script hash to check.</param>
        /// <returns>True if the hash belongs to a native contract.</returns>
        bool IsNative(byte[] hash);

        /// <summary>
        /// Gets the current block index from the Ledger contract.
        /// </summary>
        /// <param name="snapshot">The data cache snapshot.</param>
        /// <returns>The current block index.</returns>
        uint GetCurrentIndex(object snapshot);

        /// <summary>
        /// Gets a block by index from the Ledger contract.
        /// </summary>
        /// <param name="snapshot">The data cache snapshot.</param>
        /// <param name="index">The block index.</param>
        /// <returns>The block data, or null if not found.</returns>
        IBlockData? GetBlock(object snapshot, uint index);

        /// <summary>
        /// Gets a block by hash from the Ledger contract.
        /// </summary>
        /// <param name="snapshot">The data cache snapshot.</param>
        /// <param name="hash">The block hash.</param>
        /// <returns>The block data, or null if not found.</returns>
        IBlockData? GetBlock(object snapshot, byte[] hash);

        /// <summary>
        /// Checks if a transaction exists in the ledger.
        /// </summary>
        /// <param name="snapshot">The data cache snapshot.</param>
        /// <param name="hash">The transaction hash.</param>
        /// <returns>True if the transaction exists.</returns>
        bool ContainsTransaction(object snapshot, byte[] hash);

        /// <summary>
        /// Gets the fee per byte from Policy contract.
        /// </summary>
        /// <param name="snapshot">The data cache snapshot.</param>
        /// <returns>The fee per byte.</returns>
        long GetFeePerByte(object snapshot);

        /// <summary>
        /// Gets the storage price from Policy contract.
        /// </summary>
        /// <param name="snapshot">The data cache snapshot.</param>
        /// <returns>The storage price.</returns>
        uint GetStoragePrice(object snapshot);
    }
}
