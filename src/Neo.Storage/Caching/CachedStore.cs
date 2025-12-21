// Copyright (C) 2015-2025 The Neo Project.
//
// CachedStore.cs file belongs to the neo project and is free
// software distributed under the MIT software license.

using Neo.Extensions;
using Neo.Persistence.Compression;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Neo.Persistence.Caching
{
    /// <summary>
    /// A store wrapper that adds tiered caching, batch writing, and compression.
    /// </summary>
    public sealed class CachedStore : IStore
    {
        private readonly IStore _innerStore;
        private readonly TieredCache<ByteArrayKey, byte[]> _cache;
        private readonly BatchWriter _batchWriter;
        private readonly StorageCompressor? _compressor;
        private readonly CachedStoreOptions _options;
        private bool _disposed;

        /// <summary>
        /// Wrapper for byte[] to use as dictionary key.
        /// </summary>
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
                    {
                        hash = hash * 31 + b;
                    }
                    return hash;
                }
            }

            public bool Equals(ByteArrayKey other)
            {
                return _data.AsSpan().SequenceEqual(other._data);
            }

            public override bool Equals(object? obj)
            {
                return obj is ByteArrayKey other && Equals(other);
            }

            public override int GetHashCode() => _hashCode;
        }

        /// <inheritdoc/>
        public event IStore.OnNewSnapshotDelegate? OnNewSnapshot;

        /// <summary>
        /// Gets cache statistics.
        /// </summary>
        public CacheStatistics CacheStats => _cache.GetStatistics();

        /// <summary>
        /// Gets batch writer statistics.
        /// </summary>
        public BatchWriterStatistics BatchStats => _batchWriter.GetStatistics();

        /// <summary>
        /// Gets compression statistics.
        /// </summary>
        public CompressionStatistics? CompressionStats => _compressor?.GetStatistics();

        /// <summary>
        /// Creates a new cached store wrapping the specified inner store.
        /// </summary>
        public CachedStore(IStore innerStore, CachedStoreOptions? options = null)
        {
            _innerStore = innerStore ?? throw new ArgumentNullException(nameof(innerStore));
            _options = options ?? new CachedStoreOptions();

            _cache = new TieredCache<ByteArrayKey, byte[]>(_options.CacheOptions);
            _batchWriter = new BatchWriter(innerStore, _options.BatchOptions);

            if (_options.EnableCompression)
            {
                _compressor = new StorageCompressor(_options.CompressionOptions);
            }

            // Forward snapshot events
            _innerStore.OnNewSnapshot += (sender, snapshot) => OnNewSnapshot?.Invoke(this, snapshot);
        }

        /// <inheritdoc/>
        public byte[]? TryGet(byte[] key)
        {
            var cacheKey = new ByteArrayKey(key);

            // Check cache first
            if (_cache.TryGet(cacheKey, out var cached))
            {
                return _compressor != null ? _compressor.Decompress(cached) : cached;
            }

            // Load from store
            if (_innerStore.TryGet(key, out var value))
            {
                // Cache the raw value (possibly compressed)
                _cache.Set(cacheKey, value);
                return _compressor != null ? _compressor.Decompress(value) : value;
            }

            return null;
        }

        /// <inheritdoc/>
        public bool TryGet(byte[] key, [NotNullWhen(true)] out byte[]? value)
        {
            value = TryGet(key);
            return value != null;
        }

        /// <inheritdoc/>
        public bool Contains(byte[] key)
        {
            var cacheKey = new ByteArrayKey(key);
            return _cache.Contains(cacheKey) || _innerStore.Contains(key);
        }

        /// <inheritdoc/>
        public IEnumerable<(byte[] Key, byte[] Value)> Find(byte[]? keyOrPrefix, SeekDirection direction = SeekDirection.Forward)
        {
            // For range queries, bypass cache and go directly to store
            foreach (var (key, value) in _innerStore.Find(keyOrPrefix, direction))
            {
                var decompressed = _compressor != null ? _compressor.Decompress(value) : value;

                // Populate cache with found items
                _cache.Set(new ByteArrayKey(key), value);

                yield return (key, decompressed);
            }
        }

        /// <inheritdoc/>
        public void Put(byte[] key, byte[] value)
        {
            var cacheKey = new ByteArrayKey(key);
            var storedValue = _compressor != null ? _compressor.Compress(value) : value;

            // Update cache
            _cache.Set(cacheKey, storedValue);

            // Queue for batch write
            if (_options.EnableBatchWrites)
            {
                _batchWriter.Put(key, storedValue);
            }
            else
            {
                _innerStore.Put(key, storedValue);
            }
        }

        /// <inheritdoc/>
        public void Delete(byte[] key)
        {
            var cacheKey = new ByteArrayKey(key);

            // Remove from cache
            _cache.Remove(cacheKey);

            // Queue for batch delete
            if (_options.EnableBatchWrites)
            {
                _batchWriter.Delete(key);
            }
            else
            {
                _innerStore.Delete(key);
            }
        }

        /// <inheritdoc/>
        public IStoreSnapshot GetSnapshot()
        {
            var snapshot = _innerStore.GetSnapshot();
            // Note: OnNewSnapshot is already forwarded via the subscription in constructor
            // Don't invoke it again here to avoid duplicate events
            return snapshot;
        }

        /// <summary>
        /// Flushes all pending writes to the underlying store.
        /// </summary>
        public async System.Threading.Tasks.Task FlushAsync()
        {
            await _batchWriter.FlushAsync().ConfigureAwait(false);
        }

        /// <summary>
        /// Clears the cache.
        /// </summary>
        public void ClearCache()
        {
            _cache.Clear();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            // Flush pending writes synchronously
            _batchWriter.DisposeAsync().AsTask().GetAwaiter().GetResult();
            _cache.Dispose();
            _innerStore.Dispose();
        }
    }

    /// <summary>
    /// Configuration options for cached store.
    /// </summary>
    public sealed class CachedStoreOptions
    {
        /// <summary>
        /// Options for the tiered cache.
        /// </summary>
        public TieredCacheOptions CacheOptions { get; set; } = new();

        /// <summary>
        /// Options for the batch writer.
        /// </summary>
        public BatchWriterOptions BatchOptions { get; set; } = new();

        /// <summary>
        /// Options for compression.
        /// </summary>
        public StorageCompressorOptions CompressionOptions { get; set; } = new();

        /// <summary>
        /// Enable batch writes. Default: true.
        /// </summary>
        public bool EnableBatchWrites { get; set; } = true;

        /// <summary>
        /// Enable compression. Default: true.
        /// </summary>
        public bool EnableCompression { get; set; } = true;
    }
}
