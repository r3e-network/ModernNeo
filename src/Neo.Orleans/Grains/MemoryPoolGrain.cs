using Neo.Core.Interfaces;
using Neo.Extensions;
using Neo.Orleans.Interfaces;
using Neo.Orleans.States;
using Orleans.Runtime;

namespace Neo.Orleans.Grains;

/// <summary>
/// Orleans Grain implementation for transaction memory pool management.
/// Replaces Akka.NET MemoryPool component with Verified/Unverified separation.
/// </summary>
public class MemoryPoolGrain : Grain, IMemoryPoolGrain
{
    private readonly IPersistentState<MemoryPoolState> _state;
    private SortedSet<PoolItemState>? _sortedVerified;
    private SortedSet<PoolItemState>? _sortedUnverified;

    public MemoryPoolGrain(
        [PersistentState("memorypool", "MemoryPoolStore")]
        IPersistentState<MemoryPoolState> state)
    {
        _state = state;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        // Initialize sorted sets from persisted state
        _sortedVerified = new SortedSet<PoolItemState>(_state.State.VerifiedTransactions.Values);
        _sortedUnverified = new SortedSet<PoolItemState>(_state.State.UnverifiedTransactions.Values);
        return base.OnActivateAsync(cancellationToken);
    }

    public async Task<MemoryPoolAddResult> AddTransactionAsync(ITransactionData transaction)
    {
        var hashHex = Convert.ToHexString(transaction.Hash.GetSpan());

        // Check if already exists in verified pool
        if (_state.State.VerifiedTransactions.ContainsKey(hashHex))
            return MemoryPoolAddResult.AlreadyInPool;

        // Check if already exists in unverified pool
        if (_state.State.UnverifiedTransactions.ContainsKey(hashHex))
            return MemoryPoolAddResult.AlreadyInPool;

        // Check for conflicts
        if (_state.State.Conflicts.ContainsKey(hashHex))
            return MemoryPoolAddResult.HasConflicts;

        // Check expiration
        if (transaction.ValidUntilBlock <= _state.State.CurrentBlockHeight)
            return MemoryPoolAddResult.Expired;

        // Check capacity and evict if needed
        var totalCount = _state.State.VerifiedTransactions.Count + _state.State.UnverifiedTransactions.Count;
        if (totalCount >= _state.State.Capacity)
        {
            if (!TryEvictLowestPriority(transaction.FeePerByte))
                return MemoryPoolAddResult.OutOfMemory;
        }

        // Create pool item
        var item = new PoolItemState
        {
            HashHex = hashHex,
            Hash = transaction.Hash.GetSpan().ToArray(),
            FeePerByte = transaction.FeePerByte,
            NetworkFee = transaction.NetworkFee,
            SystemFee = transaction.SystemFee,
            ValidUntilBlock = transaction.ValidUntilBlock,
            Timestamp = DateTime.UtcNow,
            SerializedData = transaction.ToArray()
        };

        // Add to verified collections
        _state.State.VerifiedTransactions[hashHex] = item;
        _sortedVerified!.Add(item);

        await _state.WriteStateAsync();
        return MemoryPoolAddResult.Succeed;
    }

    public Task<IReadOnlyList<PoolItemState>> GetVerifiedTransactionsAsync(int maxCount)
    {
        var result = _sortedVerified!.Take(maxCount).ToList();
        return Task.FromResult<IReadOnlyList<PoolItemState>>(result);
    }

    public Task<IReadOnlyList<PoolItemState>> GetUnverifiedTransactionsAsync(int maxCount)
    {
        var result = _sortedUnverified!.Take(maxCount).ToList();
        return Task.FromResult<IReadOnlyList<PoolItemState>>(result);
    }

