// Copyright (C) 2015-2025 The Neo Project.
//
// IExecutionScheduler.cs file belongs to the neo project and is free
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
    /// Abstraction for scheduling transaction execution based on dependencies.
    /// </summary>
    public interface IExecutionScheduler
    {
        /// <summary>
        /// Schedules transactions into batches for parallel execution.
        /// Each batch contains transactions that can be executed concurrently.
        /// </summary>
        /// <param name="dependencyGraph">The dependency graph from analysis.</param>
        /// <returns>Batches of transaction indices for execution.</returns>
        IEnumerable<IExecutionBatch> Schedule(IDependencyGraph dependencyGraph);

        /// <summary>
        /// Gets the scheduling strategy being used.
        /// </summary>
        SchedulingStrategy Strategy { get; }
    }

    /// <summary>
    /// Represents a batch of transactions to execute in parallel.
    /// </summary>
    public interface IExecutionBatch
    {
        /// <summary>
        /// The batch number (execution order).
        /// </summary>
        int BatchNumber { get; }

        /// <summary>
        /// Indices of transactions in this batch.
        /// </summary>
        IReadOnlyList<int> TransactionIndices { get; }

        /// <summary>
        /// Whether this batch supports optimistic execution with conflict retry.
        /// </summary>
        bool SupportsOptimisticExecution { get; }
    }

    /// <summary>
    /// Scheduling strategies for parallel execution.
    /// </summary>
    public enum SchedulingStrategy
    {
        /// <summary>
        /// Conservative: Only parallelize transactions with no dependencies.
        /// </summary>
        Conservative,

        /// <summary>
        /// Optimistic: Allow weak dependencies with conflict retry.
        /// </summary>
        Optimistic,

        /// <summary>
        /// Aggressive: Maximum parallelism with speculative execution.
        /// </summary>
        Aggressive
    }
}
