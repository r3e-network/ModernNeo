// Copyright (C) 2015-2025 The Neo Project.
//
// IParallelExecutor.cs file belongs to the neo project and is free
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

namespace Neo.Core.Interfaces
{
    /// <summary>
    /// Abstraction for parallel transaction execution within a block.
    /// Enables dependency injection and testing of execution strategies.
    /// </summary>
    public interface IParallelExecutor
    {
        /// <summary>
        /// Executes a batch of transactions in parallel where possible.
        /// </summary>
        /// <param name="transactions">The transactions to execute.</param>
        /// <param name="snapshot">The data cache snapshot for state access.</param>
        /// <param name="block">The block context for execution.</param>
        /// <param name="settings">The protocol settings.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Execution results for each transaction.</returns>
        Task<IReadOnlyList<IExecutionResult>> ExecuteAsync(
            IEnumerable<ITransactionData> transactions,
            object snapshot,
            IBlockData block,
            object settings,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the maximum degree of parallelism for execution.
        /// </summary>
        int MaxDegreeOfParallelism { get; }

        /// <summary>
        /// Gets execution statistics from the last execution.
        /// </summary>
        IExecutionStatistics? LastExecutionStatistics { get; }
    }

    /// <summary>
    /// Represents the result of executing a single transaction.
    /// </summary>
    public interface IExecutionResult
    {
        /// <summary>
        /// The transaction hash.
        /// </summary>
        byte[] TransactionHash { get; }

        /// <summary>
        /// Whether the execution was successful (HALT state).
        /// </summary>
        bool IsSuccess { get; }

        /// <summary>
        /// The execution state (HALT, FAULT, BREAK, NONE).
        /// </summary>
        byte State { get; }

        /// <summary>
        /// The GAS consumed by execution.
        /// </summary>
        long GasConsumed { get; }

        /// <summary>
        /// Exception message if execution failed.
        /// </summary>
        string? Exception { get; }

        /// <summary>
        /// Notifications emitted during execution.
        /// </summary>
        IReadOnlyList<object> Notifications { get; }
    }

    /// <summary>
    /// Statistics about parallel execution performance.
    /// </summary>
    public interface IExecutionStatistics
    {
        /// <summary>
        /// Total number of transactions executed.
        /// </summary>
        int TotalTransactions { get; }

        /// <summary>
        /// Number of parallel batches executed.
        /// </summary>
        int BatchCount { get; }

        /// <summary>
        /// Average transactions per batch.
        /// </summary>
        double AverageTransactionsPerBatch { get; }

        /// <summary>
        /// Total execution time in milliseconds.
        /// </summary>
        long TotalExecutionTimeMs { get; }

        /// <summary>
        /// Number of conflicts detected and resolved.
        /// </summary>
        int ConflictsResolved { get; }

        /// <summary>
        /// Peak parallelism achieved.
        /// </summary>
        int PeakParallelism { get; }
    }
}
