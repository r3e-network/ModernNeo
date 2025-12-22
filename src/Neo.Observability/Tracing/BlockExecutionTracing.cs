// Copyright (C) 2015-2025 The Neo Project.
//
// BlockExecutionTracing.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Observability.Tracing.Propagation;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Neo.Observability.Tracing
{
    /// <summary>
    /// Provides distributed tracing support for block and transaction execution.
    /// </summary>
    public static class BlockExecutionTracing
    {
        /// <summary>
        /// The ActivitySource name for block execution tracing.
        /// </summary>
        public const string ActivitySourceName = "Neo.BlockExecution";

        private static readonly ActivitySource s_activitySource = new(ActivitySourceName, "1.0.0");

        /// <summary>
        /// Gets the ActivitySource for block execution tracing.
        /// </summary>
        public static ActivitySource ActivitySource => s_activitySource;

        #region Block Execution Spans

        /// <summary>
        /// Starts a span for block persistence/execution.
        /// </summary>
        /// <param name="blockIndex">The block index.</param>
        /// <param name="blockHash">The block hash.</param>
        /// <param name="transactionCount">Number of transactions in the block.</param>
        /// <returns>An Activity representing the span, or null if not sampled.</returns>
        public static Activity? StartBlockExecutionSpan(uint blockIndex, string? blockHash, int transactionCount)
        {
            var activity = s_activitySource.StartActivity(
                "block.execute",
                ActivityKind.Internal);

            if (activity != null)
            {
                activity.SetTag("neo.block.index", blockIndex);
                activity.SetTag("neo.block.tx_count", transactionCount);

                if (!string.IsNullOrEmpty(blockHash))
                {
                    activity.SetTag("neo.block.hash", blockHash);
                }
            }

            return activity;
        }

        /// <summary>
        /// Starts a span for block persistence to storage.
        /// </summary>
        /// <param name="blockIndex">The block index.</param>
        /// <param name="blockHash">The block hash.</param>
        /// <returns>An Activity representing the span, or null if not sampled.</returns>
        public static Activity? StartBlockPersistSpan(uint blockIndex, string? blockHash)
        {
            var activity = s_activitySource.StartActivity(
                "block.persist",
                ActivityKind.Internal);

            if (activity != null)
            {
                activity.SetTag("neo.block.index", blockIndex);

                if (!string.IsNullOrEmpty(blockHash))
                {
                    activity.SetTag("neo.block.hash", blockHash);
                }
            }

            return activity;
        }

        /// <summary>
        /// Starts a span for block validation.
        /// </summary>
        /// <param name="blockIndex">The block index.</param>
        /// <param name="blockHash">The block hash.</param>
        /// <returns>An Activity representing the span, or null if not sampled.</returns>
        public static Activity? StartBlockValidationSpan(uint blockIndex, string? blockHash)
        {
            var activity = s_activitySource.StartActivity(
                "block.validate",
                ActivityKind.Internal);

            if (activity != null)
            {
                activity.SetTag("neo.block.index", blockIndex);

                if (!string.IsNullOrEmpty(blockHash))
                {
                    activity.SetTag("neo.block.hash", blockHash);
                }
            }

            return activity;
        }

        #endregion

        #region Transaction Execution Spans

        /// <summary>
        /// Starts a span for transaction execution within a block.
        /// </summary>
        /// <param name="txHash">The transaction hash.</param>
        /// <param name="txIndex">The transaction index within the block.</param>
        /// <param name="blockIndex">The block index.</param>
        /// <returns>An Activity representing the span, or null if not sampled.</returns>
        public static Activity? StartTransactionExecutionSpan(string txHash, int txIndex, uint blockIndex)
        {
            var activity = s_activitySource.StartActivity(
                "tx.execute",
                ActivityKind.Internal);

            if (activity != null)
            {
                activity.SetTag("neo.tx.hash", txHash);
                activity.SetTag("neo.tx.index", txIndex);
                activity.SetTag("neo.block.index", blockIndex);
            }

            return activity;
        }

        /// <summary>
        /// Starts a span for transaction verification.
        /// </summary>
        /// <param name="txHash">The transaction hash.</param>
        /// <returns>An Activity representing the span, or null if not sampled.</returns>
        public static Activity? StartTransactionVerificationSpan(string txHash)
        {
            var activity = s_activitySource.StartActivity(
                "tx.verify",
                ActivityKind.Internal);

            if (activity != null)
            {
                activity.SetTag("neo.tx.hash", txHash);
            }

            return activity;
        }

        /// <summary>
        /// Starts a span for smart contract invocation.
        /// </summary>
        /// <param name="txHash">The transaction hash.</param>
        /// <param name="contractHash">The contract script hash.</param>
        /// <param name="method">The method being invoked.</param>
        /// <returns>An Activity representing the span, or null if not sampled.</returns>
        public static Activity? StartContractInvocationSpan(string txHash, string contractHash, string? method = null)
        {
            var activity = s_activitySource.StartActivity(
                "contract.invoke",
                ActivityKind.Internal);

            if (activity != null)
            {
                activity.SetTag("neo.tx.hash", txHash);
                activity.SetTag("neo.contract.hash", contractHash);

                if (!string.IsNullOrEmpty(method))
                {
                    activity.SetTag("neo.contract.method", method);
                }
            }

            return activity;
        }

        #endregion

        #region Parallel Execution Spans

        /// <summary>
        /// Starts a span for parallel transaction execution batch.
        /// </summary>
        /// <param name="blockIndex">The block index.</param>
        /// <param name="batchIndex">The batch index within the block.</param>
        /// <param name="transactionCount">Number of transactions in this batch.</param>
        /// <returns>An Activity representing the span, or null if not sampled.</returns>
        public static Activity? StartParallelBatchSpan(uint blockIndex, int batchIndex, int transactionCount)
        {
            var activity = s_activitySource.StartActivity(
                "block.parallel_batch",
                ActivityKind.Internal);

            if (activity != null)
            {
                activity.SetTag("neo.block.index", blockIndex);
                activity.SetTag("neo.execution.batch_index", batchIndex);
                activity.SetTag("neo.execution.batch_tx_count", transactionCount);
                activity.SetTag("neo.execution.mode", "parallel");
            }

            return activity;
        }

        /// <summary>
        /// Starts a span for dependency analysis before parallel execution.
        /// </summary>
        /// <param name="blockIndex">The block index.</param>
        /// <param name="transactionCount">Number of transactions to analyze.</param>
        /// <returns>An Activity representing the span, or null if not sampled.</returns>
        public static Activity? StartDependencyAnalysisSpan(uint blockIndex, int transactionCount)
        {
            var activity = s_activitySource.StartActivity(
                "block.dependency_analysis",
                ActivityKind.Internal);

            if (activity != null)
            {
                activity.SetTag("neo.block.index", blockIndex);
                activity.SetTag("neo.execution.tx_count", transactionCount);
            }

            return activity;
        }

        #endregion

        #region Recording Methods

        /// <summary>
        /// Records transaction execution result.
        /// </summary>
        /// <param name="activity">The activity to record on.</param>
        /// <param name="vmState">The VM state (HALT or FAULT).</param>
        /// <param name="gasConsumed">The gas consumed by execution.</param>
        /// <param name="notificationCount">Number of notifications emitted.</param>
        public static void RecordTransactionResult(Activity? activity, string vmState, long gasConsumed, int notificationCount)
        {
            if (activity == null) return;

            activity.SetTag("neo.tx.vm_state", vmState);
            activity.SetTag("neo.tx.gas_consumed", gasConsumed);
            activity.SetTag("neo.tx.notification_count", notificationCount);

            if (vmState == "HALT")
            {
                activity.SetStatus(ActivityStatusCode.Ok);
            }
            else
            {
                activity.SetStatus(ActivityStatusCode.Error, $"VM state: {vmState}");
            }
        }

        /// <summary>
        /// Records block execution completion.
        /// </summary>
        /// <param name="activity">The activity to record on.</param>
        /// <param name="successCount">Number of successfully executed transactions.</param>
        /// <param name="faultCount">Number of faulted transactions.</param>
        /// <param name="totalGasConsumed">Total gas consumed by all transactions.</param>
        /// <param name="executionTimeMs">Total execution time in milliseconds.</param>
        public static void RecordBlockExecutionComplete(
            Activity? activity,
            int successCount,
            int faultCount,
            long totalGasConsumed,
            double executionTimeMs)
        {
            if (activity == null) return;

            activity.SetTag("neo.block.success_count", successCount);
            activity.SetTag("neo.block.fault_count", faultCount);
            activity.SetTag("neo.block.total_gas", totalGasConsumed);
            activity.SetTag("neo.block.execution_time_ms", executionTimeMs);

            if (faultCount == 0)
            {
                activity.SetStatus(ActivityStatusCode.Ok);
            }
            else
            {
                activity.SetStatus(ActivityStatusCode.Ok, $"{faultCount} transactions faulted");
            }

            activity.AddEvent(new ActivityEvent("block.execution_complete", tags: new ActivityTagsCollection
            {
                { "neo.block.success_count", successCount },
                { "neo.block.fault_count", faultCount },
                { "neo.block.total_gas", totalGasConsumed }
            }));
        }

        /// <summary>
        /// Records parallel execution statistics.
        /// </summary>
        /// <param name="activity">The activity to record on.</param>
        /// <param name="batchCount">Number of parallel batches.</param>
        /// <param name="maxParallelism">Maximum parallelism achieved.</param>
        /// <param name="conflictCount">Number of detected conflicts.</param>
        public static void RecordParallelExecutionStats(
            Activity? activity,
            int batchCount,
            int maxParallelism,
            int conflictCount)
        {
            if (activity == null) return;

            activity.SetTag("neo.execution.batch_count", batchCount);
            activity.SetTag("neo.execution.max_parallelism", maxParallelism);
            activity.SetTag("neo.execution.conflict_count", conflictCount);
        }

        /// <summary>
        /// Records dependency analysis results.
        /// </summary>
        /// <param name="activity">The activity to record on.</param>
        /// <param name="independentCount">Number of independent transactions.</param>
        /// <param name="dependentCount">Number of dependent transactions.</param>
        /// <param name="groupCount">Number of dependency groups.</param>
        public static void RecordDependencyAnalysisResult(
            Activity? activity,
            int independentCount,
            int dependentCount,
            int groupCount)
        {
            if (activity == null) return;

            activity.SetTag("neo.execution.independent_count", independentCount);
            activity.SetTag("neo.execution.dependent_count", dependentCount);
            activity.SetTag("neo.execution.group_count", groupCount);
            activity.SetStatus(ActivityStatusCode.Ok);
        }

        /// <summary>
        /// Records a contract invocation result.
        /// </summary>
        /// <param name="activity">The activity to record on.</param>
        /// <param name="success">Whether the invocation succeeded.</param>
        /// <param name="gasConsumed">Gas consumed by the invocation.</param>
        /// <param name="returnType">The return value type.</param>
        public static void RecordContractInvocationResult(
            Activity? activity,
            bool success,
            long gasConsumed,
            string? returnType = null)
        {
            if (activity == null) return;

            activity.SetTag("neo.contract.success", success);
            activity.SetTag("neo.contract.gas_consumed", gasConsumed);

            if (!string.IsNullOrEmpty(returnType))
            {
                activity.SetTag("neo.contract.return_type", returnType);
            }

            activity.SetStatus(success ? ActivityStatusCode.Ok : ActivityStatusCode.Error);
        }

        /// <summary>
        /// Records an exception during execution.
        /// </summary>
        /// <param name="activity">The activity to record on.</param>
        /// <param name="exception">The exception that occurred.</param>
        public static void RecordException(Activity? activity, Exception exception)
        {
            if (activity == null) return;

            activity.SetStatus(ActivityStatusCode.Error, exception.Message);
            activity.AddEvent(new ActivityEvent("exception", tags: new ActivityTagsCollection
            {
                { "exception.type", exception.GetType().FullName },
                { "exception.message", exception.Message },
                { "exception.stacktrace", exception.StackTrace }
            }));
        }

        /// <summary>
        /// Records a successful operation.
        /// </summary>
        /// <param name="activity">The activity to record on.</param>
        public static void RecordSuccess(Activity? activity)
        {
            activity?.SetStatus(ActivityStatusCode.Ok);
        }

        #endregion

        #region Span Links

        /// <summary>
        /// Creates a span link for a transaction within a block.
        /// </summary>
        /// <param name="txHash">The transaction hash.</param>
        /// <param name="blockIndex">The block index.</param>
        /// <returns>A SpanLink for the transaction.</returns>
        public static SpanLink CreateTransactionLink(string txHash, uint blockIndex)
        {
            return new SpanLink(TraceContext.Empty, new Dictionary<string, object?>
            {
                ["neo.tx.hash"] = txHash,
                ["neo.block.index"] = blockIndex
            });
        }

        /// <summary>
        /// Creates a span link for a block.
        /// </summary>
        /// <param name="blockHash">The block hash.</param>
        /// <param name="blockIndex">The block index.</param>
        /// <returns>A SpanLink for the block.</returns>
        public static SpanLink CreateBlockLink(string blockHash, uint blockIndex)
        {
            return SpanLink.ForBlock(blockHash, blockIndex);
        }

        #endregion
    }
}
