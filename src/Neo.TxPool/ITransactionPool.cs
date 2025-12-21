// Copyright (C) 2015-2025 The Neo Project.
//
// ITransactionPool.cs file belongs to the neo project and is free
// software distributed under the MIT software license.

using Neo.Core.Interfaces;

namespace Neo.TxPool;

/// <summary>
/// Defines the interface for a transaction memory pool.
/// This is a dependency-free abstraction that works with ITransactionData.
/// </summary>
public interface ITransactionPool
{
    /// <summary>
    /// Raised when a transaction is added to the pool.
    /// </summary>
    event EventHandler<ITransactionData>? TransactionAdded;

    /// <summary>
    /// Raised when a transaction is removed from the pool.
    /// </summary>
    event EventHandler<TransactionRemovedEventArgs>? TransactionRemoved;

    /// <summary>
    /// Gets the maximum number of transactions that can be stored in the pool.
    /// </summary>
    int Capacity { get; }

    /// <summary>
    /// Gets the total number of transactions in the pool.
    /// </summary>
    int Count { get; }

    /// <summary>
    /// Gets the number of verified transactions in the pool.
    /// </summary>
    int VerifiedCount { get; }

    /// <summary>
    /// Gets the number of unverified transactions in the pool.
    /// </summary>
    int UnverifiedCount { get; }

    /// <summary>
    /// Determines whether the pool contains a transaction with the specified hash.
    /// </summary>
    /// <param name="hash">The hash of the transaction (32 bytes).</param>
    /// <returns><see langword="true"/> if the pool contains the transaction; otherwise, <see langword="false"/>.</returns>
    bool Contains(byte[] hash);

    /// <summary>
    /// Tries to get a transaction from the pool.
    /// </summary>
    /// <param name="hash">The hash of the transaction (32 bytes).</param>
    /// <param name="transaction">The transaction if found.</param>
    /// <returns><see langword="true"/> if the transaction was found; otherwise, <see langword="false"/>.</returns>
    bool TryGet(byte[] hash, out ITransactionData? transaction);

    /// <summary>
    /// Adds a transaction to the pool.
    /// </summary>
    /// <param name="transaction">The transaction to add.</param>
    /// <returns><see langword="true"/> if the transaction was added; otherwise, <see langword="false"/>.</returns>
    bool TryAdd(ITransactionData transaction);

    /// <summary>
    /// Removes a transaction from the pool.
    /// </summary>
    /// <param name="hash">The hash of the transaction to remove.</param>
    /// <returns><see langword="true"/> if the transaction was removed; otherwise, <see langword="false"/>.</returns>
    bool TryRemove(byte[] hash);

    /// <summary>
    /// Gets the verified transactions in the pool.
    /// </summary>
    /// <param name="maxCount">Maximum number of transactions to return.</param>
    /// <returns>An enumerable of verified transactions.</returns>
    IEnumerable<ITransactionData> GetVerifiedTransactions(int maxCount);

    /// <summary>
    /// Gets all verified transactions in the pool.
    /// </summary>
    /// <returns>An enumerable of all verified transactions.</returns>
    IEnumerable<ITransactionData> GetVerifiedTransactions();

    /// <summary>
    /// Clears all transactions from the pool.
    /// </summary>
    void Clear();
}
