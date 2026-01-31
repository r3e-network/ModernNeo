// Copyright (C) 2015-2025 The Neo Project.
//
// StoreBasedBlockStorageService.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Core.Interfaces;
using Neo.Extensions;
using Neo.IO;
using Neo.Network.P2P.Payloads;
using Neo.Orleans.Utilities;
using Neo.Persistence;

namespace Neo.Orleans.Services
{
    /// <summary>
    /// IBlockStorageService implementation backed by Neo.Storage IStore.
    /// Supports any IStore implementation (MemoryStore, RocksDB, LevelDB, etc.)
    /// </summary>
    public class StoreBasedBlockStorageService : IBlockStorageService, IDisposable
    {
        private readonly IStore _store;
        private readonly bool _ownsStore;

        // Storage key prefixes for different data types
        private static readonly byte[] BlockByHashPrefix = [0x01];
        private static readonly byte[] BlockByIndexPrefix = [0x02];
        private static readonly byte[] HeightKey = [0x03, 0x00];
        private static readonly byte[] TransactionByHashPrefix = [0x04];

        /// <summary>
        /// Block serializer/deserializer delegate.
        /// </summary>
        public Func<Block, byte[]>? BlockSerializer { get; set; }

        /// <summary>
        /// Block deserializer delegate.
        /// </summary>
        public Func<byte[], Block>? BlockDeserializer { get; set; }

        /// <summary>
        /// Creates a new StoreBasedBlockStorageService with the specified store.
        /// </summary>
        /// <param name="store">The underlying IStore implementation.</param>
        /// <param name="ownsStore">Whether this service owns the store and should dispose it.</param>
        public StoreBasedBlockStorageService(IStore store, bool ownsStore = true)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _ownsStore = ownsStore;
        }

        /// <inheritdoc/>
        public Task<bool> StoreBlockAsync(Block block)
        {
            ArgumentNullException.ThrowIfNull(block);

            var hashBytes = block.Hash.GetSpan().ToArray();
            var hashKey = CreateKey(BlockByHashPrefix, hashBytes);

            // Check if block already exists
            if (_store.Contains(hashKey))
                return Task.FromResult(false);

            // Serialize block data
            var blockData = SerializeBlock(block);

            // Store by hash
            _store.Put(hashKey, blockData);

            // Store index -> hash mapping
            var indexKey = CreateKey(BlockByIndexPrefix, BitConverter.GetBytes(block.Index));
            _store.Put(indexKey, hashBytes);

            // Update height if this is a new highest block
            var currentHeight = GetHeightInternal();
            if (block.Index > currentHeight || currentHeight == 0)
            {
                _store.Put(HeightKey, BitConverter.GetBytes(block.Index));
            }

            foreach (var tx in block.Transactions)
            {
                var txKey = CreateKey(TransactionByHashPrefix, tx.Hash.GetSpan().ToArray());
                if (_store.Contains(txKey))
                    continue;

                _store.Put(txKey, tx.ToArray());
            }

            return Task.FromResult(true);
        }

        /// <inheritdoc/>
        public Task<Block?> GetBlockByHashAsync(byte[] hash)
        {
            ArgumentNullException.ThrowIfNull(hash);

            var key = CreateKey(BlockByHashPrefix, hash);
            if (!_store.TryGet(key, out var data))
                return Task.FromResult<Block?>(null);

            var block = DeserializeBlock(data);
            return Task.FromResult<Block?>(block);
        }

        /// <inheritdoc/>
        public Task<Block?> GetBlockByIndexAsync(uint index)
        {
            var indexKey = CreateKey(BlockByIndexPrefix, BitConverter.GetBytes(index));
            if (!_store.TryGet(indexKey, out var hashBytes))
                return Task.FromResult<Block?>(null);

            return GetBlockByHashAsync(hashBytes);
        }

        /// <inheritdoc/>
        public Task<bool> ContainsBlockAsync(byte[] hash)
        {
            ArgumentNullException.ThrowIfNull(hash);

            var key = CreateKey(BlockByHashPrefix, hash);
            return Task.FromResult(_store.Contains(key));
        }

        /// <inheritdoc/>
        public Task<bool> ContainsTransactionAsync(byte[] hash)
        {
            ArgumentNullException.ThrowIfNull(hash);

            var key = CreateKey(TransactionByHashPrefix, hash);
            return Task.FromResult(_store.Contains(key));
        }

        public Task<ITransactionData?> GetTransactionAsync(byte[] hash)
        {
            ArgumentNullException.ThrowIfNull(hash);

            var key = CreateKey(TransactionByHashPrefix, hash);
            if (!_store.TryGet(key, out var data))
                return Task.FromResult<ITransactionData?>(null);

            var reader = new MemoryReader(data);
            var transaction = reader.ReadSerializable<Transaction>();
            return Task.FromResult<ITransactionData?>(transaction);
        }

        /// <inheritdoc/>
        public Task<uint> GetHeightAsync()
        {
            return Task.FromResult(GetHeightInternal());
        }

        private uint GetHeightInternal()
        {
            if (!_store.TryGet(HeightKey, out var data))
                return 0;

            return BitConverter.ToUInt32(data);
        }

        private static byte[] CreateKey(byte[] prefix, byte[] key)
        {
            var result = new byte[prefix.Length + key.Length];
            prefix.CopyTo(result, 0);
            key.CopyTo(result, prefix.Length);
            return result;
        }

        private byte[] SerializeBlock(Block block)
        {
            if (BlockSerializer != null)
                return BlockSerializer(block);

            return block.ToArray();
        }

        private Block DeserializeBlock(byte[] data)
        {
            if (BlockDeserializer != null)
                return BlockDeserializer(data);

            if (SerializationHelper.TryDeserializeBlock(data, out var fullBlock))
                return fullBlock;

            throw new FormatException("Stored block data is not a full block payload.");
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (_ownsStore)
            {
                _store.Dispose();
            }
        }
    }
}
