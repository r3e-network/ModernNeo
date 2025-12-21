// Copyright (C) 2015-2025 The Neo Project.
//
// PriorityTransactionPool.cs file belongs to the neo project and is free
// software distributed under the MIT software license.

using Neo.Core.Interfaces;
using System.Collections.Concurrent;

namespace Neo.TxPool;

/// <summary>
/// Advanced transaction pool with priority queues, nonce tracking, and conflict detection.
/// Implements the NeoAN smart memory pool design.
/// </summary>
public sealed class PriorityTransactionPool : ITransactionPool, IDisposable
{
    private readonly ConcurrentDictionary<string, PoolEntry> _allTransactions = new();
    private readonly PriorityQueue<string, long> _highPriorityQueue = new();
    private readonly PriorityQueue<string, long> _normalPriorityQueue = new();
    private readonly PriorityQueue<string, long> _lowPriorityQueue = new();
    private readonly ConcurrentDictionary<string, HashSet<string>> _senderTransactions = new();
    private readonly ConcurrentDictionary<string, uint> _senderNonces = new();
    private readonly ReaderWriterLockSlim _queueLock = new();
    private readonly PriorityPoolOptions _options;
    private readonly IConflictDetector? _conflictDetector;
    private bool _disposed;

    /// <inheritdoc/>
    public event EventHandler<ITransactionData>? TransactionAdded;

    /// <inheritdoc/>
    public event EventHandler<TransactionRemovedEventArgs>? TransactionRemoved;

    /// <summary>
    /// Event raised when a conflict is detected.
    /// </summary>
    public event EventHandler<ConflictDetectedEventArgs>? ConflictDetected;

    /// <inheritdoc/>
    public int Capacity => _options.MaxPoolSize;

    /// <inheritdoc/>
    public int Count => _allTransactions.Count;

    /// <inheritdoc/>
    public int VerifiedCount => _allTransactions.Count(x => x.Value.IsVerified);

    /// <inheritdoc/>
    public int UnverifiedCount => _allTransactions.Count(x => !x.Value.IsVerified);

    /// <summary>
    /// Gets the number of high priority transactions.
    /// </summary>
    public int HighPriorityCount
    {
        get
        {
            _queueLock.EnterReadLock();
            try { return _highPriorityQueue.Count; }
            finally { _queueLock.ExitReadLock(); }
        }
    }

    /// <summary>
    /// Gets the number of normal priority transactions.
    /// </summary>
    public int NormalPriorityCount
    {
        get
        {
            _queueLock.EnterReadLock();
            try { return _normalPriorityQueue.Count; }
            finally { _queueLock.ExitReadLock(); }
        }
    }

    /// <summary>
    /// Gets the number of low priority transactions.
    /// </summary>
    public int LowPriorityCount
    {
        get
        {
            _queueLock.EnterReadLock();
            try { return _lowPriorityQueue.Count; }
            finally { _queueLock.ExitReadLock(); }
        }
    }

    /// <summary>
    /// Creates a new priority transaction pool.
    /// </summary>
    public PriorityTransactionPool(PriorityPoolOptions? options = null, IConflictDetector? conflictDetector = null)
    {
        _options = options ?? new PriorityPoolOptions();
        _conflictDetector = conflictDetector;
    }

    /// <inheritdoc/>
    public bool Contains(byte[] hash)
    {
        ArgumentNullException.ThrowIfNull(hash);
        return _allTransactions.ContainsKey(GetHashKey(hash));
    }

    /// <inheritdoc/>
    public bool TryGet(byte[] hash, out ITransactionData? transaction)
    {
        ArgumentNullException.ThrowIfNull(hash);
        if (_allTransactions.TryGetValue(GetHashKey(hash), out var entry))
        {
            transaction = entry.Transaction;
            return true;
        }
        transaction = null;
        return false;
    }

    /// <inheritdoc/>
    public bool TryAdd(ITransactionData transaction)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        ObjectDisposedException.ThrowIf(_disposed, this);

        var hashKey = GetHashKey(transaction.Hash.GetSpan().ToArray());
        var senderKey = GetSenderKey(transaction);

        // Check if already exists
        if (_allTransactions.ContainsKey(hashKey))
            return false;

        // Check for conflicts
        if (_conflictDetector != null)
        {
            var conflicts = _conflictDetector.DetectConflicts(transaction, GetAllTransactions());
            if (conflicts.Any())
            {
                ConflictDetected?.Invoke(this, new ConflictDetectedEventArgs(transaction, conflicts));
                if (!_options.AllowConflictingTransactions)
                    return false;
            }
        }

        // Determine priority
        var priority = DeterminePriority(transaction);
        var entry = new PoolEntry(transaction, priority);

