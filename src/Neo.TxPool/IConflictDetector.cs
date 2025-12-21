// Copyright (C) 2015-2025 The Neo Project.
//
// IConflictDetector.cs file belongs to the neo project and is free
// software distributed under the MIT software license.

using Neo.Core.Interfaces;

namespace Neo.TxPool;

/// <summary>
/// Interface for detecting conflicts between transactions.
/// </summary>
public interface IConflictDetector
{
    /// <summary>
    /// Detects conflicts between a new transaction and existing pool transactions.
    /// </summary>
    /// <param name="newTransaction">The new transaction to check.</param>
    /// <param name="existingTransactions">Existing transactions in the pool.</param>
    /// <returns>List of conflicting transactions.</returns>
    IEnumerable<ITransactionData> DetectConflicts(
        ITransactionData newTransaction,
        IEnumerable<ITransactionData> existingTransactions);
}

/// <summary>
/// Default conflict detector based on sender and nonce.
/// </summary>
public sealed class DefaultConflictDetector : IConflictDetector
{
    /// <inheritdoc/>
    public IEnumerable<ITransactionData> DetectConflicts(
        ITransactionData newTransaction,
        IEnumerable<ITransactionData> existingTransactions)
    {
        var conflicts = new List<ITransactionData>();
        var newSender = GetSenderKey(newTransaction);
        var newNonce = newTransaction.Nonce;

        foreach (var existing in existingTransactions)
        {
            var existingSender = GetSenderKey(existing);

            // Check for same sender with same nonce (double-spend attempt)
            if (newSender == existingSender && newNonce == existing.Nonce)
            {
                conflicts.Add(existing);
            }
        }

        return conflicts;
    }

    private static string GetSenderKey(ITransactionData tx)
    {
        // Use Sender property from ITransactionData
        return tx.Sender.ToString();
    }
}
