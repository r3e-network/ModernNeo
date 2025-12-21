// Copyright (C) 2015-2025 The Neo Project.
//
// BlockExecutorAdapter.cs file belongs to the neo project and is free
// software distributed under the MIT software license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Neo.Core.Interfaces;
using Neo.Execution;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.SmartContract;
using Neo.SmartContract.Native;
using Neo.VM;

namespace Neo.Ledger;

/// <summary>
/// Adapts the ParallelExecutor for use with Blockchain.Persist.
/// Provides both sequential and parallel execution modes with automatic fallback.
/// </summary>
public class BlockExecutorAdapter : IBlockExecutor
{
    private readonly ParallelExecutor? _parallelExecutor;
    private readonly DependencyAnalyzer _analyzer;
    private IExecutionStatistics? _lastStatistics;

    /// <summary>
    /// Minimum transaction count to enable parallel execution.
    /// Below this threshold, sequential execution is more efficient.
    /// </summary>
    public int ParallelThreshold { get; set; } = 3;

    /// <summary>
    /// Whether to force sequential execution regardless of transaction count.
    /// </summary>
    public bool ForceSequential { get; set; } = false;

    /// <inheritdoc/>
    public bool IsParallelEnabled => !ForceSequential && _parallelExecutor != null;

    /// <inheritdoc/>
    public IExecutionStatistics? LastStatistics => _lastStatistics;

    /// <summary>
    /// Creates a new block executor adapter.
    /// </summary>
    /// <param name="enableParallel">Whether to enable parallel execution.</param>
    public BlockExecutorAdapter(bool enableParallel = true)
    {
        _analyzer = new DependencyAnalyzer { IncludeHeuristicDependencies = true };

        if (enableParallel)
        {
            _parallelExecutor = new ParallelExecutor(_analyzer, new ExecutionScheduler());
        }
    }

    /// <inheritdoc/>
    public IReadOnlyList<IBlockExecutionResult> Execute(
        IReadOnlyList<object> transactions,
        object snapshot,
        IBlockData block,
        object settings,
        Action<object>? onExecuted = null)
    {
        if (transactions.Count == 0)
            return Array.Empty<IBlockExecutionResult>();

        // Cast to concrete types
        var txStates = transactions.Cast<TransactionState>().ToList();
        var storeCache = snapshot as StoreCache
            ?? throw new ArgumentException("Snapshot must be StoreCache", nameof(snapshot));
        var neoBlock = block as Block
            ?? throw new ArgumentException("Block must be Neo.Network.P2P.Payloads.Block", nameof(block));
        var protocolSettings = settings as ProtocolSettings
            ?? throw new ArgumentException("Settings must be ProtocolSettings", nameof(settings));

        // Determine execution mode
        bool useParallel = ShouldUseParallel(txStates);

        if (useParallel)
        {
            return ExecuteParallel(txStates, storeCache, neoBlock, protocolSettings, onExecuted);
        }
        else
        {
            return ExecuteSequential(txStates, storeCache, neoBlock, protocolSettings, onExecuted);
        }
    }

    private bool ShouldUseParallel(List<TransactionState> transactions)
    {
        if (ForceSequential || _parallelExecutor == null)
            return false;

        if (transactions.Count < ParallelThreshold)
            return false;

        // Analyze dependencies to see if parallel execution is beneficial
        var txData = transactions.Select(ts => ts.Transaction as ITransactionData).Where(t => t != null).ToList();
        var graph = _analyzer.Analyze(txData!);

        // If too many dependencies, sequential may be better
        var independentCount = graph.GetIndependentTransactions().Count;
        return independentCount >= 2;
    }

