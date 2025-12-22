// Copyright (C) 2015-2025 The Neo Project.
//
// TieredCache.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Neo.Persistence.Caching
{
    /// <summary>
    /// A tiered caching system with L1 (hot) and L2 (warm) caches.
    /// Provides automatic promotion/demotion based on access patterns.
    /// </summary>
    public sealed class TieredCache<TKey, TValue> : IDisposable
        where TKey : notnull
    {
        private readonly ConcurrentDictionary<TKey, CacheEntry> _l1Cache; // Hot cache
        private readonly ConcurrentDictionary<TKey, CacheEntry> _l2Cache; // Warm cache
        private readonly IEqualityComparer<TKey>? _comparer;
        private readonly TieredCacheOptions _options;
        private readonly Timer _evictionTimer;
        private readonly SemaphoreSlim _evictionLock = new(1, 1);
        private long _hits;
        private long _misses;
        private bool _disposed;

        /// <summary>
        /// Cache entry with metadata for eviction decisions.
        /// </summary>
        private sealed class CacheEntry
        {
            public TValue Value { get; set; }
            private long _accessCount;
            public long AccessCount => _accessCount;
            public DateTime LastAccess { get; set; }
            public DateTime Created { get; }
            public int Size { get; }

            public CacheEntry(TValue value, int size)
            {
                Value = value;
                Size = size;
                _accessCount = 1;
                LastAccess = DateTime.UtcNow;
                Created = DateTime.UtcNow;
            }

            public void RecordAccess()
            {
                Interlocked.Increment(ref _accessCount);
                LastAccess = DateTime.UtcNow;
            }
        }

        /// <summary>
        /// Gets the number of items in L1 cache.
        /// </summary>
        public int L1Count => _l1Cache.Count;

        /// <summary>
        /// Gets the number of items in L2 cache.
        /// </summary>
        public int L2Count => _l2Cache.Count;

        /// <summary>
        /// Gets the total number of cached items.
        /// </summary>
        public int TotalCount => L1Count + L2Count;

        /// <summary>
        /// Gets the cache hit ratio.
        /// </summary>
        public double HitRatio
        {
            get
            {
                var total = Interlocked.Read(ref _hits) + Interlocked.Read(ref _misses);
                return total == 0 ? 0 : (double)Interlocked.Read(ref _hits) / total;
            }
        }

        /// <summary>
        /// Creates a new tiered cache with the specified options.
        /// </summary>
        public TieredCache(TieredCacheOptions? options = null, IEqualityComparer<TKey>? comparer = null)
        {
            _options = options ?? new TieredCacheOptions();
            _comparer = comparer;
            _l1Cache = new ConcurrentDictionary<TKey, CacheEntry>(comparer);
            _l2Cache = new ConcurrentDictionary<TKey, CacheEntry>(comparer);

            // Start background eviction timer
            _evictionTimer = new Timer(
                _ => _ = EvictAsync(),
                null,
                _options.EvictionInterval,
                _options.EvictionInterval);
        }

        /// <summary>
        /// Gets a value from the cache, checking L1 first, then L2.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGet(TKey key, [NotNullWhen(true)] out TValue? value)
        {
            // Check L1 first (hot cache)
            if (_l1Cache.TryGetValue(key, out var entry))
            {
                entry.RecordAccess();
                Interlocked.Increment(ref _hits);
                value = entry.Value!;
                return true;
            }

            // Check L2 (warm cache) and promote if found
            if (_l2Cache.TryGetValue(key, out entry))
            {
                entry.RecordAccess();
                Interlocked.Increment(ref _hits);

                // Promote to L1 if access count exceeds threshold
                if (entry.AccessCount >= _options.PromotionThreshold)
                {
                    PromoteToL1(key, entry);
                }

                value = entry.Value!;
                return true;
            }

            Interlocked.Increment(ref _misses);
            value = default;
            return false;
        }

        /// <summary>
        /// Adds or updates a value in the cache.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Set(TKey key, TValue value, int size = 1)
        {
            var entry = new CacheEntry(value, size);

            // New items go to L1 if there's space, otherwise L2
            if (_l1Cache.Count < _options.L1MaxItems)
            {
                _l1Cache[key] = entry;
                _l2Cache.TryRemove(key, out _);
            }
            else
            {
                _l2Cache[key] = entry;
            }
        }

        /// <summary>
        /// Removes a value from the cache.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Remove(TKey key)
        {
            var removed = _l1Cache.TryRemove(key, out _);
            removed |= _l2Cache.TryRemove(key, out _);
            return removed;
        }

        /// <summary>
        /// Checks if the cache contains the specified key.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Contains(TKey key)
        {
            return _l1Cache.ContainsKey(key) || _l2Cache.ContainsKey(key);
        }

        /// <summary>
        /// Clears all cached items.
        /// </summary>
        public void Clear()
        {
            _l1Cache.Clear();
            _l2Cache.Clear();
            Interlocked.Exchange(ref _hits, 0);
            Interlocked.Exchange(ref _misses, 0);
        }

        /// <summary>
        /// Gets cache statistics.
        /// </summary>
        public CacheStatistics GetStatistics()
        {
            return new CacheStatistics
            {
                L1Count = L1Count,
                L2Count = L2Count,
                TotalCount = TotalCount,
                Hits = Interlocked.Read(ref _hits),
                Misses = Interlocked.Read(ref _misses),
                HitRatio = HitRatio
            };
        }

        private void PromoteToL1(TKey key, CacheEntry entry)
        {
            // Only promote if L1 has space or we can demote something
            if (_l1Cache.Count >= _options.L1MaxItems)
            {
                DemoteColdestFromL1();
            }

            if (_l2Cache.TryRemove(key, out _))
            {
                _l1Cache[key] = entry;
            }
        }

        private void DemoteColdestFromL1()
        {
            // Find the entry with lowest access count
            TKey? coldestKey = default;
            long lowestAccess = long.MaxValue;

            foreach (var kvp in _l1Cache)
            {
                if (kvp.Value.AccessCount < lowestAccess)
                {
                    lowestAccess = kvp.Value.AccessCount;
                    coldestKey = kvp.Key;
                }
            }

            if (coldestKey != null && _l1Cache.TryRemove(coldestKey, out var entry))
            {
                // Move to L2 if there's space
                if (_l2Cache.Count < _options.L2MaxItems)
                {
                    _l2Cache[coldestKey] = entry;
                }
            }
        }

        private async Task EvictAsync()
        {
            if (_disposed) return;

            if (!await _evictionLock.WaitAsync(0))
                return; // Skip if eviction is already running

            try
            {
                var now = DateTime.UtcNow;

                // Evict expired entries from L1
                foreach (var kvp in _l1Cache)
                {
                    if (now - kvp.Value.LastAccess > _options.L1Ttl)
                    {
                        if (_l1Cache.TryRemove(kvp.Key, out var entry))
                        {
                            // Demote to L2 instead of removing completely
                            if (_l2Cache.Count < _options.L2MaxItems)
                            {
                                _l2Cache[kvp.Key] = entry;
                            }
                        }
                    }
                }

                // Evict expired entries from L2
                foreach (var kvp in _l2Cache)
                {
                    if (now - kvp.Value.LastAccess > _options.L2Ttl)
                    {
                        _l2Cache.TryRemove(kvp.Key, out _);
                    }
                }

                // Enforce size limits
                while (_l1Cache.Count > _options.L1MaxItems)
                {
                    DemoteColdestFromL1();
                }

                while (_l2Cache.Count > _options.L2MaxItems)
                {
                    EvictColdestFromL2();
                }
            }
            finally
            {
                _evictionLock.Release();
            }
        }

        private void EvictColdestFromL2()
        {
            TKey? coldestKey = default;
            long lowestAccess = long.MaxValue;

            foreach (var kvp in _l2Cache)
            {
                if (kvp.Value.AccessCount < lowestAccess)
                {
                    lowestAccess = kvp.Value.AccessCount;
                    coldestKey = kvp.Key;
                }
            }

            if (coldestKey != null)
            {
                _l2Cache.TryRemove(coldestKey, out _);
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _evictionTimer.Dispose();
            _evictionLock.Dispose();
            _l1Cache.Clear();
            _l2Cache.Clear();
        }
    }

    /// <summary>
    /// Configuration options for tiered cache.
    /// </summary>
    public sealed class TieredCacheOptions
    {
        /// <summary>
        /// Maximum number of items in L1 cache. Default: 10000.
        /// </summary>
        public int L1MaxItems { get; set; } = 10_000;

        /// <summary>
        /// Maximum number of items in L2 cache. Default: 100000.
        /// </summary>
        public int L2MaxItems { get; set; } = 100_000;

        /// <summary>
        /// Time-to-live for L1 cache entries. Default: 5 minutes.
        /// </summary>
        public TimeSpan L1Ttl { get; set; } = TimeSpan.FromMinutes(5);

        /// <summary>
        /// Time-to-live for L2 cache entries. Default: 30 minutes.
        /// </summary>
        public TimeSpan L2Ttl { get; set; } = TimeSpan.FromMinutes(30);

        /// <summary>
        /// Number of accesses required to promote from L2 to L1. Default: 3.
        /// </summary>
        public int PromotionThreshold { get; set; } = 3;

        /// <summary>
        /// Interval between eviction runs. Default: 1 minute.
        /// </summary>
        public TimeSpan EvictionInterval { get; set; } = TimeSpan.FromMinutes(1);
    }

    /// <summary>
    /// Cache statistics for monitoring.
    /// </summary>
    public readonly struct CacheStatistics
    {
        public int L1Count { get; init; }
        public int L2Count { get; init; }
        public int TotalCount { get; init; }
        public long Hits { get; init; }
        public long Misses { get; init; }
        public double HitRatio { get; init; }
    }
}
