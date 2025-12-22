// Copyright (C) 2015-2025 The Neo Project.
//
// IMemoryPoolQuery.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Neo.Core.Abstractions.MemPool
{
    /// <summary>
    /// Provides read-only access to the memory pool (transaction pool).
    /// </summary>
    public interface IMemoryPoolQuery
    {
        /// <summary>
        /// Gets the number of verified transactions in the pool.
        /// </summary>
        int VerifiedCount { get; }

        /// <summary>
        /// Gets the number of unverified transactions in the pool.
        /// </summary>
        int UnverifiedCount { get; }

        /// <summary>
        /// Gets the total number of transactions in the pool.
        /// </summary>
        int Count { get; }

        /// <summary>
        /// Checks if a transaction with the specified hash exists in the pool.
        /// </summary>
        /// <param name="hash">The transaction hash.</param>
        /// <returns>True if the transaction exists, false otherwise.</returns>
        bool ContainsKey(byte[] hash);

        /// <summary>
        /// Gets verified transactions from the pool.
        /// </summary>
        /// <param name="maxCount">Maximum number of transactions to return.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>An enumerable of verified transactions.</returns>
        Task<IEnumerable<ITransactionData>> GetVerifiedTransactionsAsync(int maxCount, CancellationToken cancellationToken = default);

        /// <summary>
        /// Tries to get a transaction by its hash.
        /// </summary>
        /// <param name="hash">The transaction hash.</param>
        /// <param name="transaction">The transaction if found.</param>
        /// <returns>True if the transaction was found, false otherwise.</returns>
        bool TryGetValue(byte[] hash, out ITransactionData? transaction);
    }
}
