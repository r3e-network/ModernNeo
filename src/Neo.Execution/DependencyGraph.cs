// Copyright (C) 2015-2025 The Neo Project.
//
// DependencyGraph.cs file belongs to the neo project and is free
// software distributed under the MIT software license.

using Neo.Core.Interfaces;

namespace Neo.Execution;

/// <summary>
/// Represents a graph of transaction dependencies for parallel execution scheduling.
/// </summary>
public class DependencyGraph : IDependencyGraph
{
    private readonly List<ITransactionData> _transactions;
    private readonly Dictionary<int, HashSet<int>> _dependencies; // tx -> depends on these
    private readonly Dictionary<int, HashSet<int>> _dependents;   // tx -> these depend on it
    private readonly Dictionary<int, DependencyType> _dependencyTypes;

    public DependencyGraph(IEnumerable<ITransactionData> transactions)
    {
        _transactions = transactions.ToList();
        _dependencies = new Dictionary<int, HashSet<int>>();
        _dependents = new Dictionary<int, HashSet<int>>();
        _dependencyTypes = new Dictionary<int, DependencyType>();

        for (int i = 0; i < _transactions.Count; i++)
        {
            _dependencies[i] = new HashSet<int>();
            _dependents[i] = new HashSet<int>();
        }
    }

    /// <inheritdoc/>
    public int TransactionCount => _transactions.Count;

    /// <inheritdoc/>
    public int DependencyCount => _dependencies.Values.Sum(d => d.Count);

    /// <inheritdoc/>
    public IReadOnlyList<ITransactionData> Transactions => _transactions;

    /// <summary>
    /// Adds a dependency edge: fromIndex depends on toIndex.
    /// </summary>
    public void AddDependency(int fromIndex, int toIndex, DependencyType type = DependencyType.None)
    {
        if (fromIndex == toIndex) return;
        if (fromIndex < 0 || fromIndex >= _transactions.Count) return;
        if (toIndex < 0 || toIndex >= _transactions.Count) return;

        _dependencies[fromIndex].Add(toIndex);
        _dependents[toIndex].Add(fromIndex);

        var key = GetEdgeKey(fromIndex, toIndex);
        if (_dependencyTypes.TryGetValue(key, out var existing))
        {
            _dependencyTypes[key] = existing | type;
        }
        else
        {
            _dependencyTypes[key] = type;
        }
    }

    /// <summary>
    /// Adds a weak dependency (potential conflict, may allow optimistic execution).
    /// </summary>
    public void AddWeakDependency(int fromIndex, int toIndex)
    {
        AddDependency(fromIndex, toIndex, DependencyType.Weak);
    }

    /// <summary>
    /// Gets the dependency type between two transactions.
    /// </summary>
    public DependencyType GetDependencyType(int fromIndex, int toIndex)
    {
        var key = GetEdgeKey(fromIndex, toIndex);
        return _dependencyTypes.TryGetValue(key, out var type) ? type : DependencyType.None;
    }

    /// <inheritdoc/>
    public IReadOnlyList<int> GetDependencies(int transactionIndex)
    {
        return _dependencies.TryGetValue(transactionIndex, out var deps)
            ? deps.ToList()
            : [];
    }

    /// <summary>
    /// Gets transactions that depend on this one.
    /// </summary>
    public IReadOnlyList<int> GetDependents(int transactionIndex)
    {
        return _dependents.TryGetValue(transactionIndex, out var deps)
            ? deps.ToList()
            : [];
    }

    /// <inheritdoc/>
    public int GetInDegree(int transactionIndex, ISet<int>? remainingIndices = null)
    {
        if (!_dependencies.TryGetValue(transactionIndex, out var deps))
            return 0;

        if (remainingIndices == null)
            return deps.Count;

        return deps.Count(d => remainingIndices.Contains(d));
    }

    /// <inheritdoc/>
    public IReadOnlyList<int> GetIndependentTransactions(ISet<int>? remainingIndices = null)
    {
        var result = new List<int>();
        var indices = remainingIndices ?? Enumerable.Range(0, _transactions.Count).ToHashSet();

        foreach (var idx in indices)
        {
            if (GetInDegree(idx, remainingIndices) == 0)
            {
                result.Add(idx);
            }
        }

        return result;
    }

    /// <inheritdoc/>
    public bool HasCycle()
    {
        var visited = new HashSet<int>();
        var recursionStack = new HashSet<int>();

        for (int i = 0; i < _transactions.Count; i++)
        {
            if (HasCycleDfs(i, visited, recursionStack))
                return true;
        }

        return false;
    }

    private bool HasCycleDfs(int node, HashSet<int> visited, HashSet<int> recursionStack)
    {
        if (recursionStack.Contains(node))
            return true;

        if (visited.Contains(node))
            return false;

        visited.Add(node);
        recursionStack.Add(node);

        if (_dependencies.TryGetValue(node, out var deps))
        {
            foreach (var dep in deps)
            {
                if (HasCycleDfs(dep, visited, recursionStack))
                    return true;
            }
        }

        recursionStack.Remove(node);
        return false;
    }

    private static int GetEdgeKey(int from, int to) => from * 10000 + to;
}
