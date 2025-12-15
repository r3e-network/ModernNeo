// Copyright (C) 2015-2025 The Neo Project.
//
// IBlockchainOperations.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Persistence;

namespace Neo.Ledger
{
    /// <summary>
    /// Defines the interface for blockchain query and state operations.
    /// </summary>
    public interface IBlockchainOperations
    {
        /// <summary>
        /// Gets the memory pool associated with this blockchain.
        /// </summary>
        MemoryPool MemPool { get; }

        /// <summary>
        /// Gets the header cache for this blockchain.
        /// </summary>
        HeaderCache HeaderCache { get; }

        /// <summary>
        /// Gets a readonly view of the store.
        /// </summary>
        StoreCache StoreView { get; }

        /// <summary>
        /// Gets a snapshot of the current blockchain state.
        /// </summary>
        /// <returns>A snapshot of the blockchain state.</returns>
        StoreCache GetSnapshotCache();

        /// <summary>
        /// Determines whether the blockchain or memory pool contains a transaction with the specified hash.
        /// </summary>
        /// <param name="hash">The hash of the transaction.</param>
        /// <returns>The type indicating where the transaction exists.</returns>
        ContainsTransactionType ContainsTransaction(UInt256 hash);
    }
}
