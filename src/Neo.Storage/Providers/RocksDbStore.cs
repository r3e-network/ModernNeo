// Copyright (C) 2015-2025 The Neo Project.
//
// RocksDbStore.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Extensions;
using RocksDbSharp;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Neo.Persistence.Providers
{
    /// <summary>
    /// A RocksDB-based <see cref="IStore"/> implementation for production use.
    /// </summary>
    public class RocksDbStore : IStore
    {
        private readonly RocksDb _db;
        private readonly WriteOptions _writeOptions;
        private readonly ReadOptions _readOptions;
        private readonly BlockBasedTableOptions? _tableOptions;
        private readonly Cache? _blockCache;

        /// <inheritdoc/>
        public event IStore.OnNewSnapshotDelegate? OnNewSnapshot;

        /// <summary>
        /// Initializes a new instance of the <see cref="RocksDbStore"/> class with default options.
        /// </summary>
        /// <param name="path">The path to the database directory.</param>
        public RocksDbStore(string path)
            : this(path, RocksDbStoreOptions.Default)
        {
        }

        /// <summary>
        /// Initializes a new instance with custom options.
        /// </summary>
        /// <param name="path">The path to the database directory.</param>
        /// <param name="storeOptions">Store configuration options.</param>
        public RocksDbStore(string path, RocksDbStoreOptions storeOptions)
        {
            // Create block cache
            _blockCache = Cache.CreateLru(storeOptions.BlockCacheSize);

            // Configure block-based table options with Bloom filter
            _tableOptions = new BlockBasedTableOptions()
                .SetBlockCache(_blockCache)
                .SetCacheIndexAndFilterBlocks(storeOptions.CacheIndexAndFilterBlocks)
                .SetPinL0FilterAndIndexBlocksInCache(storeOptions.PinL0FilterAndIndexBlocksInCache);

            // Enable Bloom filter if configured
            if (storeOptions.EnableBloomFilter)
            {
                _tableOptions
                    .SetFilterPolicy(BloomFilterPolicy.Create(storeOptions.BloomFilterBitsPerKey))
                    .SetWholeKeyFiltering(storeOptions.WholeKeyFiltering);
            }

            // Map compression type
            var compression = storeOptions.CompressionType switch
            {
                RocksDbCompressionType.None => RocksDbSharp.Compression.No,
                RocksDbCompressionType.Snappy => RocksDbSharp.Compression.Snappy,
                RocksDbCompressionType.Lz4 => RocksDbSharp.Compression.Lz4,
                RocksDbCompressionType.Zstd => RocksDbSharp.Compression.Zstd,
                _ => RocksDbSharp.Compression.Snappy
            };

            // Configure database options
            var dbOptions = new DbOptions()
                .SetCreateIfMissing(true)
                .SetMaxOpenFiles(storeOptions.MaxOpenFiles)
                .SetKeepLogFileNum(storeOptions.KeepLogFileNum)
                .SetMaxTotalWalSize(storeOptions.MaxTotalWalSize)
                .SetWriteBufferSize(storeOptions.WriteBufferSize)
                .SetCompression(compression)
                .SetBlockBasedTableFactory(_tableOptions);

            _db = RocksDb.Open(dbOptions, path);
            _writeOptions = new WriteOptions();
            _readOptions = new ReadOptions();
        }

        /// <summary>
        /// Initializes a new instance with custom DbOptions (legacy constructor).
        /// </summary>
        /// <param name="path">The path to the database directory.</param>
        /// <param name="options">Custom database options.</param>
        public RocksDbStore(string path, DbOptions options)
        {
            _db = RocksDb.Open(options, path);
            _writeOptions = new WriteOptions();
            _readOptions = new ReadOptions();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Delete(byte[] key)
        {
            _db.Remove(key, null, _writeOptions);
        }

        public void Dispose()
        {
            _db?.Dispose();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IStoreSnapshot GetSnapshot()
        {
            var snapshot = new RocksDbSnapshot(this, _db);
            OnNewSnapshot?.Invoke(this, snapshot);
            return snapshot;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Put(byte[] key, byte[] value)
        {
            _db.Put(key, value, null, _writeOptions);
        }

        /// <inheritdoc/>
        public IEnumerable<(byte[] Key, byte[] Value)> Find(byte[]? keyOrPrefix, SeekDirection direction = SeekDirection.Forward)
        {
            keyOrPrefix ??= [];
            using var iterator = _db.NewIterator(readOptions: _readOptions);

            if (direction == SeekDirection.Forward)
            {
                for (iterator.Seek(keyOrPrefix); iterator.Valid(); iterator.Next())
                {
                    yield return (iterator.Key(), iterator.Value());
                }
            }
            else
            {
                if (keyOrPrefix.Length == 0)
                    yield break;

                // For backward seek, we need to find the last key <= keyOrPrefix
                iterator.SeekForPrev(keyOrPrefix);
                while (iterator.Valid())
                {
                    yield return (iterator.Key(), iterator.Value());
                    iterator.Prev();
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte[]? TryGet(byte[] key)
        {
            return _db.Get(key, null, _readOptions);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGet(byte[] key, [NotNullWhen(true)] out byte[]? value)
        {
            value = _db.Get(key, null, _readOptions);
            return value != null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Contains(byte[] key)
        {
            return _db.Get(key, null, _readOptions) != null;
        }
    }

    /// <summary>
    /// A snapshot of a RocksDB store.
    /// </summary>
    public class RocksDbSnapshot : IStoreSnapshot
    {
        private readonly RocksDbStore _store;
        private readonly RocksDb _db;
        private readonly Snapshot _snapshot;
        private readonly ReadOptions _readOptions;
        private readonly WriteBatch _batch;

        public IStore Store => _store;

        internal RocksDbSnapshot(RocksDbStore store, RocksDb db)
        {
            _store = store;
            _db = db;
            _snapshot = db.CreateSnapshot();
            _readOptions = new ReadOptions().SetSnapshot(_snapshot);
            _batch = new WriteBatch();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Delete(byte[] key)
        {
            _batch.Delete(key);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Put(byte[] key, byte[] value)
        {
            _batch.Put(key, value);
        }

        public void Commit()
        {
            _db.Write(_batch);
        }

        public void Dispose()
        {
            _batch.Dispose();
            _snapshot.Dispose();
        }

        public IEnumerable<(byte[] Key, byte[] Value)> Find(byte[]? keyOrPrefix, SeekDirection direction = SeekDirection.Forward)
        {
            keyOrPrefix ??= [];
            using var iterator = _db.NewIterator(readOptions: _readOptions);

            if (direction == SeekDirection.Forward)
            {
                for (iterator.Seek(keyOrPrefix); iterator.Valid(); iterator.Next())
                {
                    yield return (iterator.Key(), iterator.Value());
                }
            }
            else
            {
                if (keyOrPrefix.Length == 0)
                    yield break;

                iterator.SeekForPrev(keyOrPrefix);
                while (iterator.Valid())
                {
                    yield return (iterator.Key(), iterator.Value());
                    iterator.Prev();
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte[]? TryGet(byte[] key)
        {
            return _db.Get(key, null, _readOptions);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGet(byte[] key, [NotNullWhen(true)] out byte[]? value)
        {
            value = _db.Get(key, null, _readOptions);
            return value != null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Contains(byte[] key)
        {
            return _db.Get(key, null, _readOptions) != null;
        }
    }
}
