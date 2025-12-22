// Copyright (C) 2015-2025 The Neo Project.
//
// InMemoryTransactionPool.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Core.Interfaces;
using System.Collections.Concurrent;

namespace Neo.TxPool
{
    /// <summary>
    /// Thread-safe in-memory implementation of <see cref="ITransactionPool"/>.
    /// Provides a basic transaction pool with priority-based ordering.
    /// </summary>
    public class InMemoryTransactionPool : ITransactionPool
    {
        private readonly ConcurrentDictionary<string, PoolItemData> _transactions = new();
        private readonly object _lock = new();
        private readonly int _capacity;

        /// <inheritdoc/>
        public event EventHandler<ITransactionData>? TransactionAdded;

        /// <inheritdoc/>
        public event EventHandler<TransactionRemovedEventArgs>? TransactionRemoved;

        /// <inheritdoc/>
        public int Capacity => _capacity;

        /// <inheritdoc/>
        public int Count => _transactions.Count;

        /// <inheritdoc/>
        public int VerifiedCount => _transactions.Count(x => x.Value.IsVerified);

        /// <inheritdoc/>
        public int UnverifiedCount => _transactions.Count(x => !x.Value.IsVerified);

        /// <summary>
        /// Initializes a new instance of the <see cref="InMemoryTransactionPool"/> class.
        /// </summary>
        /// <param name="capacity">Maximum number of transactions in the pool.</param>
        public InMemoryTransactionPool(int capacity = 50000)
        {
            _capacity = capacity;
        }

        /// <inheritdoc/>
        public bool Contains(byte[] hash)
        {
            ArgumentNullException.ThrowIfNull(hash);
            return _transactions.ContainsKey(GetHashKey(hash));
        }

        /// <inheritdoc/>
        public bool TryGet(byte[] hash, out ITransactionData? transaction)
        {
            ArgumentNullException.ThrowIfNull(hash);
            if (_transactions.TryGetValue(GetHashKey(hash), out var item))
            {
                transaction = item.Transaction;
                return true;
            }
            transaction = null;
            return false;
        }

        /// <inheritdoc/>
        public bool TryAdd(ITransactionData transaction)
        {
            ArgumentNullException.ThrowIfNull(transaction);

            var hashKey = GetHashKey(transaction.Hash.GetSpan().ToArray());

            lock (_lock)
            {
                // Check if already exists
                if (_transactions.ContainsKey(hashKey))
                    return false;

                // Check capacity and evict if needed
                if (_transactions.Count >= _capacity)
                {
                    EvictLowestPriority();
                }

                var item = new PoolItemData(transaction);
                if (_transactions.TryAdd(hashKey, item))
                {
                    TransactionAdded?.Invoke(this, transaction);
                    return true;
                }
            }

            return false;
        }

        /// <inheritdoc/>
        public bool TryRemove(byte[] hash)
        {
            ArgumentNullException.ThrowIfNull(hash);

            var hashKey = GetHashKey(hash);
            if (_transactions.TryRemove(hashKey, out var item))
            {
                TransactionRemoved?.Invoke(this, new TransactionRemovedEventArgs(
                    item.Transaction, TransactionRemovalReason.Explicit));
                return true;
            }
            return false;
        }

        /// <inheritdoc/>
        public IEnumerable<ITransactionData> GetVerifiedTransactions(int maxCount)
        {
            return _transactions.Values
                .Where(x => x.IsVerified)
                .OrderByDescending(x => x.FeePerByte)
                .ThenByDescending(x => x.NetworkFee)
                .Take(maxCount)
                .Select(x => x.Transaction);
        }

        /// <inheritdoc/>
        public IEnumerable<ITransactionData> GetVerifiedTransactions()
        {
            return _transactions.Values
                .Where(x => x.IsVerified)
                .OrderByDescending(x => x.FeePerByte)
                .ThenByDescending(x => x.NetworkFee)
                .Select(x => x.Transaction);
        }

        /// <inheritdoc/>
        public void Clear()
        {
            lock (_lock)
            {
                var items = _transactions.Values.ToList();
                _transactions.Clear();

                foreach (var item in items)
                {
                    TransactionRemoved?.Invoke(this, new TransactionRemovedEventArgs(
                        item.Transaction, TransactionRemovalReason.Explicit));
                }
            }
        }

        private void EvictLowestPriority()
        {
            var lowest = _transactions
                .OrderBy(x => x.Value.FeePerByte)
                .ThenBy(x => x.Value.NetworkFee)
                .FirstOrDefault();

            if (lowest.Key != null && _transactions.TryRemove(lowest.Key, out var item))
            {
                TransactionRemoved?.Invoke(this, new TransactionRemovedEventArgs(
                    item.Transaction, TransactionRemovalReason.CapacityExceeded));
            }
        }

        private static string GetHashKey(byte[] hash) => Convert.ToHexString(hash);

        /// <summary>
        /// Internal pool item data structure.
        /// </summary>
        private class PoolItemData
        {
            public ITransactionData Transaction { get; }
            public DateTime Timestamp { get; }
            public bool IsVerified { get; set; } = true;
            public long FeePerByte => Transaction.FeePerByte;
            public long NetworkFee => Transaction.NetworkFee;

            public PoolItemData(ITransactionData transaction)
            {
                Transaction = transaction;
                Timestamp = DateTime.UtcNow;
            }
        }
    }
}
