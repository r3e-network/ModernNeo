// Copyright (C) 2015-2025 The Neo Project.
//
// MemoryPoolGrain.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo;
using Neo.Core;
using Neo.Core.Interfaces;
using Neo.Extensions;
using Neo.IO;
using Neo.Ledger;
using Neo.Network.P2P.Payloads;
using Neo.Orleans.Hosting;
using Neo.Orleans.Interfaces;
using Neo.Orleans.Options;
using Neo.Orleans.States;
using Neo.Orleans.Utilities;
using Neo.SmartContract.Native;
using Orleans.Runtime;

namespace Neo.Orleans.Grains
{
    /// <summary>
    /// Orleans Grain implementation for transaction memory pool management.
    /// Maintains verified/unverified separation.
    /// </summary>
    public class MemoryPoolGrain : Grain, IMemoryPoolGrain
    {
        private readonly IPersistentState<MemoryPoolState> _state;
        private readonly IOrleansOptions _options;
        private readonly INeoSystem _system;
        private SortedSet<PoolItemState>? _sortedVerified;
        private SortedSet<PoolItemState>? _sortedUnverified;
        private bool UseLegacyState => _options.ValidationMode == NeoValidationMode.None;

        public MemoryPoolGrain(
            [PersistentState("memorypool", "MemoryPoolStore")]
            IPersistentState<MemoryPoolState> state,
            INeoSystem system,
            IOrleansOptions? options = null)
        {
            _state = state;
            _system = system;
            _options = options ?? new OrleansOptions();
        }

        public override async Task OnActivateAsync(CancellationToken cancellationToken)
        {
            if (UseLegacyState)
            {
                if (_options.MaxMemoryPoolSize > 0 && _state.State.Capacity != _options.MaxMemoryPoolSize)
                {
                    _state.State.Capacity = _options.MaxMemoryPoolSize;
                    await _state.WriteStateAsync();
                }

                // Initialize sorted sets from persisted state
                _sortedVerified = new SortedSet<PoolItemState>(_state.State.VerifiedTransactions.Values);
                _sortedUnverified = new SortedSet<PoolItemState>(_state.State.UnverifiedTransactions.Values);
            }
            await base.OnActivateAsync(cancellationToken);
        }

        public async Task<MemoryPoolAddResult> AddTransactionAsync(ITransactionData transaction)
        {
            if (UseLegacyState)
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

            if (!SerializationHelper.TryDeserializeTransaction(transaction, out var tx))
                return MemoryPoolAddResult.Invalid;

            if (tx.VerifyStateIndependent(_system.Settings) != VerifyResult.Succeed)
                return MemoryPoolAddResult.Invalid;

            var contains = _system.ContainsTransaction(tx.Hash);
            if (contains == ContainsTransactionType.ExistsInPool)
                return MemoryPoolAddResult.AlreadyInPool;
            if (contains == ContainsTransactionType.ExistsInLedger)
                return MemoryPoolAddResult.Invalid;

            if (_system.ContainsConflictHash(tx.Hash, tx.Signers.Select(s => s.Account)))
                return MemoryPoolAddResult.HasConflicts;

            var result = _system.MemPool.TryAdd(tx, _system.StoreView);
            return MapVerifyResult(result);
        }

        public Task<IReadOnlyList<PoolItemState>> GetVerifiedTransactionsAsync(int maxCount)
        {
            if (UseLegacyState)
            {
                var result = _sortedVerified!.Take(maxCount).ToList();
                return Task.FromResult<IReadOnlyList<PoolItemState>>(result);
            }

            var verified = _system.MemPool.GetSortedVerifiedTransactions(maxCount);
            var items = verified.Select(CreatePoolItemState).ToList();
            return Task.FromResult<IReadOnlyList<PoolItemState>>(items);
        }

        public Task<IReadOnlyList<PoolItemState>> GetUnverifiedTransactionsAsync(int maxCount)
        {
            if (UseLegacyState)
            {
                var result = _sortedUnverified!.Take(maxCount).ToList();
                return Task.FromResult<IReadOnlyList<PoolItemState>>(result);
            }

            _system.MemPool.GetVerifiedAndUnverifiedTransactions(out _, out var unverifiedTransactions);
            var items = unverifiedTransactions.Take(maxCount).Select(CreatePoolItemState).ToList();
            return Task.FromResult<IReadOnlyList<PoolItemState>>(items);
        }

        public async Task<bool> RemoveTransactionAsync(byte[] hash)
        {
            if (UseLegacyState)
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

            if (hash == null || hash.Length != UInt256.Length)
                return false;

            return _system.MemPool.RemoveTransaction(new UInt256(hash));
        }

        public async Task<bool> TryRemoveUnverifiedAsync(byte[] hash)
        {
            if (UseLegacyState)
            {
                var hashHex = Convert.ToHexString(hash);

                if (!_state.State.UnverifiedTransactions.TryGetValue(hashHex, out var item))
                    return false;

                _state.State.UnverifiedTransactions.Remove(hashHex);
                _sortedUnverified!.Remove(item);
                await _state.WriteStateAsync();
                return true;
            }

            if (hash == null || hash.Length != UInt256.Length)
                return false;

            return _system.MemPool.TryRemoveUnverified(new UInt256(hash));
        }

