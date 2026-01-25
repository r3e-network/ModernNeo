// Copyright (C) 2015-2025 The Neo Project.
//
// IMemoryPoolMutator.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Core.Abstractions.Blockchain;
using System.Threading;
using System.Threading.Tasks;

namespace Neo.Core.Abstractions.MemPool
{
    /// <summary>
    /// Provides write operations for the memory pool (transaction pool).
    /// </summary>
    public interface IMemoryPoolMutator : IMemoryPoolQuery
    {
        /// <summary>
        /// Adds a transaction to the memory pool.
        /// </summary>
        /// <param name="transaction">The transaction to add.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The verification result.</returns>
        Task<VerifyResult> AddTransactionAsync(ITransactionData transaction, CancellationToken cancellationToken = default);

        /// <summary>
        /// Removes a transaction from the memory pool.
        /// </summary>
        /// <param name="hash">The transaction hash to remove.</param>
        /// <returns>True if the transaction was removed, false if not found.</returns>
        bool Remove(byte[] hash);

        /// <summary>
        /// Clears all transactions from the memory pool.
        /// </summary>
        void Clear();
    }
}
