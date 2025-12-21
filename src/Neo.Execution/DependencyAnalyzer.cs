// Copyright (C) 2015-2025 The Neo Project.
//
// DependencyAnalyzer.cs file belongs to the neo project and is free
// software distributed under the MIT software license.

using Neo.Core.Interfaces;
using Neo.Network.P2P.Payloads;
using System.Runtime.CompilerServices;

namespace Neo.Execution;

/// <summary>
/// Analyzes read/write sets of transactions to build a dependency graph.
/// Identifies explicit conflicts (sender, Conflicts attribute, Oracle) and
/// heuristic conflicts (same contract access).
/// </summary>
public class DependencyAnalyzer : IDependencyAnalyzer
{
    /// <summary>
    /// Whether to include heuristic (weak) dependencies based on contract access.
    /// </summary>
    public bool IncludeHeuristicDependencies { get; set; } = true;

    /// <inheritdoc/>
    public IDependencyGraph Analyze(IEnumerable<ITransactionData> transactions)
    {
        var txList = transactions.ToList();
        var graph = new DependencyGraph(txList);

        // Build lookup structures for O(1) conflict detection
        var senderGroups = BuildSenderGroups(txList);
        var conflictHashes = BuildConflictHashSet(txList);
        var oracleResponses = BuildOracleResponseSet(txList);
        var contractAccess = IncludeHeuristicDependencies ? BuildContractAccessSet(txList) : null;

        // Analyze pairwise dependencies
        for (int i = 0; i < txList.Count; i++)
        {
            for (int j = i + 1; j < txList.Count; j++)
            {
                var depType = CheckDependencyInternal(
                    txList[i], txList[j], i, j,
                    senderGroups, conflictHashes, oracleResponses, contractAccess);

                if (depType != DependencyType.None)
                {
                    // Later transaction (j) depends on earlier one (i)
                    graph.AddDependency(j, i, depType);
                }
            }
        }

        return graph;
    }

    /// <inheritdoc/>
    public DependencyType CheckDependency(ITransactionData tx1, ITransactionData tx2)
    {
        var depType = DependencyType.None;

        // 1. Same sender
        if (tx1.Sender == tx2.Sender)
        {
            depType |= DependencyType.SameSender;
        }

        // 2. Explicit conflicts (requires casting to full Transaction type)
        if (tx1 is Transaction fullTx1 && tx2 is Transaction fullTx2)
        {
            if (HasExplicitConflict(fullTx1, fullTx2))
            {
                depType |= DependencyType.ExplicitConflict;
            }

            if (HasOracleConflict(fullTx1, fullTx2))
            {
                depType |= DependencyType.OracleResponseConflict;
            }

            if (IncludeHeuristicDependencies && MayHaveStorageConflict(fullTx1, fullTx2))
            {
                depType |= DependencyType.StorageConflict | DependencyType.Weak;
            }
        }

        return depType;
    }

    private DependencyType CheckDependencyInternal(
        ITransactionData tx1, ITransactionData tx2,
        int idx1, int idx2,
        Dictionary<UInt160, List<int>> senderGroups,
        HashSet<UInt256> conflictHashes,
        Dictionary<ulong, int> oracleResponses,
        Dictionary<UInt160, List<int>>? contractAccess)
    {
        var depType = DependencyType.None;

        // 1. Same sender - O(1) via lookup
        if (tx1.Sender == tx2.Sender)
        {
            depType |= DependencyType.SameSender;
        }

        // 2. Explicit conflicts
        if (tx1 is Transaction fullTx1 && tx2 is Transaction fullTx2)
        {
            // Check if tx1 has Conflicts pointing to tx2 or vice versa
            var hash1 = fullTx1.Hash;
            var hash2 = fullTx2.Hash;

            foreach (var attr in fullTx1.Attributes)
            {
                if (attr is Conflicts conflict && conflict.Hash == hash2)
                {
                    depType |= DependencyType.ExplicitConflict;
                    break;
                }
            }

            foreach (var attr in fullTx2.Attributes)
            {
                if (attr is Conflicts conflict && conflict.Hash == hash1)
                {
                    depType |= DependencyType.ExplicitConflict;
                    break;
                }
            }

            // 3. Oracle response conflict
            var oracle1 = GetOracleResponseId(fullTx1);
            var oracle2 = GetOracleResponseId(fullTx2);
            if (oracle1.HasValue && oracle2.HasValue && oracle1.Value == oracle2.Value)
            {
                depType |= DependencyType.OracleResponseConflict;
            }

            // 4. Heuristic: Same contract access (weak dependency)
            if (contractAccess != null)
            {
                var contracts1 = ExtractContractHashes(fullTx1);
                var contracts2 = ExtractContractHashes(fullTx2);

                if (contracts1.Intersect(contracts2).Any())
                {
                    depType |= DependencyType.StorageConflict | DependencyType.Weak;
                }
            }
        }

        return depType;
    }

