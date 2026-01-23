// Copyright (C) 2015-2025 The Neo Project.
//
// BlockchainState.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Core.Interfaces;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using System.Collections.Concurrent;

namespace Neo.Ledger
{
    /// <summary>
    /// In-memory blockchain state implementation with storage backend.
    /// Provides fast access to blockchain data with caching.
    /// </summary>
    public sealed class BlockchainState : IBlockchainState
    {
        private readonly IStore? _store;
        private readonly ConcurrentDictionary<string, Block> _blockCache = new();
        private readonly ConcurrentDictionary<string, Header> _headerCache = new();
        private readonly ConcurrentDictionary<string, ITransactionData> _txCache = new();
        private readonly ConcurrentDictionary<uint, byte[]> _indexToHash = new();
        private readonly ReaderWriterLockSlim _lock = new();

        private uint _height;
        private byte[] _currentBlockHash = Array.Empty<byte>();
        private byte[] _genesisBlockHash = Array.Empty<byte>();
        private bool _isSynchronized;

        /// <inheritdoc/>
        public uint Height
        {
            get
            {
                _lock.EnterReadLock();
                try { return _height; }
                finally { _lock.ExitReadLock(); }
            }
        }

        /// <inheritdoc/>
        public byte[] CurrentBlockHash
        {
            get
            {
                _lock.EnterReadLock();
                try { return _currentBlockHash; }
                finally { _lock.ExitReadLock(); }
            }
        }

        /// <inheritdoc/>
        public byte[] GenesisBlockHash
        {
            get
            {
                _lock.EnterReadLock();
                try { return _genesisBlockHash; }
                finally { _lock.ExitReadLock(); }
            }
        }

        /// <inheritdoc/>
        public bool IsSynchronized
        {
            get
            {
                _lock.EnterReadLock();
                try { return _isSynchronized; }
                finally { _lock.ExitReadLock(); }
            }
        }

        /// <summary>
        /// Creates a new blockchain state.
        /// </summary>
        /// <param name="store">Optional storage backend.</param>
        public BlockchainState(IStore? store = null)
        {
            _store = store;
        }

        /// <summary>
        /// Initializes the state with genesis block.
        /// </summary>
        public void Initialize(Block genesisBlock)
        {
            ArgumentNullException.ThrowIfNull(genesisBlock);

            _lock.EnterWriteLock();
            try
            {
                var hash = GetBlockHashBytes(genesisBlock);
                _genesisBlockHash = hash;
                _currentBlockHash = hash;
                _height = 0;

                var hashKey = Convert.ToHexString(hash);
                _blockCache[hashKey] = genesisBlock;
                _indexToHash[0] = hash;

                foreach (var tx in genesisBlock.Transactions)
                {
                    _txCache[Convert.ToHexString(tx.Hash.GetSpan())] = tx;
                }
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Updates state after a block is persisted.
        /// </summary>
        public void OnBlockPersisted(Block block)
        {
            ArgumentNullException.ThrowIfNull(block);

            _lock.EnterWriteLock();
            try
            {
                var hash = GetBlockHashBytes(block);
                var hashKey = Convert.ToHexString(hash);

                _blockCache[hashKey] = block;
                _indexToHash[block.Index] = hash;
                _currentBlockHash = hash;
                _height = block.Index;

                foreach (var tx in block.Transactions)
                {
                    _txCache[Convert.ToHexString(tx.Hash.GetSpan())] = tx;
                }
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Sets the synchronization status.
        /// </summary>
        public void SetSynchronized(bool synchronized)
        {
            _lock.EnterWriteLock();
            try { _isSynchronized = synchronized; }
            finally { _lock.ExitWriteLock(); }
        }

        /// <inheritdoc/>
        public Task<Header?> GetHeaderAsync(byte[] hash)
        {
            ArgumentNullException.ThrowIfNull(hash);
            var hashKey = Convert.ToHexString(hash);

            if (_headerCache.TryGetValue(hashKey, out var header))
                return Task.FromResult<Header?>(header);

            // Try to get from block cache
            if (_blockCache.TryGetValue(hashKey, out var block))
                return Task.FromResult<Header?>(block.Header);

            return Task.FromResult<Header?>(null);
        }

        /// <inheritdoc/>
        public Task<Header?> GetHeaderByIndexAsync(uint index)
        {
            if (_indexToHash.TryGetValue(index, out var hash))
                return GetHeaderAsync(hash);

            return Task.FromResult<Header?>(null);
        }

        /// <inheritdoc/>
        public Task<Block?> GetBlockAsync(byte[] hash)
        {
            ArgumentNullException.ThrowIfNull(hash);
            var hashKey = Convert.ToHexString(hash);

            if (_blockCache.TryGetValue(hashKey, out var block))
                return Task.FromResult<Block?>(block);

            return Task.FromResult<Block?>(null);
        }

        /// <inheritdoc/>
        public Task<Block?> GetBlockByIndexAsync(uint index)
        {
            if (_indexToHash.TryGetValue(index, out var hash))
                return GetBlockAsync(hash);

            return Task.FromResult<Block?>(null);
        }

        /// <inheritdoc/>
        public Task<bool> ContainsBlockAsync(byte[] hash)
        {
            ArgumentNullException.ThrowIfNull(hash);
            var hashKey = Convert.ToHexString(hash);
            return Task.FromResult(_blockCache.ContainsKey(hashKey));
        }

        /// <inheritdoc/>
        public Task<ITransactionData?> GetTransactionAsync(byte[] hash)
        {
            ArgumentNullException.ThrowIfNull(hash);
            var hashKey = Convert.ToHexString(hash);

            if (_txCache.TryGetValue(hashKey, out var tx))
                return Task.FromResult<ITransactionData?>(tx);

            return Task.FromResult<ITransactionData?>(null);
        }

        /// <inheritdoc/>
        public Task<bool> ContainsTransactionAsync(byte[] hash)
        {
            ArgumentNullException.ThrowIfNull(hash);
            var hashKey = Convert.ToHexString(hash);
            return Task.FromResult(_txCache.ContainsKey(hashKey));
        }

        /// <summary>
        /// Adds a transaction to the cache.
        /// </summary>
        public void AddTransaction(ITransactionData transaction)
        {
            ArgumentNullException.ThrowIfNull(transaction);
            var hashKey = Convert.ToHexString(transaction.Hash.GetSpan());
            _txCache[hashKey] = transaction;
        }

        /// <summary>
        /// Gets statistics about the current state.
        /// </summary>
        public BlockchainStateStatistics GetStatistics()
        {
            return new BlockchainStateStatistics
            {
                Height = Height,
                CachedBlocks = _blockCache.Count,
                CachedHeaders = _headerCache.Count,
                CachedTransactions = _txCache.Count,
                IsSynchronized = IsSynchronized
            };
        }

        private static byte[] GetBlockHashBytes(Block block)
        {
            return block.Hash.GetSpan().ToArray();
        }
    }

    /// <summary>
    /// Statistics about blockchain state.
    /// </summary>
    public readonly struct BlockchainStateStatistics
    {
        public uint Height { get; init; }
        public int CachedBlocks { get; init; }
        public int CachedHeaders { get; init; }
        public int CachedTransactions { get; init; }
        public bool IsSynchronized { get; init; }
    }
}