        public Task<int> GetCountAsync() =>
            UseLegacyState
                ? Task.FromResult(_state.State.VerifiedTransactions.Count + _state.State.UnverifiedTransactions.Count)
                : Task.FromResult(_system.MemPool.Count);

        public Task<int> GetVerifiedCountAsync() =>
            UseLegacyState
                ? Task.FromResult(_state.State.VerifiedTransactions.Count)
                : Task.FromResult(_system.MemPool.VerifiedCount);

        public Task<int> GetUnverifiedCountAsync() =>
            UseLegacyState
                ? Task.FromResult(_state.State.UnverifiedTransactions.Count)
                : Task.FromResult(_system.MemPool.UnVerifiedCount);

        public Task<bool> ContainsAsync(byte[] hash)
        {
            if (UseLegacyState)
            {
                var hashHex = Convert.ToHexString(hash);
                return Task.FromResult(
                    _state.State.VerifiedTransactions.ContainsKey(hashHex) ||
                    _state.State.UnverifiedTransactions.ContainsKey(hashHex));
            }

            if (hash == null || hash.Length != UInt256.Length)
                return Task.FromResult(false);

            return Task.FromResult(_system.MemPool.ContainsKey(new UInt256(hash)));
        }

        public Task<bool> ContainsConflictAsync(byte[] hash)
        {
            if (UseLegacyState)
            {
                var hashHex = Convert.ToHexString(hash);
                return Task.FromResult(_state.State.Conflicts.ContainsKey(hashHex));
            }

            if (hash == null || hash.Length != UInt256.Length)
                return Task.FromResult(false);

            return Task.FromResult(_system.MemPool.ContainsConflict(new UInt256(hash)));
        }

        public Task<ITransactionData?> GetTransactionAsync(byte[] hash)
        {
            if (UseLegacyState)
            {
                var hashHex = Convert.ToHexString(hash);

                if (!_state.State.VerifiedTransactions.TryGetValue(hashHex, out var item) &&
                    !_state.State.UnverifiedTransactions.TryGetValue(hashHex, out item))
                {
                    return Task.FromResult<ITransactionData?>(null);
                }

                if (item.SerializedData.Length == 0)
                    return Task.FromResult<ITransactionData?>(null);

                var reader = new MemoryReader(item.SerializedData);
                var transaction = reader.ReadSerializable<Transaction>();
                return Task.FromResult<ITransactionData?>(transaction);
            }

            if (hash == null || hash.Length != UInt256.Length)
                return Task.FromResult<ITransactionData?>(null);

            if (!_system.MemPool.TryGetValue(new UInt256(hash), out var tx))
                return Task.FromResult<ITransactionData?>(null);

            return Task.FromResult<ITransactionData?>(tx);
        }

        public async Task ClearAsync()
        {
            if (UseLegacyState)
            {
                _state.State.VerifiedTransactions.Clear();
                _state.State.UnverifiedTransactions.Clear();
                _state.State.Conflicts.Clear();
                _state.State.LastBroadcastBlock.Clear();
                _sortedVerified!.Clear();
                _sortedUnverified!.Clear();
                await _state.WriteStateAsync();
                return;
            }

            _system.MemPool.Clear();
        }

        public async Task InvalidateAllTransactionsAsync()
        {
            if (UseLegacyState)
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
                return;
            }

            _system.MemPool.InvalidateAllTransactions();
        }

        public async Task UpdateForBlockPersistedAsync(uint blockIndex, IEnumerable<byte[]> includedTxHashes)
        {
            if (UseLegacyState)
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
                return;
            }

            var block = NativeContract.Ledger.GetBlock(_system.StoreView, blockIndex);
            if (block != null)
                _system.MemPool.UpdatePoolForBlockPersisted(block, _system.StoreView);
        }

        public async Task<bool> ReVerifyTopUnverifiedAsync(int maxCount)
        {
            if (UseLegacyState)
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

            return _system.MemPool.ReVerifyTopUnverifiedTransactionsIfNeeded(maxCount, _system.StoreView);
        }

        public async Task<int> RemoveExpiredTransactionsAsync(uint currentBlockHeight)
        {
            if (UseLegacyState)
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

            return 0;
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

        private static PoolItemState CreatePoolItemState(Transaction tx)
        {
            return new PoolItemState
            {
                HashHex = Convert.ToHexString(tx.Hash.GetSpan()),
                Hash = tx.Hash.GetSpan().ToArray(),
                FeePerByte = tx.FeePerByte,
                NetworkFee = tx.NetworkFee,
                SystemFee = tx.SystemFee,
                ValidUntilBlock = tx.ValidUntilBlock,
                Timestamp = DateTime.UtcNow,
                SerializedData = tx.ToArray()
            };
        }

        private static MemoryPoolAddResult MapVerifyResult(VerifyResult result)
        {
            return result switch
            {
                VerifyResult.Succeed => MemoryPoolAddResult.Succeed,
                VerifyResult.AlreadyInPool => MemoryPoolAddResult.AlreadyInPool,
                VerifyResult.HasConflicts => MemoryPoolAddResult.HasConflicts,
                VerifyResult.OutOfMemory => MemoryPoolAddResult.OutOfMemory,
                VerifyResult.Expired => MemoryPoolAddResult.Expired,
                _ => MemoryPoolAddResult.Invalid
            };
        }

        #endregion
    }
}
#pragma warning restore CS0618