    public async Task<bool> RemoveTransactionAsync(byte[] hash)
    {
        var hashHex = Convert.ToHexString(hash);

        // Try verified first
        if (_state.State.VerifiedTransactions.TryGetValue(hashHex, out var verifiedItem))
        {
            _state.State.VerifiedTransactions.Remove(hashHex);
            _sortedVerified!.Remove(verifiedItem);
            RemoveConflictsForTransaction(hashHex);
            await _state.WriteStateAsync();
            return true;
        }

        // Try unverified
        if (_state.State.UnverifiedTransactions.TryGetValue(hashHex, out var unverifiedItem))
        {
            _state.State.UnverifiedTransactions.Remove(hashHex);
            _sortedUnverified!.Remove(unverifiedItem);
            await _state.WriteStateAsync();
            return true;
        }

        return false;
    }

    public async Task<bool> TryRemoveUnverifiedAsync(byte[] hash)
    {
        var hashHex = Convert.ToHexString(hash);

        if (!_state.State.UnverifiedTransactions.TryGetValue(hashHex, out var item))
            return false;

        _state.State.UnverifiedTransactions.Remove(hashHex);
        _sortedUnverified!.Remove(item);
        await _state.WriteStateAsync();
        return true;
    }

    public Task<int> GetCountAsync() =>
        Task.FromResult(_state.State.VerifiedTransactions.Count + _state.State.UnverifiedTransactions.Count);

    public Task<int> GetVerifiedCountAsync() =>
        Task.FromResult(_state.State.VerifiedTransactions.Count);

    public Task<int> GetUnverifiedCountAsync() =>
        Task.FromResult(_state.State.UnverifiedTransactions.Count);

    public Task<bool> ContainsAsync(byte[] hash)
    {
        var hashHex = Convert.ToHexString(hash);
        return Task.FromResult(
            _state.State.VerifiedTransactions.ContainsKey(hashHex) ||
            _state.State.UnverifiedTransactions.ContainsKey(hashHex));
    }

    public Task<bool> ContainsConflictAsync(byte[] hash)
    {
        var hashHex = Convert.ToHexString(hash);
        return Task.FromResult(_state.State.Conflicts.ContainsKey(hashHex));
    }

    public async Task ClearAsync()
    {
        _state.State.VerifiedTransactions.Clear();
        _state.State.UnverifiedTransactions.Clear();
        _state.State.Conflicts.Clear();
        _state.State.LastBroadcastBlock.Clear();
        _sortedVerified!.Clear();
        _sortedUnverified!.Clear();
        await _state.WriteStateAsync();
    }

    public async Task InvalidateAllTransactionsAsync()
    {
        // Move all verified to unverified
        foreach (var kvp in _state.State.VerifiedTransactions)
        {
            _state.State.UnverifiedTransactions[kvp.Key] = kvp.Value;
            _sortedUnverified!.Add(kvp.Value);
        }

        _state.State.VerifiedTransactions.Clear();
        _sortedVerified!.Clear();
        _state.State.Conflicts.Clear();

        await _state.WriteStateAsync();
    }

    public async Task UpdateForBlockPersistedAsync(uint blockIndex, IEnumerable<byte[]> includedTxHashes)
    {
        _state.State.CurrentBlockHeight = blockIndex;

        // Remove included transactions
        foreach (var hash in includedTxHashes)
        {
            var hashHex = Convert.ToHexString(hash);

            if (_state.State.VerifiedTransactions.TryGetValue(hashHex, out var verifiedItem))
            {
                _state.State.VerifiedTransactions.Remove(hashHex);
                _sortedVerified!.Remove(verifiedItem);
                RemoveConflictsForTransaction(hashHex);
            }

            if (_state.State.UnverifiedTransactions.TryGetValue(hashHex, out var unverifiedItem))
            {
                _state.State.UnverifiedTransactions.Remove(hashHex);
                _sortedUnverified!.Remove(unverifiedItem);
            }

            _state.State.LastBroadcastBlock.Remove(hashHex);
        }

        // Move remaining verified to unverified for re-verification
        foreach (var kvp in _state.State.VerifiedTransactions.ToList())
        {
            _state.State.UnverifiedTransactions[kvp.Key] = kvp.Value;
            _sortedUnverified!.Add(kvp.Value);
        }

        _state.State.VerifiedTransactions.Clear();
        _sortedVerified!.Clear();
        _state.State.Conflicts.Clear();

        // Remove expired transactions
        await RemoveExpiredTransactionsAsync(blockIndex);

        await _state.WriteStateAsync();
    }

