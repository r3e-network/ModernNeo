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

        /// <summary>
        /// Block serializer/deserializer delegate.
        /// </summary>
        public Func<IBlockData, byte[]>? BlockSerializer { get; set; }

        /// <summary>
        /// Block deserializer delegate.
        /// </summary>
        public Func<byte[], IBlockData>? BlockDeserializer { get; set; }

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
        public Task<bool> StoreBlockAsync(IBlockData block)
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

            return Task.FromResult(true);
        }

        /// <inheritdoc/>
        public Task<IBlockData?> GetBlockByHashAsync(byte[] hash)
        {
            ArgumentNullException.ThrowIfNull(hash);

            var key = CreateKey(BlockByHashPrefix, hash);
            if (!_store.TryGet(key, out var data))
                return Task.FromResult<IBlockData?>(null);

            var block = DeserializeBlock(data);
            return Task.FromResult<IBlockData?>(block);
        }

        /// <inheritdoc/>
        public Task<IBlockData?> GetBlockByIndexAsync(uint index)
        {
            var indexKey = CreateKey(BlockByIndexPrefix, BitConverter.GetBytes(index));
            if (!_store.TryGet(indexKey, out var hashBytes))
                return Task.FromResult<IBlockData?>(null);

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

            // Transaction storage would need a separate prefix
            // For now, return false as transactions are not tracked separately
            // This would be enhanced when transaction storage is implemented
            return Task.FromResult(false);
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

        private byte[] SerializeBlock(IBlockData block)
        {
            if (BlockSerializer != null)
                return BlockSerializer(block);

            // Default serialization: store essential fields
            // Format: [Index:4][Timestamp:8][Hash:32][PrevHash:32][MerkleRoot:32][Version:4][Nonce:8][PrimaryIndex:1][NextConsensus:20]
            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms);

            writer.Write(block.Index);
            writer.Write(block.Timestamp);
            writer.Write(block.Hash.GetSpan());
            writer.Write(block.PrevHash.GetSpan());
            writer.Write(block.MerkleRoot.GetSpan());
            writer.Write(block.Version);
            writer.Write(block.Nonce);
            writer.Write(block.PrimaryIndex);
            writer.Write(block.NextConsensus.GetSpan());
            writer.Write(block.TransactionsCount);

            return ms.ToArray();
        }

        private IBlockData DeserializeBlock(byte[] data)
        {
            if (BlockDeserializer != null)
                return BlockDeserializer(data);

            // Default deserialization
            using var ms = new MemoryStream(data);
            using var reader = new BinaryReader(ms);

            return new StoredBlockData
            {
                Index = reader.ReadUInt32(),
                Timestamp = reader.ReadUInt64(),
                Hash = new UInt256(reader.ReadBytes(32)),
                PrevHash = new UInt256(reader.ReadBytes(32)),
                MerkleRoot = new UInt256(reader.ReadBytes(32)),
                Version = reader.ReadUInt32(),
                Nonce = reader.ReadUInt64(),
                PrimaryIndex = reader.ReadByte(),
                NextConsensus = new UInt160(reader.ReadBytes(20)),
                TransactionsCount = reader.ReadInt32()
            };
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

    /// <summary>
    /// Internal block data representation for deserialization.
    /// </summary>
    internal class StoredBlockData : IBlockData
    {
        public required UInt256 Hash { get; init; }
        public required uint Version { get; init; }
        public required UInt256 PrevHash { get; init; }
        public required UInt256 MerkleRoot { get; init; }
        public required ulong Timestamp { get; init; }
        public required ulong Nonce { get; init; }
        public required uint Index { get; init; }
        public required byte PrimaryIndex { get; init; }
        public required UInt160 NextConsensus { get; init; }
        public required int TransactionsCount { get; init; }
        public int Size => 141; // Fixed size for header data

        public void Deserialize(ref Neo.IO.MemoryReader reader) { }
        public void DeserializeUnsigned(ref Neo.IO.MemoryReader reader) { }
        public void Serialize(BinaryWriter writer) { }
        public void SerializeUnsigned(BinaryWriter writer) { }
    }
}
