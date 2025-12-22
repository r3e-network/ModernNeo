// Copyright (C) 2015-2025 The Neo Project.
//
// CacheWarmupService.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Neo.Persistence.Caching
{
    /// <summary>
    /// Service for warming up caches with frequently accessed data.
    /// Supports multiple warmup strategies for different data patterns.
    /// </summary>
    public sealed class CacheWarmupService : IAsyncDisposable
    {
        private readonly LmdbCacheStore _cacheStore;
        private readonly CacheWarmupOptions _options;
        private readonly List<IWarmupStrategy> _strategies = [];
        private readonly SemaphoreSlim _warmupLock = new(1, 1);
        private CancellationTokenSource? _cts;
        private Task? _backgroundTask;
        private bool _disposed;

        /// <summary>
        /// Event raised when warmup starts.
        /// </summary>
        public event Action<WarmupPhase>? OnWarmupStarted;

        /// <summary>
        /// Event raised when warmup completes.
        /// </summary>
        public event Action<WarmupResult>? OnWarmupCompleted;

        /// <summary>
        /// Event raised on warmup progress.
        /// </summary>
        public event Action<WarmupProgress>? OnWarmupProgress;

        /// <summary>
        /// Creates a new cache warmup service.
        /// </summary>
        public CacheWarmupService(LmdbCacheStore cacheStore, CacheWarmupOptions? options = null)
        {
            _cacheStore = cacheStore ?? throw new ArgumentNullException(nameof(cacheStore));
            _options = options ?? new CacheWarmupOptions();
        }

        /// <summary>
        /// Registers a warmup strategy.
        /// </summary>
        public void RegisterStrategy(IWarmupStrategy strategy)
        {
            _strategies.Add(strategy);
        }

        /// <summary>
        /// Starts the warmup service.
        /// </summary>
        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            if (_cts != null)
                return;

            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            // Perform initial warmup
            if (_options.WarmupOnStart)
            {
                await WarmupAsync(_cts.Token);
            }

            // Start background refresh if enabled
            if (_options.EnablePeriodicRefresh)
            {
                _backgroundTask = BackgroundRefreshLoopAsync(_cts.Token);
            }
        }

        /// <summary>
        /// Stops the warmup service.
        /// </summary>
        public async Task StopAsync(CancellationToken cancellationToken = default)
        {
            if (_cts == null)
                return;

            _cts.Cancel();

            if (_backgroundTask != null)
            {
                try
                {
                    await _backgroundTask.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
                }
                catch (OperationCanceledException) { }
                catch (TimeoutException) { }
            }

            _cts.Dispose();
            _cts = null;
        }

        /// <summary>
        /// Performs a full cache warmup using all registered strategies.
        /// </summary>
        public async Task WarmupAsync(CancellationToken cancellationToken = default)
        {
            if (!await _warmupLock.WaitAsync(0, cancellationToken))
            {
                // Warmup already in progress
                return;
            }

            try
            {
                var startTime = DateTime.UtcNow;
                var totalItems = 0;
                var results = new List<StrategyResult>();

                OnWarmupStarted?.Invoke(WarmupPhase.Starting);

                foreach (var strategy in _strategies.OrderBy(s => s.Priority))
                {
                    if (cancellationToken.IsCancellationRequested)
                        break;

                    OnWarmupStarted?.Invoke(WarmupPhase.ExecutingStrategy);

                    var strategyStart = DateTime.UtcNow;
                    var itemsWarmed = 0;

                    try
                    {
                        await foreach (var batch in strategy.GetWarmupBatchesAsync(cancellationToken))
                        {
                            if (cancellationToken.IsCancellationRequested)
                                break;

                            await _cacheStore.WarmupAsync(batch, cancellationToken);
                            itemsWarmed += batch.Count();

                            OnWarmupProgress?.Invoke(new WarmupProgress
                            {
                                StrategyName = strategy.Name,
                                ItemsWarmed = itemsWarmed,
                                ElapsedTime = DateTime.UtcNow - strategyStart
                            });
                        }

                        results.Add(new StrategyResult
                        {
                            StrategyName = strategy.Name,
                            ItemsWarmed = itemsWarmed,
                            Duration = DateTime.UtcNow - strategyStart,
                            Success = true
                        });

                        totalItems += itemsWarmed;
                    }
                    catch (Exception ex)
                    {
                        results.Add(new StrategyResult
                        {
                            StrategyName = strategy.Name,
                            ItemsWarmed = itemsWarmed,
                            Duration = DateTime.UtcNow - strategyStart,
                            Success = false,
                            Error = ex.Message
                        });
                    }
                }

                OnWarmupCompleted?.Invoke(new WarmupResult
                {
                    TotalItemsWarmed = totalItems,
                    TotalDuration = DateTime.UtcNow - startTime,
                    StrategyResults = results
                });
            }
            finally
            {
                _warmupLock.Release();
            }
        }

        /// <summary>
        /// Warms up specific key prefixes.
        /// </summary>
        public async Task WarmupPrefixesAsync(IEnumerable<byte[]> prefixes, CancellationToken cancellationToken = default)
        {
            foreach (var prefix in prefixes)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                await _cacheStore.WarmupPrefixAsync(prefix, _options.MaxItemsPerPrefix, cancellationToken);
            }
        }

        private async Task BackgroundRefreshLoopAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(_options.RefreshInterval, cancellationToken);
                    await WarmupAsync(cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (_disposed) return;
            _disposed = true;

            await StopAsync(CancellationToken.None);
            _warmupLock.Dispose();
        }
    }

    /// <summary>
    /// Interface for cache warmup strategies.
    /// </summary>
    public interface IWarmupStrategy
    {
        /// <summary>
        /// Gets the strategy name.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Gets the priority (lower = higher priority).
        /// </summary>
        int Priority { get; }

        /// <summary>
        /// Gets batches of keys to warm up.
        /// </summary>
        IAsyncEnumerable<IEnumerable<byte[]>> GetWarmupBatchesAsync(CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Warmup strategy for blockchain state data (contracts, storage).
    /// </summary>
    public sealed class BlockchainStateWarmupStrategy : IWarmupStrategy
    {
        private readonly IStore _store;
        private readonly BlockchainStateWarmupOptions _options;

        public string Name => "BlockchainState";
        public int Priority => 1;

        public BlockchainStateWarmupStrategy(IStore store, BlockchainStateWarmupOptions? options = null)
        {
            _store = store;
            _options = options ?? new BlockchainStateWarmupOptions();
        }

        public async IAsyncEnumerable<IEnumerable<byte[]>> GetWarmupBatchesAsync(
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            // Warm up native contract storage
            foreach (var prefix in _options.NativeContractPrefixes)
            {
                if (cancellationToken.IsCancellationRequested)
                    yield break;

                var keys = new List<byte[]>();
                foreach (var (key, _) in _store.Find(prefix))
                {
                    keys.Add(key);
                    if (keys.Count >= _options.BatchSize)
                    {
                        yield return keys;
                        keys = [];
                    }
                }

                if (keys.Count > 0)
                    yield return keys;

                await Task.Yield(); // Allow cancellation check
            }
        }
    }

    /// <summary>
    /// Warmup strategy for recent blocks and transactions.
    /// </summary>
    public sealed class RecentBlocksWarmupStrategy : IWarmupStrategy
    {
        private readonly IStore _store;
        private readonly RecentBlocksWarmupOptions _options;

        public string Name => "RecentBlocks";
        public int Priority => 2;

        public RecentBlocksWarmupStrategy(IStore store, RecentBlocksWarmupOptions? options = null)
        {
            _store = store;
            _options = options ?? new RecentBlocksWarmupOptions();
        }

        public async IAsyncEnumerable<IEnumerable<byte[]>> GetWarmupBatchesAsync(
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            // Warm up recent block data
            if (_options.BlockPrefix != null)
            {
                var keys = new List<byte[]>();
                int count = 0;

                foreach (var (key, _) in _store.Find(_options.BlockPrefix, SeekDirection.Backward))
                {
                    if (cancellationToken.IsCancellationRequested || count >= _options.RecentBlockCount)
                        break;

                    keys.Add(key);
                    count++;

                    if (keys.Count >= _options.BatchSize)
                    {
                        yield return keys;
                        keys = [];
                    }
                }

                if (keys.Count > 0)
                    yield return keys;
            }

            await Task.Yield();
        }
    }

    /// <summary>
    /// Warmup strategy based on access frequency tracking.
    /// </summary>
    public sealed class FrequencyBasedWarmupStrategy : IWarmupStrategy
    {
        private readonly IAccessTracker _accessTracker;
        private readonly FrequencyWarmupOptions _options;

        public string Name => "FrequencyBased";
        public int Priority => 3;

        public FrequencyBasedWarmupStrategy(IAccessTracker accessTracker, FrequencyWarmupOptions? options = null)
        {
            _accessTracker = accessTracker;
            _options = options ?? new FrequencyWarmupOptions();
        }

        public async IAsyncEnumerable<IEnumerable<byte[]>> GetWarmupBatchesAsync(
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var hotKeys = _accessTracker.GetMostAccessedKeys(_options.TopKeyCount);
            var batch = new List<byte[]>();

            foreach (var key in hotKeys)
            {
                if (cancellationToken.IsCancellationRequested)
                    yield break;

                batch.Add(key);

                if (batch.Count >= _options.BatchSize)
                {
                    yield return batch;
                    batch = [];
                }
            }

            if (batch.Count > 0)
                yield return batch;

            await Task.Yield();
        }
    }

    /// <summary>
    /// Interface for tracking key access frequency.
    /// </summary>
    public interface IAccessTracker
    {
        /// <summary>
        /// Records an access to the specified key.
        /// </summary>
        void RecordAccess(byte[] key);

        /// <summary>
        /// Gets the most frequently accessed keys.
        /// </summary>
        IEnumerable<byte[]> GetMostAccessedKeys(int count);

        /// <summary>
        /// Clears access statistics.
        /// </summary>
        void Clear();
    }

    /// <summary>
    /// Simple in-memory access tracker using Count-Min Sketch approximation.
    /// </summary>
    public sealed class InMemoryAccessTracker : IAccessTracker
    {
        private readonly Dictionary<ByteArrayKey, long> _accessCounts = new();
        private readonly object _lock = new();
        private readonly int _maxTrackedKeys;

        public InMemoryAccessTracker(int maxTrackedKeys = 100000)
        {
            _maxTrackedKeys = maxTrackedKeys;
        }

        public void RecordAccess(byte[] key)
        {
            var cacheKey = new ByteArrayKey(key);

            lock (_lock)
            {
                if (_accessCounts.TryGetValue(cacheKey, out var count))
                {
                    _accessCounts[cacheKey] = count + 1;
                }
                else if (_accessCounts.Count < _maxTrackedKeys)
                {
                    _accessCounts[cacheKey] = 1;
                }
            }
        }

        public IEnumerable<byte[]> GetMostAccessedKeys(int count)
        {
            lock (_lock)
            {
                return _accessCounts
                    .OrderByDescending(kvp => kvp.Value)
                    .Take(count)
                    .Select(kvp => kvp.Key.Data)
                    .ToList();
            }
        }

        public void Clear()
        {
            lock (_lock)
            {
                _accessCounts.Clear();
            }
        }

        private readonly struct ByteArrayKey : IEquatable<ByteArrayKey>
        {
            private readonly byte[] _data;
            private readonly int _hashCode;

            public ByteArrayKey(byte[] data)
            {
                _data = data;
                _hashCode = ComputeHashCode(data);
            }

            public byte[] Data => _data;

            private static int ComputeHashCode(byte[] data)
            {
                unchecked
                {
                    var hash = 17;
                    foreach (var b in data)
                        hash = hash * 31 + b;
                    return hash;
                }
            }

            public bool Equals(ByteArrayKey other) => _data.AsSpan().SequenceEqual(other._data);
            public override bool Equals(object? obj) => obj is ByteArrayKey other && Equals(other);
            public override int GetHashCode() => _hashCode;
        }
    }

    #region Options

    /// <summary>
    /// Configuration options for cache warmup service.
    /// </summary>
    public sealed class CacheWarmupOptions
    {
        /// <summary>
        /// Whether to perform warmup on service start. Default: true.
        /// </summary>
        public bool WarmupOnStart { get; set; } = true;

        /// <summary>
        /// Enable periodic cache refresh. Default: false.
        /// </summary>
        public bool EnablePeriodicRefresh { get; set; } = false;

        /// <summary>
        /// Interval between periodic refreshes. Default: 1 hour.
        /// </summary>
        public TimeSpan RefreshInterval { get; set; } = TimeSpan.FromHours(1);

        /// <summary>
        /// Maximum items to warm up per prefix. Default: 10000.
        /// </summary>
        public int MaxItemsPerPrefix { get; set; } = 10000;
    }

    /// <summary>
    /// Options for blockchain state warmup strategy.
    /// </summary>
    public sealed class BlockchainStateWarmupOptions
    {
        /// <summary>
        /// Prefixes for native contract storage to warm up.
        /// </summary>
        public IReadOnlyList<byte[]> NativeContractPrefixes { get; set; } = [];

        /// <summary>
        /// Batch size for warmup operations. Default: 1000.
        /// </summary>
        public int BatchSize { get; set; } = 1000;
    }

    /// <summary>
    /// Options for recent blocks warmup strategy.
    /// </summary>
    public sealed class RecentBlocksWarmupOptions
    {
        /// <summary>
        /// Prefix for block storage.
        /// </summary>
        public byte[]? BlockPrefix { get; set; }

        /// <summary>
        /// Number of recent blocks to warm up. Default: 1000.
        /// </summary>
        public int RecentBlockCount { get; set; } = 1000;

        /// <summary>
        /// Batch size for warmup operations. Default: 100.
        /// </summary>
        public int BatchSize { get; set; } = 100;
    }

    /// <summary>
    /// Options for frequency-based warmup strategy.
    /// </summary>
    public sealed class FrequencyWarmupOptions
    {
        /// <summary>
        /// Number of top keys to warm up. Default: 10000.
        /// </summary>
        public int TopKeyCount { get; set; } = 10000;

        /// <summary>
        /// Batch size for warmup operations. Default: 1000.
        /// </summary>
        public int BatchSize { get; set; } = 1000;
    }

    #endregion

    #region Result Types

    /// <summary>
    /// Warmup phase enumeration.
    /// </summary>
    public enum WarmupPhase
    {
        Starting,
        ExecutingStrategy,
        Completed
    }

    /// <summary>
    /// Warmup progress information.
    /// </summary>
    public readonly struct WarmupProgress
    {
        public string StrategyName { get; init; }
        public int ItemsWarmed { get; init; }
        public TimeSpan ElapsedTime { get; init; }
    }

    /// <summary>
    /// Result of a single warmup strategy execution.
    /// </summary>
    public readonly struct StrategyResult
    {
        public string StrategyName { get; init; }
        public int ItemsWarmed { get; init; }
        public TimeSpan Duration { get; init; }
        public bool Success { get; init; }
        public string? Error { get; init; }
    }

    /// <summary>
    /// Overall warmup result.
    /// </summary>
    public readonly struct WarmupResult
    {
        public int TotalItemsWarmed { get; init; }
        public TimeSpan TotalDuration { get; init; }
        public IReadOnlyList<StrategyResult> StrategyResults { get; init; }
    }

    #endregion
}
