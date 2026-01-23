// Copyright (C) 2015-2025 The Neo Project.
//
// SequentialBlockExecutor.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Core.Interfaces;
using Neo.Network.P2P.Payloads;
using System.Diagnostics;

namespace Neo.Ledger
{
    /// <summary>
    /// Sequential block executor that processes transactions one by one.
    /// This is the baseline implementation ensuring correctness.
    /// </summary>
    public sealed class SequentialBlockExecutor : IBlockExecutor
    {
        private readonly ITransactionExecutor? _transactionExecutor;
        private ExecutionStatistics? _lastStatistics;

        /// <inheritdoc/>
        public bool IsParallelEnabled => false;

        /// <inheritdoc/>
        public IExecutionStatistics? LastStatistics => _lastStatistics;

        /// <summary>
        /// Creates a new sequential block executor.
        /// </summary>
        /// <param name="transactionExecutor">Optional transaction executor for actual execution.</param>
        public SequentialBlockExecutor(ITransactionExecutor? transactionExecutor = null)
        {
            _transactionExecutor = transactionExecutor;
        }

        /// <inheritdoc/>
        public IReadOnlyList<IBlockExecutionResult> Execute(
            IReadOnlyList<object> transactions,
            object snapshot,
            Block block,
            object settings,
            Action<object>? onExecuted = null)
        {
            var stopwatch = Stopwatch.StartNew();
            var results = new List<IBlockExecutionResult>();

            foreach (var tx in transactions)
            {
                var result = ExecuteTransaction(tx, snapshot, block, settings);
                results.Add(result);
                onExecuted?.Invoke(result);
            }

            stopwatch.Stop();
            _lastStatistics = new ExecutionStatistics
            {
                TotalTransactions = transactions.Count,
                BatchCount = 1,
                TotalExecutionTimeMs = stopwatch.ElapsedMilliseconds,
                ConflictsResolved = 0,
                PeakParallelism = 1
            };

            return results;
        }

        private IBlockExecutionResult ExecuteTransaction(
            object transaction,
            object snapshot,
            Block block,
            object settings)
        {
            if (_transactionExecutor != null && transaction is ITransactionData txData)
            {
                return _transactionExecutor.Execute(txData, snapshot, block, settings);
            }

            // Default stub implementation when no executor is provided
            var hash = transaction is ITransactionData td
                ? td.Hash.GetSpan().ToArray()
                : new byte[32];

            return new BlockExecutionResultImpl
            {
                TransactionHash = hash,
                State = 0x01, // HALT
                ShouldCommit = true,
                ApplicationExecuted = null
            };
        }
    }

    /// <summary>
    /// Interface for executing individual transactions.
    /// </summary>
    public interface ITransactionExecutor
    {
        /// <summary>
        /// Executes a single transaction.
        /// </summary>
        IBlockExecutionResult Execute(
            ITransactionData transaction,
            object snapshot,
            Block block,
            object settings);
    }

    /// <summary>
    /// Default implementation of block execution result.
    /// </summary>
    public sealed class BlockExecutionResultImpl : IBlockExecutionResult
    {
        /// <inheritdoc/>
        public required byte[] TransactionHash { get; init; }

        /// <inheritdoc/>
        public required byte State { get; init; }

        /// <inheritdoc/>
        public required bool ShouldCommit { get; init; }

        /// <inheritdoc/>
        public object? ApplicationExecuted { get; init; }
    }

    /// <summary>
    /// Default implementation of execution statistics.
    /// </summary>
    public sealed class ExecutionStatistics : IExecutionStatistics
    {
        private int _successfulTransactions;
        private int _failedTransactions;
        private int _peakParallelism;

        /// <inheritdoc/>
        public int TotalTransactions { get; init; }

        /// <inheritdoc/>
        public int BatchCount { get; init; }

        /// <inheritdoc/>
        public double AverageTransactionsPerBatch =>
            BatchCount > 0 ? (double)TotalTransactions / BatchCount : 0;

        /// <inheritdoc/>
        public long TotalExecutionTimeMs { get; init; }

        /// <inheritdoc/>
        public int ConflictsResolved { get; init; }

        /// <inheritdoc/>
        public int PeakParallelism
        {
            get => _peakParallelism;
            init => _peakParallelism = value;
        }

        /// <summary>
        /// Gets or sets the number of successful transactions.
        /// </summary>
        public int SuccessfulTransactions
        {
            get => _successfulTransactions;
            init => _successfulTransactions = value;
        }

        /// <summary>
        /// Gets or sets the number of failed transactions.
        /// </summary>
        public int FailedTransactions
        {
            get => _failedTransactions;
            init => _failedTransactions = value;
        }

        /// <summary>
        /// Thread-safe increment of successful transaction count.
        /// </summary>
        public void IncrementSuccessful() => Interlocked.Increment(ref _successfulTransactions);

        /// <summary>
        /// Thread-safe increment of failed transaction count.
        /// </summary>
        public void IncrementFailed() => Interlocked.Increment(ref _failedTransactions);

        /// <summary>
        /// Updates peak parallelism if the new value is higher.
        /// </summary>
        public void UpdatePeakParallelism(int value)
        {
            int current;
            do
            {
                current = _peakParallelism;
                if (value <= current) return;
            } while (Interlocked.CompareExchange(ref _peakParallelism, value, current) != current);
        }
    }
}
