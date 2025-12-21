// Copyright (C) 2015-2025 The Neo Project.
//
// ExecutionScheduler.cs file belongs to the neo project and is free
// software distributed under the MIT software license.

using Neo.Core.Interfaces;

namespace Neo.Execution;

/// <summary>
/// Represents a batch of transactions to execute in parallel.
/// </summary>
public class ExecutionBatch : IExecutionBatch
{
    public int BatchNumber { get; }
    public IReadOnlyList<int> TransactionIndices { get; }
    public bool SupportsOptimisticExecution { get; }

    public ExecutionBatch(int batchNumber, IReadOnlyList<int> indices, bool supportsOptimistic = false)
    {
        BatchNumber = batchNumber;
        TransactionIndices = indices;
        SupportsOptimisticExecution = supportsOptimistic;
    }
}

/// <summary>
/// Schedules transactions for execution based on their dependencies.
/// Uses topological sorting to determine execution order.
/// </summary>
public class ExecutionScheduler : IExecutionScheduler
{
    /// <inheritdoc/>
    public SchedulingStrategy Strategy { get; set; } = SchedulingStrategy.Conservative;

    /// <summary>
    /// Maximum batch size for parallel execution.
    /// </summary>
    public int MaxBatchSize { get; set; } = 64;

    /// <inheritdoc/>
    public IEnumerable<IExecutionBatch> Schedule(IDependencyGraph graph)
    {
        if (graph.TransactionCount == 0)
            yield break;

        // Check for cycles
        if (graph.HasCycle())
            throw new InvalidOperationException("Dependency graph contains a cycle - cannot schedule execution");

        var batches = new List<IExecutionBatch>();
        var remaining = new HashSet<int>(Enumerable.Range(0, graph.TransactionCount));
        int batchNumber = 0;

        while (remaining.Count > 0)
        {
            // Find all transactions with no remaining dependencies
            var independentTx = graph.GetIndependentTransactions(remaining);

            if (independentTx.Count == 0)
            {
                // This shouldn't happen if HasCycle() returned false
                throw new InvalidOperationException("No independent transactions found but cycle not detected");
            }

            // Determine if this batch supports optimistic execution
            bool supportsOptimistic = Strategy != SchedulingStrategy.Conservative &&
                                      HasOnlyWeakDependencies(graph, independentTx);

            // Split large batches if needed
            foreach (var chunk in ChunkBatch(independentTx.ToList(), MaxBatchSize))
            {
                yield return new ExecutionBatch(batchNumber++, chunk, supportsOptimistic);
            }

            // Remove processed transactions
            foreach (var idx in independentTx)
            {
                remaining.Remove(idx);
            }
        }
    }

    /// <summary>
    /// Schedules with specific ordering constraints for testing.
    /// </summary>
    public IEnumerable<IExecutionBatch> ScheduleWithOrdering(
        IDependencyGraph graph,
        Func<IReadOnlyList<int>, IReadOnlyList<int>> orderingFunction)
    {
        if (graph.TransactionCount == 0)
            yield break;

        var remaining = new HashSet<int>(Enumerable.Range(0, graph.TransactionCount));
        int batchNumber = 0;

        while (remaining.Count > 0)
        {
            var independentTx = graph.GetIndependentTransactions(remaining);

            if (independentTx.Count == 0)
                throw new InvalidOperationException("No independent transactions found");

            var orderedTx = orderingFunction(independentTx);
            bool supportsOptimistic = Strategy != SchedulingStrategy.Conservative;

            foreach (var chunk in ChunkBatch(orderedTx.ToList(), MaxBatchSize))
            {
                yield return new ExecutionBatch(batchNumber++, chunk, supportsOptimistic);
            }

            foreach (var idx in independentTx)
            {
                remaining.Remove(idx);
            }
        }
    }

    private static bool HasOnlyWeakDependencies(IDependencyGraph graph, IReadOnlyList<int> indices)
    {
        if (graph is not DependencyGraph concreteGraph)
            return false;

        foreach (var idx in indices)
        {
            var deps = graph.GetDependencies(idx);
            foreach (var dep in deps)
            {
                var type = concreteGraph.GetDependencyType(idx, dep);
                if ((type & ~DependencyType.Weak) != DependencyType.None)
                {
                    // Has a strong dependency
                    return false;
                }
            }
        }

        return true;
    }

    private static IEnumerable<IReadOnlyList<int>> ChunkBatch(List<int> indices, int maxSize)
    {
        for (int i = 0; i < indices.Count; i += maxSize)
        {
            var count = Math.Min(maxSize, indices.Count - i);
            yield return indices.GetRange(i, count);
        }
    }
}