        _queueLock.EnterWriteLock();
        try
        {
            // Check capacity and evict if needed
            while (_allTransactions.Count >= _options.MaxPoolSize)
            {
                if (!EvictLowestPriority())
                    return false; // Cannot evict, pool is full of high priority
            }

            if (!_allTransactions.TryAdd(hashKey, entry))
                return false;

            // Add to appropriate priority queue
            var queuePriority = -entry.FeePerByte; // Negative for max-heap behavior
            switch (priority)
            {
                case TransactionPriority.High:
                    _highPriorityQueue.Enqueue(hashKey, queuePriority);
                    break;
                case TransactionPriority.Normal:
                    _normalPriorityQueue.Enqueue(hashKey, queuePriority);
                    break;
                case TransactionPriority.Low:
                    _lowPriorityQueue.Enqueue(hashKey, queuePriority);
                    break;
            }

            // Track sender transactions
            _senderTransactions.AddOrUpdate(
                senderKey,
                _ => [hashKey],
                (_, set) => { set.Add(hashKey); return set; });

            // Update sender nonce
            UpdateSenderNonce(senderKey, transaction);
        }
        finally
        {
            _queueLock.ExitWriteLock();
        }

        TransactionAdded?.Invoke(this, transaction);
        return true;
    }

    /// <inheritdoc/>
    public bool TryRemove(byte[] hash)
    {
        ArgumentNullException.ThrowIfNull(hash);

        var hashKey = GetHashKey(hash);
        if (!_allTransactions.TryRemove(hashKey, out var entry))
            return false;

        var senderKey = GetSenderKey(entry.Transaction);

        _queueLock.EnterWriteLock();
        try
        {
            // Remove from sender tracking
            if (_senderTransactions.TryGetValue(senderKey, out var senderTxs))
            {
                senderTxs.Remove(hashKey);
                if (senderTxs.Count == 0)
                {
                    _senderTransactions.TryRemove(senderKey, out _);
                    _senderNonces.TryRemove(senderKey, out _);
                }
            }
        }
        finally
        {
            _queueLock.ExitWriteLock();
        }

        TransactionRemoved?.Invoke(this, new TransactionRemovedEventArgs(
            entry.Transaction, TransactionRemovalReason.Explicit));
        return true;
    }

    /// <inheritdoc/>
    public IEnumerable<ITransactionData> GetVerifiedTransactions(int maxCount)
    {
        return GetTransactionsByPriority()
            .Where(e => e.IsVerified)
            .Take(maxCount)
            .Select(e => e.Transaction);
    }

    /// <inheritdoc/>
    public IEnumerable<ITransactionData> GetVerifiedTransactions()
    {
        return GetTransactionsByPriority()
            .Where(e => e.IsVerified)
            .Select(e => e.Transaction);
    }

    /// <summary>
    /// Gets transactions for a specific sender.
    /// </summary>
    public IEnumerable<ITransactionData> GetTransactionsBySender(UInt160 sender)
    {
        var senderKey = sender.ToString();
        if (!_senderTransactions.TryGetValue(senderKey, out var txHashes))
            yield break;

        foreach (var hash in txHashes.ToList())
        {
            if (_allTransactions.TryGetValue(hash, out var entry))
                yield return entry.Transaction;
        }
    }

    /// <summary>
    /// Gets the next expected nonce for a sender.
    /// </summary>
    public uint? GetNextNonce(UInt160 sender)
    {
        var senderKey = sender.ToString();
        return _senderNonces.TryGetValue(senderKey, out var nonce) ? nonce + 1 : null;
    }

    /// <inheritdoc/>
    public void Clear()
    {
        _queueLock.EnterWriteLock();
        try
        {
            var entries = _allTransactions.Values.ToList();
            _allTransactions.Clear();
            _highPriorityQueue.Clear();
            _normalPriorityQueue.Clear();
            _lowPriorityQueue.Clear();
            _senderTransactions.Clear();
            _senderNonces.Clear();

            foreach (var entry in entries)
            {
                TransactionRemoved?.Invoke(this, new TransactionRemovedEventArgs(
                    entry.Transaction, TransactionRemovalReason.Explicit));
            }
        }
        finally
        {
            _queueLock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Gets pool statistics.
    /// </summary>
    public PoolStatistics GetStatistics()
    {
        return new PoolStatistics
        {
            TotalCount = Count,
            VerifiedCount = VerifiedCount,
            UnverifiedCount = UnverifiedCount,
            HighPriorityCount = HighPriorityCount,
            NormalPriorityCount = NormalPriorityCount,
            LowPriorityCount = LowPriorityCount,
            UniqueSenders = _senderTransactions.Count,
            Capacity = Capacity
        };
    }

    private IEnumerable<PoolEntry> GetTransactionsByPriority()
    {
        // Return high priority first, then normal, then low
        // Within each priority, order by fee per byte descending
        return _allTransactions.Values
            .OrderBy(e => e.Priority)
            .ThenByDescending(e => e.FeePerByte)
            .ThenByDescending(e => e.NetworkFee);
    }

    private IEnumerable<ITransactionData> GetAllTransactions()
    {
        return _allTransactions.Values.Select(e => e.Transaction);
    }

    private TransactionPriority DeterminePriority(ITransactionData tx)
    {
        var feePerByte = tx.FeePerByte;

        if (feePerByte >= _options.HighPriorityThreshold)
            return TransactionPriority.High;
        if (feePerByte >= _options.NormalPriorityThreshold)
            return TransactionPriority.Normal;
        return TransactionPriority.Low;
    }

    private bool EvictLowestPriority()
    {
        // Try to evict from low priority first
        if (TryEvictFromQueue(_lowPriorityQueue))
            return true;
        if (TryEvictFromQueue(_normalPriorityQueue))
            return true;
        // Don't evict high priority unless explicitly configured
        if (_options.EvictHighPriority && TryEvictFromQueue(_highPriorityQueue))
            return true;
        return false;
    }

    private bool TryEvictFromQueue(PriorityQueue<string, long> queue)
    {
        while (queue.Count > 0)
        {
            if (!queue.TryDequeue(out var hashKey, out _))
                continue;

            if (_allTransactions.TryRemove(hashKey, out var entry))
            {
                var senderKey = GetSenderKey(entry.Transaction);
                if (_senderTransactions.TryGetValue(senderKey, out var senderTxs))
                {
                    senderTxs.Remove(hashKey);
                    if (senderTxs.Count == 0)
                    {
                        _senderTransactions.TryRemove(senderKey, out _);
                        _senderNonces.TryRemove(senderKey, out _);
                    }
                }

                TransactionRemoved?.Invoke(this, new TransactionRemovedEventArgs(
                    entry.Transaction, TransactionRemovalReason.CapacityExceeded));
                return true;
            }
        }
        return false;
    }

    private void UpdateSenderNonce(string senderKey, ITransactionData tx)
    {
        var nonce = tx.Nonce;
        _senderNonces.AddOrUpdate(
            senderKey,
            _ => nonce,
            (_, current) => Math.Max(current, nonce));
    }

    private static string GetHashKey(byte[] hash) => Convert.ToHexString(hash);

    private static string GetSenderKey(ITransactionData tx)
    {
        return tx.Sender.ToString();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _queueLock.Dispose();
    }

    /// <summary>
    /// Pool entry with metadata.
    /// </summary>
    private sealed class PoolEntry
    {
        public ITransactionData Transaction { get; }
        public TransactionPriority Priority { get; }
        public DateTime Timestamp { get; }
        public bool IsVerified { get; set; } = true;
        public long FeePerByte => Transaction.FeePerByte;
        public long NetworkFee => Transaction.NetworkFee;

        public PoolEntry(ITransactionData transaction, TransactionPriority priority)
        {
            Transaction = transaction;
            Priority = priority;
            Timestamp = DateTime.UtcNow;
        }
    }
}

/// <summary>
/// Transaction priority levels.
/// </summary>
public enum TransactionPriority
{
    High = 0,
    Normal = 1,
    Low = 2
}

/// <summary>
/// Configuration options for priority pool.
/// </summary>
public sealed class PriorityPoolOptions
{
    /// <summary>
    /// Maximum pool size. Default: 50000.
    /// </summary>
    public int MaxPoolSize { get; set; } = 50_000;

    /// <summary>
    /// Fee per byte threshold for high priority. Default: 1000.
    /// </summary>
    public long HighPriorityThreshold { get; set; } = 1000;

    /// <summary>
    /// Fee per byte threshold for normal priority. Default: 100.
    /// </summary>
    public long NormalPriorityThreshold { get; set; } = 100;

    /// <summary>
    /// Whether to allow conflicting transactions. Default: false.
    /// </summary>
    public bool AllowConflictingTransactions { get; set; } = false;

    /// <summary>
    /// Whether to evict high priority transactions when pool is full. Default: false.
    /// </summary>
    public bool EvictHighPriority { get; set; } = false;
}

/// <summary>
/// Pool statistics.
/// </summary>
public readonly struct PoolStatistics
{
    public int TotalCount { get; init; }
    public int VerifiedCount { get; init; }
    public int UnverifiedCount { get; init; }
    public int HighPriorityCount { get; init; }
    public int NormalPriorityCount { get; init; }
    public int LowPriorityCount { get; init; }
    public int UniqueSenders { get; init; }
    public int Capacity { get; init; }
    public double UtilizationPercent => Capacity > 0 ? (double)TotalCount / Capacity * 100 : 0;
}

/// <summary>
/// Event args for conflict detection.
/// </summary>
public sealed class ConflictDetectedEventArgs : EventArgs
{
    public ITransactionData Transaction { get; }
    public IReadOnlyList<ITransactionData> ConflictingTransactions { get; }

    public ConflictDetectedEventArgs(ITransactionData transaction, IEnumerable<ITransactionData> conflicts)
    {
        Transaction = transaction;
        ConflictingTransactions = conflicts.ToList().AsReadOnly();
    }
}