    private static Dictionary<UInt160, List<int>> BuildSenderGroups(List<ITransactionData> transactions)
    {
        var groups = new Dictionary<UInt160, List<int>>();

        for (int i = 0; i < transactions.Count; i++)
        {
            var sender = transactions[i].Sender;
            if (!groups.TryGetValue(sender, out var list))
            {
                list = new List<int>();
                groups[sender] = list;
            }
            list.Add(i);
        }

        return groups;
    }

    private static HashSet<UInt256> BuildConflictHashSet(List<ITransactionData> transactions)
    {
        var conflicts = new HashSet<UInt256>();

        foreach (var tx in transactions)
        {
            if (tx is Transaction fullTx)
            {
                foreach (var attr in fullTx.Attributes)
                {
                    if (attr is Conflicts conflict)
                    {
                        conflicts.Add(conflict.Hash);
                    }
                }
            }
        }

        return conflicts;
    }

    private static Dictionary<ulong, int> BuildOracleResponseSet(List<ITransactionData> transactions)
    {
        var responses = new Dictionary<ulong, int>();

        for (int i = 0; i < transactions.Count; i++)
        {
            if (transactions[i] is Transaction fullTx)
            {
                var oracleId = GetOracleResponseId(fullTx);
                if (oracleId.HasValue)
                {
                    responses[oracleId.Value] = i;
                }
            }
        }

        return responses;
    }

    private static Dictionary<UInt160, List<int>>? BuildContractAccessSet(List<ITransactionData> transactions)
    {
        var access = new Dictionary<UInt160, List<int>>();

        for (int i = 0; i < transactions.Count; i++)
        {
            if (transactions[i] is Transaction fullTx)
            {
                var contracts = ExtractContractHashes(fullTx);
                foreach (var contract in contracts)
                {
                    if (!access.TryGetValue(contract, out var list))
                    {
                        list = new List<int>();
                        access[contract] = list;
                    }
                    list.Add(i);
                }
            }
        }

        return access;
    }

    private static bool HasExplicitConflict(Transaction tx1, Transaction tx2)
    {
        foreach (var attr in tx1.Attributes)
        {
            if (attr is Conflicts conflict && conflict.Hash == tx2.Hash)
                return true;
        }

        foreach (var attr in tx2.Attributes)
        {
            if (attr is Conflicts conflict && conflict.Hash == tx1.Hash)
                return true;
        }

        return false;
    }

    private static bool HasOracleConflict(Transaction tx1, Transaction tx2)
    {
        var id1 = GetOracleResponseId(tx1);
        var id2 = GetOracleResponseId(tx2);
        return id1.HasValue && id2.HasValue && id1.Value == id2.Value;
    }

    private static ulong? GetOracleResponseId(Transaction tx)
    {
        foreach (var attr in tx.Attributes)
        {
            if (attr is OracleResponse oracle)
                return oracle.Id;
        }
        return null;
    }

    private static bool MayHaveStorageConflict(Transaction tx1, Transaction tx2)
    {
        var contracts1 = ExtractContractHashes(tx1);
        var contracts2 = ExtractContractHashes(tx2);
        return contracts1.Intersect(contracts2).Any();
    }

    /// <summary>
    /// Extracts contract hashes from transaction script using simple heuristics.
    /// This is a lightweight static analysis - does not execute the script.
    /// </summary>
    private static HashSet<UInt160> ExtractContractHashes(Transaction tx)
    {
        var contracts = new HashSet<UInt160>();
        var script = tx.Script.Span;

        // Simple heuristic: look for 20-byte sequences that could be contract hashes
        // This is a best-effort extraction without full script parsing
        for (int i = 0; i < script.Length - 20; i++)
        {
            // Look for PUSHDATA1 (0x0C) followed by length 20 (0x14)
            if (script[i] == 0x0C && i + 1 < script.Length && script[i + 1] == 0x14)
            {
                if (i + 22 <= script.Length)
                {
                    try
                    {
                        var hash = new UInt160(script.Slice(i + 2, 20));
                        contracts.Add(hash);
                    }
                    catch
                    {
                        // Invalid hash, skip
                    }
                }
            }
        }

        // Also add signers as potential contract interactions
        foreach (var signer in tx.Signers)
        {
            contracts.Add(signer.Account);
        }

        return contracts;
    }
}
