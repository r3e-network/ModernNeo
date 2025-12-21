// Copyright (C) 2015-2025 The Neo Project.
//
// InMemoryBlockStorageService.cs file belongs to the neo project and is free
// software distributed under the MIT software license.

using System.Collections.Concurrent;
using Neo.Core.Interfaces;

namespace Neo.Orleans.Services;

/// <summary>
/// In-memory implementation of IBlockStorageService for testing and development.
/// Thread-safe using ConcurrentDictionary.
/// </summary>
public class InMemoryBlockStorageService : IBlockStorageService
{
    private readonly ConcurrentDictionary<string, IBlockData> _blocksByHash = new();
    private readonly ConcurrentDictionary<uint, IBlockData> _blocksByIndex = new();
    private uint _height;

    public Task<bool> StoreBlockAsync(IBlockData block)
    {
        var hashKey = Convert.ToBase64String(block.Hash.GetSpan().ToArray());

        if (_blocksByHash.TryAdd(hashKey, block))
        {
            _blocksByIndex[block.Index] = block;

            // Update height if this is a new highest block
            if (block.Index > _height || _height == 0)
            {
                _height = block.Index;
            }

            return Task.FromResult(true);
        }

        return Task.FromResult(false);
    }

    public Task<IBlockData?> GetBlockByHashAsync(byte[] hash)
    {
        var hashKey = Convert.ToBase64String(hash);
        _blocksByHash.TryGetValue(hashKey, out var block);
        return Task.FromResult(block);
    }

    public Task<IBlockData?> GetBlockByIndexAsync(uint index)
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
        // In-memory implementation doesn't track transactions separately
        // This would need to be enhanced if transaction tracking is required
        return Task.FromResult(false);
    }

    public Task<uint> GetHeightAsync()
    {
        return Task.FromResult(_height);
    }
}
