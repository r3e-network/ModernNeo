// Copyright (C) 2015-2025 The Neo Project.
//
// IMemoryPool.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Network.P2P.Payloads;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Neo.Ledger
{
    /// <summary>
    /// Defines the interface for a memory pool that caches verified transactions
    /// before they are written into a block.
    /// </summary>
    public interface IMemoryPool : IReadOnlyCollection<Transaction>
    {
        /// <summary>
        /// Raised when a transaction is added to the pool.
        /// </summary>
        event EventHandler<Transaction>? TransactionAdded;

        /// <summary>
        /// Raised when a transaction is removed from the pool.
        /// </summary>
        event EventHandler<TransactionRemovedEventArgs>? TransactionRemoved;

        /// <summary>
        /// Gets the maximum number of transactions that can be stored in the pool.
        /// </summary>
        int Capacity { get; }

        /// <summary>
        /// Gets the number of verified transactions in the pool.
        /// </summary>
        int VerifiedCount { get; }

        /// <summary>
        /// Gets the number of unverified transactions in the pool.
        /// </summary>
        int UnVerifiedCount { get; }

        /// <summary>
        /// Determines whether the pool contains a transaction with the specified hash.
        /// </summary>
        /// <param name="hash">The hash of the transaction.</param>
        /// <returns><see langword="true"/> if the pool contains the transaction; otherwise, <see langword="false"/>.</returns>
        bool ContainsKey(UInt256 hash);

        /// <summary>
        /// Tries to get a transaction from the pool.
        /// </summary>
        /// <param name="hash">The hash of the transaction.</param>
        /// <param name="tx">The transaction if found.</param>
        /// <returns><see langword="true"/> if the transaction was found; otherwise, <see langword="false"/>.</returns>
        bool TryGetValue(UInt256 hash, [NotNullWhen(true)] out Transaction? tx);

        /// <summary>
        /// Gets the verified transactions in the pool.
        /// </summary>
        /// <returns>An enumerable of verified transactions.</returns>
        IEnumerable<Transaction> GetVerifiedTransactions();

        /// <summary>
        /// Gets both verified and unverified transactions from the pool.
        /// </summary>
        /// <param name="verifiedTransactions">The verified transactions.</param>
        /// <param name="unverifiedTransactions">The unverified transactions.</param>
        void GetVerifiedAndUnverifiedTransactions(
            out IEnumerable<Transaction> verifiedTransactions,
            out IEnumerable<Transaction> unverifiedTransactions);

    }
}