    private IReadOnlyList<IBlockExecutionResult> ExecuteSequential(
        List<TransactionState> txStates,
        StoreCache snapshot,
        Block block,
        ProtocolSettings settings,
        Action<object>? onExecuted)
    {
        var results = new List<IBlockExecutionResult>();
        var clonedSnapshot = snapshot.CloneCache();

        foreach (var transactionState in txStates)
        {
            var tx = transactionState.Transaction!;

            using var engine = ApplicationEngine.Create(
                TriggerType.Application,
                tx,
                clonedSnapshot,
                block,
                settings,
                tx.SystemFee);

            engine.LoadScript(tx.Script);
            transactionState.State = engine.Execute();

            var applicationExecuted = new Blockchain.ApplicationExecuted(engine);
            onExecuted?.Invoke(applicationExecuted);

            if (transactionState.State == VMState.HALT)
            {
                clonedSnapshot.Commit();
                results.Add(BlockExecutionResult.Success(
                    tx.Hash.GetSpan().ToArray(),
                    applicationExecuted));
            }
            else
            {
                clonedSnapshot = snapshot.CloneCache();
                results.Add(BlockExecutionResult.Failure(
                    tx.Hash.GetSpan().ToArray(),
                    applicationExecuted));
            }
        }

        _lastStatistics = new ExecutionStatistics
        {
            TotalTransactions = txStates.Count,
            BatchCount = txStates.Count, // Each tx is its own "batch"
            SuccessfulTransactions = results.Count(r => r.ShouldCommit),
            FailedTransactions = results.Count(r => !r.ShouldCommit)
        };

        return results;
    }

    private IReadOnlyList<IBlockExecutionResult> ExecuteParallel(
        List<TransactionState> txStates,
        StoreCache snapshot,
        Block block,
        ProtocolSettings settings,
        Action<object>? onExecuted)
    {
        var txList = txStates.Select(ts => ts.Transaction as ITransactionData).Where(t => t != null).ToList();

        // Build dependency graph
        var graph = _analyzer.Analyze(txList!) as DependencyGraph;
        var scheduler = new ExecutionScheduler { Strategy = SchedulingStrategy.Conservative };
        var batches = scheduler.Schedule(graph!).ToList();

        var results = new List<IBlockExecutionResult>();
        var clonedSnapshot = snapshot.CloneCache();
        var txStateMap = txStates.ToDictionary(ts => ts.Transaction!.Hash);
        var executionSnapshots = new DataCache?[txList.Count];
        var executionStates = new VMState?[txList.Count];
        var executionResults = new Blockchain.ApplicationExecuted?[txList.Count];

        var stats = new ExecutionStatistics
        {
            TotalTransactions = txStates.Count,
            BatchCount = batches.Count
        };

        foreach (var batch in batches)
        {
            // Execute batch in parallel
            Parallel.ForEach(batch.TransactionIndices, txIndex =>
            {
                var tx = txList[txIndex] as Transaction;
                if (tx == null)
                    return;

                var transactionState = txStateMap[tx.Hash];

                // Each parallel execution gets its own cloned cache
                var execSnapshot = clonedSnapshot.CloneCache();

                using var engine = ApplicationEngine.Create(
                    TriggerType.Application,
                    tx,
                    execSnapshot,
                    block,
                    settings,
                    tx.SystemFee);

                engine.LoadScript(tx.Script);
                var vmState = engine.Execute();

                var applicationExecuted = new Blockchain.ApplicationExecuted(engine);

                if (vmState == VMState.HALT)
                    stats.IncrementSuccessful();
                else
                    stats.IncrementFailed();

                transactionState.State = vmState;
                executionSnapshots[txIndex] = execSnapshot;
                executionStates[txIndex] = vmState;
                executionResults[txIndex] = applicationExecuted;
            });

            // Commit successful transactions in order
            foreach (var index in batch.TransactionIndices.OrderBy(i => i))
            {
                var tx = txList[index] as Transaction;
                var applicationExecuted = executionResults[index];
                var vmState = executionStates[index] ?? VMState.FAULT;

                if (tx == null || applicationExecuted == null || executionSnapshots[index] == null)
                    throw new InvalidOperationException($"Missing execution result for transaction index {index}.");

                onExecuted?.Invoke(applicationExecuted);

                if (vmState == VMState.HALT)
                {
                    executionSnapshots[index]!.Commit();
                    results.Add(BlockExecutionResult.Success(
                        tx.Hash.GetSpan().ToArray(),
                        applicationExecuted));
                }
                else
                {
                    results.Add(BlockExecutionResult.Failure(
                        tx.Hash.GetSpan().ToArray(),
                        applicationExecuted));
                }

                executionSnapshots[index] = null;
                executionResults[index] = null;
                executionStates[index] = null;
            }

            stats.UpdatePeakParallelism(batch.TransactionIndices.Count);
        }

        _lastStatistics = stats;
        return results;
    }
}
