// Copyright (C) 2015-2025 The Neo Project.
//
// IMemoryPoolGrain.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Core.Interfaces;
using Neo.Orleans.States;

namespace Neo.Orleans.Interfaces
{
    /// <summary>
    /// Orleans Grain interface for transaction memory pool management.
    /// Replaces Akka.NET MemoryPool component with Verified/Unverified separation.
    /// </summary>
    public interface IMemoryPoolGrain : IGrainWithIntegerKey
    {
        /// <summary>
        /// Adds a transaction to the verified pool.
        /// </summary>
        Task<MemoryPoolAddResult> AddTransactionAsync(ITransactionData transaction);

        /// <summary>
        /// Gets verified transactions up to the specified count, sorted by priority.
        /// </summary>
        Task<IReadOnlyList<PoolItemState>> GetVerifiedTransactionsAsync(int maxCount);

        /// <summary>
        /// Gets unverified transactions up to the specified count.
        /// </summary>
        Task<IReadOnlyList<PoolItemState>> GetUnverifiedTransactionsAsync(int maxCount);

        /// <summary>
        /// Removes a transaction from the pool (verified or unverified).
        /// </summary>
        Task<bool> RemoveTransactionAsync(byte[] hash);

        /// <summary>
        /// Tries to remove a transaction from the unverified pool.
        /// </summary>
        Task<bool> TryRemoveUnverifiedAsync(byte[] hash);

        /// <summary>
        /// Gets the total pool count (verified + unverified).
        /// </summary>
        Task<int> GetCountAsync();

        /// <summary>
        /// Gets the verified transaction count.
        /// </summary>
        Task<int> GetVerifiedCountAsync();

        /// <summary>
        /// Gets the unverified transaction count.
        /// </summary>
        Task<int> GetUnverifiedCountAsync();

        /// <summary>
        /// Checks if a transaction exists in the pool (verified or unverified).
        /// </summary>
        Task<bool> ContainsAsync(byte[] hash);

        /// <summary>
        /// Checks if a hash conflicts with any transaction in the pool.
        /// </summary>
        Task<bool> ContainsConflictAsync(byte[] hash);

        /// <summary>
        /// Clears all transactions from the pool.
        /// </summary>
        Task ClearAsync();

        /// <summary>
        /// Invalidates all verified transactions, moving them to unverified.
        /// Called when consensus fills the memory pool.
        /// </summary>
        Task InvalidateAllTransactionsAsync();

        /// <summary>
        /// Updates the pool after a block is persisted.
        /// Removes included transactions and moves verified to unverified.
        /// </summary>
        Task UpdateForBlockPersistedAsync(uint blockIndex, IEnumerable<byte[]> includedTxHashes);

        /// <summary>
        /// Re-verifies top unverified transactions if needed.
        /// Returns true if more transactions need re-verification.
        /// </summary>
        Task<bool> ReVerifyTopUnverifiedAsync(int maxCount);

        /// <summary>
        /// Removes expired transactions based on current block height.
        /// </summary>
        Task<int> RemoveExpiredTransactionsAsync(uint currentBlockHeight);
    }

    /// <summary>
    /// Result of adding a transaction to the memory pool.
    /// </summary>
    public enum MemoryPoolAddResult
    {
        /// <summary>Transaction was added successfully.</summary>
        Succeed,
        /// <summary>Transaction already exists in the pool.</summary>
        AlreadyInPool,
        /// <summary>Transaction conflicts with existing pool transactions.</summary>
        HasConflicts,
        /// <summary>Pool is at capacity and transaction has lower priority.</summary>
        OutOfMemory,
        /// <summary>Transaction is invalid.</summary>
        Invalid,
        /// <summary>Transaction has expired.</summary>
        Expired
    }
}
