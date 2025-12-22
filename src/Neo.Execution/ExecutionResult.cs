// Copyright (C) 2015-2025 The Neo Project.
//
// ExecutionResult.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Core.Interfaces;

namespace Neo.Execution
{
    /// <summary>
    /// Represents the result of executing a single transaction.
    /// </summary>
    public class ExecutionResult : IExecutionResult
    {
        /// <inheritdoc/>
        public byte[] TransactionHash { get; init; } = Array.Empty<byte>();

        /// <inheritdoc/>
        public bool IsSuccess { get; init; }

        /// <inheritdoc/>
        public byte State { get; init; }

        /// <inheritdoc/>
        public long GasConsumed { get; init; }

        /// <inheritdoc/>
        public string? Exception { get; init; }

        /// <inheritdoc/>
        public IReadOnlyList<object> Notifications { get; init; } = Array.Empty<object>();

        /// <summary>
        /// Creates a successful execution result.
        /// </summary>
        public static ExecutionResult Success(byte[] txHash, long gasConsumed, IReadOnlyList<object>? notifications = null)
        {
            return new ExecutionResult
            {
                TransactionHash = txHash,
                IsSuccess = true,
                State = 1, // HALT
                GasConsumed = gasConsumed,
                Notifications = notifications ?? Array.Empty<object>()
            };
        }

        /// <summary>
        /// Creates a failed execution result.
        /// </summary>
        public static ExecutionResult Failure(byte[] txHash, string exception, long gasConsumed = 0)
        {
            return new ExecutionResult
            {
                TransactionHash = txHash,
                IsSuccess = false,
                State = 2, // FAULT
                GasConsumed = gasConsumed,
                Exception = exception
            };
        }
    }

    /// <summary>
    /// Statistics about parallel execution performance.
    /// </summary>
    public class ExecutionStatistics : IExecutionStatistics
    {
        private int _successfulTransactions;
        private int _failedTransactions;
        private int _retriedTransactions;

        /// <inheritdoc/>
        public int TotalTransactions { get; set; }

        /// <inheritdoc/>
        public int BatchCount { get; set; }

        /// <inheritdoc/>
        public double AverageTransactionsPerBatch =>
            BatchCount > 0 ? (double)TotalTransactions / BatchCount : 0;

        /// <inheritdoc/>
        public long TotalExecutionTimeMs { get; set; }

        /// <inheritdoc/>
        public int ConflictsResolved { get; set; }

        /// <inheritdoc/>
        public int PeakParallelism { get; set; }

        /// <summary>
        /// Number of transactions that executed successfully.
        /// </summary>
        public int SuccessfulTransactions
        {
            get => _successfulTransactions;
            set => _successfulTransactions = value;
        }

        /// <summary>
        /// Number of transactions that failed.
        /// </summary>
        public int FailedTransactions
        {
            get => _failedTransactions;
            set => _failedTransactions = value;
        }

        /// <summary>
        /// Number of transactions that were retried due to conflicts.
        /// </summary>
        public int RetriedTransactions
        {
            get => _retriedTransactions;
            set => _retriedTransactions = value;
        }

        /// <summary>
        /// Thread-safe increment of successful transactions.
        /// </summary>
        public void IncrementSuccessful() => Interlocked.Increment(ref _successfulTransactions);

        /// <summary>
        /// Thread-safe increment of failed transactions.
        /// </summary>
        public void IncrementFailed() => Interlocked.Increment(ref _failedTransactions);

        /// <summary>
        /// Thread-safe increment of retried transactions.
        /// </summary>
        public void IncrementRetried() => Interlocked.Increment(ref _retriedTransactions);

        /// <summary>
        /// Average execution time per transaction in microseconds.
        /// </summary>
        public double AverageExecutionTimePerTxUs =>
            TotalTransactions > 0 ? (double)TotalExecutionTimeMs * 1000 / TotalTransactions : 0;

        /// <summary>
        /// Estimated speedup factor compared to sequential execution.
        /// </summary>
        public double EstimatedSpeedup =>
            BatchCount > 0 ? (double)TotalTransactions / BatchCount : 1;

        /// <summary>
        /// Creates a summary string.
        /// </summary>
        public override string ToString()
        {
            return $"Executed {TotalTransactions} tx in {BatchCount} batches ({TotalExecutionTimeMs}ms, " +
                   $"peak parallelism: {PeakParallelism}, conflicts resolved: {ConflictsResolved})";
        }
    }
}
