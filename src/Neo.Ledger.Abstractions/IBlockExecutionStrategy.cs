// Copyright (C) 2015-2025 The Neo Project.
//
// IBlockExecutionStrategy.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Core.Abstractions;
using Neo.Core.Abstractions.Storage;
using System;
using System.Collections.Generic;

namespace Neo.Ledger.Abstractions
{
    /// <summary>
    /// Defines a strategy for executing transactions within a block.
    /// Implementations can provide sequential or parallel execution.
    /// </summary>
    public interface IBlockExecutionStrategy
    {
        /// <summary>
        /// Gets whether parallel execution is enabled.
        /// </summary>
        bool IsParallelEnabled { get; }

        /// <summary>
        /// Gets the statistics from the last execution.
        /// </summary>
        IExecutionStatistics? LastStatistics { get; }

        /// <summary>
        /// Executes transactions in a block.
        /// </summary>
        /// <param name="transactions">The transactions to execute.</param>
        /// <param name="snapshot">The storage snapshot to use.</param>
        /// <param name="block">The block containing the transactions.</param>
        /// <param name="onExecuted">Optional callback invoked after each transaction execution.</param>
        /// <returns>The execution results for each transaction.</returns>
        IReadOnlyList<IBlockExecutionResult> Execute(
            IReadOnlyList<ITransactionData> transactions,
            IStorageSnapshot snapshot,
            IBlockData block,
            Action<ITransactionExecutionContext>? onExecuted = null);
    }

    /// <summary>
    /// Represents the result of executing a single transaction in a block.
    /// </summary>
    public interface IBlockExecutionResult
    {
        /// <summary>
        /// Gets the transaction hash.
        /// </summary>
        byte[] TransactionHash { get; }

        /// <summary>
        /// Gets whether the transaction should be committed.
        /// </summary>
        bool ShouldCommit { get; }

        /// <summary>
        /// Gets the execution context with details about the execution.
        /// </summary>
        ITransactionExecutionContext? Context { get; }
    }

    /// <summary>
    /// Represents the context of a transaction execution.
    /// </summary>
    public interface ITransactionExecutionContext
    {
        /// <summary>
        /// Gets the transaction that was executed.
        /// </summary>
        ITransactionData Transaction { get; }

        /// <summary>
        /// Gets the VM state after execution.
        /// </summary>
        byte State { get; }

        /// <summary>
        /// Gets the gas consumed during execution.
        /// </summary>
        long GasConsumed { get; }

        /// <summary>
        /// Gets the exception message if execution failed.
        /// </summary>
        string? Exception { get; }

        /// <summary>
        /// Gets the notifications generated during execution.
        /// </summary>
        IReadOnlyList<INotification> Notifications { get; }
    }

    /// <summary>
    /// Represents a notification generated during contract execution.
    /// </summary>
    public interface INotification
    {
        /// <summary>
        /// Gets the script hash of the contract that generated the notification.
        /// </summary>
        byte[] ScriptHash { get; }

        /// <summary>
        /// Gets the event name.
        /// </summary>
        string EventName { get; }
    }

    /// <summary>
    /// Represents statistics from block execution.
    /// </summary>
    public interface IExecutionStatistics
    {
        /// <summary>
        /// Gets the total number of transactions executed.
        /// </summary>
        int TotalTransactions { get; }

        /// <summary>
        /// Gets the number of successful transactions.
        /// </summary>
        int SuccessfulTransactions { get; }

        /// <summary>
        /// Gets the number of failed transactions.
        /// </summary>
        int FailedTransactions { get; }

        /// <summary>
        /// Gets the number of execution batches (for parallel execution).
        /// </summary>
        int BatchCount { get; }

        /// <summary>
        /// Gets the peak parallelism achieved.
        /// </summary>
        int PeakParallelism { get; }
    }
}
