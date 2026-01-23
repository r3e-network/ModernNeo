// Copyright (C) 2015-2025 The Neo Project.
//
// ParallelExecutor.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Core.Interfaces;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.SmartContract;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime;

namespace Neo.Execution
{
    /// <summary>
    /// Handles the parallel execution of transactions within a block.
    /// Uses dependency analysis to maximize parallelism while maintaining correctness.
    /// </summary>
    public class ParallelExecutor : IParallelExecutor
    {
        private readonly IDependencyAnalyzer _dependencyAnalyzer;
        private readonly IExecutionScheduler _scheduler;
        private ExecutionStatistics? _lastStatistics;

        /// <summary>
        /// Memory budget for NoGC region during critical execution paths (default: 16MB).
        /// Set to 0 to disable NoGC region protection.
        /// </summary>
        public long NoGCRegionBudget { get; set; } = 16 * 1024 * 1024;

        /// <inheritdoc/>
        public int MaxDegreeOfParallelism { get; set; } = Environment.ProcessorCount;

        /// <inheritdoc/>
        public IExecutionStatistics? LastExecutionStatistics => _lastStatistics;

        /// <summary>
        /// Creates a new parallel executor with default analyzer and scheduler.
        /// </summary>
        public ParallelExecutor()
            : this(new DependencyAnalyzer(), new ExecutionScheduler())
        {
        }

        /// <summary>
        /// Creates a new parallel executor with custom analyzer and scheduler.
        /// </summary>
        public ParallelExecutor(IDependencyAnalyzer analyzer, IExecutionScheduler scheduler)
        {
            _dependencyAnalyzer = analyzer;
            _scheduler = scheduler;
        }

        /// <inheritdoc/>
        public async Task<IReadOnlyList<IExecutionResult>> ExecuteAsync(
            IEnumerable<ITransactionData> transactions,
            object snapshot,
            IBlockData block,
            object settings,
            CancellationToken cancellationToken = default)
        {
            var txList = transactions.ToList();
            if (txList.Count == 0)
                return Array.Empty<IExecutionResult>();

            if (block is not Block blockPayload)
                throw new ArgumentException("Block must be of type Neo.Network.P2P.Payloads.Block", nameof(block));

            var stopwatch = Stopwatch.StartNew();
            var statistics = new ExecutionStatistics { TotalTransactions = txList.Count };

            // 1. Analyze dependencies
            var dependencyGraph = _dependencyAnalyzer.Analyze(txList);

            // 2. Schedule execution batches
            var batches = _scheduler.Schedule(dependencyGraph).ToList();
            statistics.BatchCount = batches.Count;

            // 3. Execute batches
            var results = new ConcurrentDictionary<int, IExecutionResult>();

            foreach (var batch in batches)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var batchResults = await ExecuteBatchAsync(
                    batch, txList, snapshot, blockPayload, settings, statistics, cancellationToken);

                foreach (var (idx, result) in batchResults)
                {
                    results[idx] = result;
                }

                statistics.PeakParallelism = Math.Max(statistics.PeakParallelism, batch.TransactionIndices.Count);
            }

            stopwatch.Stop();
            statistics.TotalExecutionTimeMs = stopwatch.ElapsedMilliseconds;
            _lastStatistics = statistics;

            // Return results in original transaction order
            return Enumerable.Range(0, txList.Count)
                .Select(i => results.TryGetValue(i, out var r) ? r : CreateFallbackResult(txList[i]))
                .ToList();
        }

        private async Task<List<(int Index, IExecutionResult Result)>> ExecuteBatchAsync(
            IExecutionBatch batch,
            List<ITransactionData> allTransactions,
            object snapshot,
            Block block,
            object settings,
            ExecutionStatistics statistics,
            CancellationToken cancellationToken)
        {
            var results = new ConcurrentBag<(int Index, IExecutionResult Result)>();

            var parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = Math.Min(MaxDegreeOfParallelism, batch.TransactionIndices.Count),
                CancellationToken = cancellationToken
            };

            // Try to enter NoGC region for critical execution path
            bool noGCStarted = false;
            if (NoGCRegionBudget > 0)
            {
                try
                {
                    noGCStarted = GC.TryStartNoGCRegion(NoGCRegionBudget, disallowFullBlockingGC: true);
                }
                catch (InvalidOperationException)
                {
                    // Already in NoGC region or not supported
                    noGCStarted = false;
                }
            }

            try
            {
                await Parallel.ForEachAsync(
                    batch.TransactionIndices,
                    parallelOptions,
                    async (txIndex, ct) =>
                    {
                        var tx = allTransactions[txIndex];
                        var result = await ExecuteTransactionAsync(tx, snapshot, block, settings, ct);

                        if (result.IsSuccess)
                            statistics.IncrementSuccessful();
                        else
                            statistics.IncrementFailed();

                        results.Add((txIndex, result));
                    });
            }
            finally
            {
                // Exit NoGC region if we started one
                if (noGCStarted && GCSettings.LatencyMode == GCLatencyMode.NoGCRegion)
                {
                    try
                    {
                        GC.EndNoGCRegion();
                    }
                    catch (InvalidOperationException)
                    {
                        // Not in NoGC region (may have been exited due to allocation)
                    }
                }
            }

            return results.ToList();
        }

        private async Task<IExecutionResult> ExecuteTransactionAsync(
            ITransactionData txData,
            object snapshot,
            Block block,
            object settings,
            CancellationToken cancellationToken)
        {
            // Type validation
            if (txData is not Transaction tx)
            {
                return ExecutionResult.Failure(
                    txData.Hash?.GetSpan().ToArray() ?? Array.Empty<byte>(),
                    "Transaction must be of type Neo.Network.P2P.Payloads.Transaction");
            }

            if (snapshot is not StoreCache storeCache)
            {
                return ExecutionResult.Failure(
                    tx.Hash.GetSpan().ToArray(),
                    "Snapshot must be of type StoreCache");
            }

            if (settings is not ProtocolSettings protocolSettings)
            {
                return ExecutionResult.Failure(
                    tx.Hash.GetSpan().ToArray(),
                    "Settings must be of type ProtocolSettings");
            }

            try
            {
                // Clone the cache for isolated execution
                var clonedCache = storeCache.CloneCache();

                // Create and execute the ApplicationEngine
                using var engine = ApplicationEngine.Create(
                    TriggerType.Application,
                    tx,
                    clonedCache,
                    block,
                    protocolSettings,
                    tx.SystemFee);

                engine.LoadScript(tx.Script);
                var state = await Task.Run(() => engine.Execute(), cancellationToken);

                var notifications = engine.Notifications
                    .Select(n => (object)n)
                    .ToList();

                if (state == Neo.VM.VMState.HALT)
                {
                    // Commit changes on success
                    clonedCache.Commit();

                    return ExecutionResult.Success(
                        tx.Hash.GetSpan().ToArray(),
                        engine.FeeConsumed,
                        notifications);
                }
                else
                {
                    return ExecutionResult.Failure(
                        tx.Hash.GetSpan().ToArray(),
                        engine.FaultException?.Message ?? "Execution faulted",
                        engine.FeeConsumed);
                }
            }
            catch (Exception ex)
            {
                return ExecutionResult.Failure(
                    tx.Hash.GetSpan().ToArray(),
                    ex.Message);
            }
        }

        private static IExecutionResult CreateFallbackResult(ITransactionData tx)
        {
            return ExecutionResult.Failure(
                tx.Hash?.GetSpan().ToArray() ?? Array.Empty<byte>(),
                "Transaction was not executed (missing from results)");
        }
    }
}
