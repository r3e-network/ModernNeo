// Copyright (C) 2015-2025 The Neo Project.
//
// IDependencyAnalyzer.cs file belongs to the neo project and is free
// software distributed under the MIT software license.

using System;
using System.Collections.Generic;

namespace Neo.Core.Interfaces;

/// <summary>
/// Abstraction for analyzing dependencies between transactions.
/// Used by the parallel executor to determine which transactions can run concurrently.
/// </summary>
public interface IDependencyAnalyzer
{
    /// <summary>
    /// Analyzes dependencies between transactions and builds a dependency graph.
    /// </summary>
    /// <param name="transactions">The transactions to analyze.</param>
    /// <returns>A dependency graph representing transaction relationships.</returns>
    IDependencyGraph Analyze(IEnumerable<ITransactionData> transactions);

    /// <summary>
    /// Checks if two transactions have a direct dependency.
    /// </summary>
    /// <param name="tx1">First transaction.</param>
    /// <param name="tx2">Second transaction.</param>
    /// <returns>The type of dependency, or None if independent.</returns>
    DependencyType CheckDependency(ITransactionData tx1, ITransactionData tx2);
}

/// <summary>
/// Represents a graph of transaction dependencies.
/// </summary>
public interface IDependencyGraph
{
    /// <summary>
    /// Total number of transactions in the graph.
    /// </summary>
    int TransactionCount { get; }

    /// <summary>
    /// Total number of dependency edges.
    /// </summary>
    int DependencyCount { get; }

    /// <summary>
    /// Gets all transactions in the graph.
    /// </summary>
    IReadOnlyList<ITransactionData> Transactions { get; }

    /// <summary>
    /// Gets the dependencies for a specific transaction.
    /// </summary>
    /// <param name="transactionIndex">Index of the transaction.</param>
    /// <returns>Indices of transactions this one depends on.</returns>
    IReadOnlyList<int> GetDependencies(int transactionIndex);

    /// <summary>
    /// Gets the in-degree (number of dependencies) for a transaction.
    /// </summary>
    /// <param name="transactionIndex">Index of the transaction.</param>
    /// <param name="remainingIndices">Only consider dependencies from these indices.</param>
    /// <returns>The in-degree count.</returns>
    int GetInDegree(int transactionIndex, ISet<int>? remainingIndices = null);

    /// <summary>
    /// Gets all transactions that have no dependencies (in-degree = 0).
    /// </summary>
    /// <param name="remainingIndices">Only consider these indices.</param>
    /// <returns>Indices of independent transactions.</returns>
    IReadOnlyList<int> GetIndependentTransactions(ISet<int>? remainingIndices = null);

    /// <summary>
    /// Checks if the graph contains any cycles.
    /// </summary>
    /// <returns>True if a cycle exists.</returns>
    bool HasCycle();
}

/// <summary>
/// Types of dependencies between transactions.
/// </summary>
[Flags]
public enum DependencyType
{
    /// <summary>
    /// No dependency exists.
    /// </summary>
    None = 0,

    /// <summary>
    /// Same sender account (GAS balance dependency).
    /// </summary>
    SameSender = 1,

    /// <summary>
    /// Explicit conflict via Conflicts attribute.
    /// </summary>
    ExplicitConflict = 2,

    /// <summary>
    /// Same Oracle response ID.
    /// </summary>
    OracleResponseConflict = 4,

    /// <summary>
    /// Potential storage key conflict (same contract access).
    /// </summary>
    StorageConflict = 8,

    /// <summary>
    /// Weak dependency (may not actually conflict at runtime).
    /// </summary>
    Weak = 16
}
