// Copyright (C) 2015-2025 The Neo Project.
//
// InMemoryBlockStorageService.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Core.Interfaces;
using Neo.Network.P2P.Payloads;
using System.Collections.Concurrent;

namespace Neo.Orleans.Services
{
    /// <summary>
    /// In-memory implementation of IBlockStorageService for testing and development.
    /// Thread-safe using ConcurrentDictionary.
    /// </summary>
    public class InMemoryBlockStorageService : IBlockStorageService
    {
        private readonly ConcurrentDictionary<string, Block> _blocksByHash = new();
        private readonly ConcurrentDictionary<uint, Block> _blocksByIndex = new();
        private readonly ConcurrentDictionary<string, ITransactionData> _transactionsByHash = new();
        private uint _height;

        public Task<bool> StoreBlockAsync(Block block)
        {
            var hashKey = Convert.ToBase64String(block.Hash.GetSpan().ToArray());

            if (_blocksByHash.TryAdd(hashKey, block))
            {
                _blocksByIndex[block.Index] = block;

                foreach (var tx in block.Transactions)
                {
                    var txKey = Convert.ToBase64String(tx.Hash.GetSpan().ToArray());
                    _transactionsByHash.TryAdd(txKey, tx);
                }

                // Update height if this is a new highest block
                if (block.Index > _height || _height == 0)
                {
                    _height = block.Index;
                }

                return Task.FromResult(true);
            }

            return Task.FromResult(false);
        }

        public Task<Block?> GetBlockByHashAsync(byte[] hash)
        {
            var hashKey = Convert.ToBase64String(hash);
            _blocksByHash.TryGetValue(hashKey, out var block);
            return Task.FromResult(block);
        }

        public Task<Block?> GetBlockByIndexAsync(uint index)
        {
            _blocksByIndex.TryGetValue(index, out var block);
            return Task.FromResult(block);
        }

        public Task<bool> ContainsBlockAsync(byte[] hash)
        {
            var hashKey = Convert.ToBase64String(hash);
            return Task.FromResult(_blocksByHash.ContainsKey(hashKey));
        }

        public Task<bool> ContainsTransactionAsync(byte[] hash)
        {
            var hashKey = Convert.ToBase64String(hash);
            return Task.FromResult(_transactionsByHash.ContainsKey(hashKey));
        }

        public Task<ITransactionData?> GetTransactionAsync(byte[] hash)
        {
            var hashKey = Convert.ToBase64String(hash);
            _transactionsByHash.TryGetValue(hashKey, out var transaction);
            return Task.FromResult(transaction);
        }

        public Task<uint> GetHeightAsync()
        {
            return Task.FromResult(_height);
        }
    }
}