    public async Task<bool> ReVerifyTopUnverifiedAsync(int maxCount)
    {
        if (_sortedUnverified!.Count == 0)
            return false;

        var toReVerify = _sortedUnverified.Take(maxCount).ToList();
        var reVerified = 0;

        foreach (var item in toReVerify)
        {
            // Check if still valid (not expired)
            if (item.ValidUntilBlock <= _state.State.CurrentBlockHeight)
            {
                _state.State.UnverifiedTransactions.Remove(item.HashHex);
                _sortedUnverified.Remove(item);
                continue;
            }

            // Move to verified
            _state.State.UnverifiedTransactions.Remove(item.HashHex);
            _sortedUnverified.Remove(item);

            _state.State.VerifiedTransactions[item.HashHex] = item;
            _sortedVerified!.Add(item);
            reVerified++;
        }

        if (reVerified > 0)
            await _state.WriteStateAsync();

        return _sortedUnverified.Count > 0;
    }

    public async Task<int> RemoveExpiredTransactionsAsync(uint currentBlockHeight)
    {
        _state.State.CurrentBlockHeight = currentBlockHeight;
        var removedCount = 0;

        // Remove expired from verified
        var expiredVerified = _state.State.VerifiedTransactions
            .Where(kvp => kvp.Value.ValidUntilBlock <= currentBlockHeight)
            .ToList();

        foreach (var kvp in expiredVerified)
        {
            _state.State.VerifiedTransactions.Remove(kvp.Key);
            _sortedVerified!.Remove(kvp.Value);
            RemoveConflictsForTransaction(kvp.Key);
            removedCount++;
        }

        // Remove expired from unverified
        var expiredUnverified = _state.State.UnverifiedTransactions
            .Where(kvp => kvp.Value.ValidUntilBlock <= currentBlockHeight)
            .ToList();

        foreach (var kvp in expiredUnverified)
        {
            _state.State.UnverifiedTransactions.Remove(kvp.Key);
            _sortedUnverified!.Remove(kvp.Value);
            removedCount++;
        }

        if (removedCount > 0)
            await _state.WriteStateAsync();

        return removedCount;
    }

    #region Private Methods

    /// <summary>
    /// Tries to evict the lowest priority transaction if new transaction has higher priority.
    /// </summary>
    private bool TryEvictLowestPriority(long newTxFeePerByte)
    {
        // Try to evict from verified first
        if (_sortedVerified!.Count > 0)
        {
            var lowestVerified = _sortedVerified.Max; // Lowest priority is at Max due to descending sort
            if (lowestVerified != null && newTxFeePerByte > lowestVerified.FeePerByte)
            {
                _state.State.VerifiedTransactions.Remove(lowestVerified.HashHex);
                _sortedVerified.Remove(lowestVerified);
                RemoveConflictsForTransaction(lowestVerified.HashHex);
                return true;
            }
        }

        // Try to evict from unverified
        if (_sortedUnverified!.Count > 0)
        {
            var lowestUnverified = _sortedUnverified.Max;
            if (lowestUnverified != null && newTxFeePerByte > lowestUnverified.FeePerByte)
            {
                _state.State.UnverifiedTransactions.Remove(lowestUnverified.HashHex);
                _sortedUnverified.Remove(lowestUnverified);
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Removes conflict entries for a transaction being removed.
    /// </summary>
    private void RemoveConflictsForTransaction(string txHashHex)
    {
        // Find and remove all conflict entries that reference this transaction
        var conflictsToRemove = _state.State.Conflicts
            .Where(kvp => kvp.Value.Contains(txHashHex))
            .ToList();

        foreach (var kvp in conflictsToRemove)
        {
            kvp.Value.Remove(txHashHex);
            if (kvp.Value.Count == 0)
            {
                _state.State.Conflicts.Remove(kvp.Key);
            }
        }
    }

    #endregion
}
