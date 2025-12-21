using Neo.Core.Interfaces;
using Neo.Extensions;
using Neo.IO;
using Neo.Orleans.Interfaces;
using Neo.Orleans.Services;
using Neo.Orleans.States;
using Orleans.Runtime;

namespace Neo.Orleans.Grains;

/// <summary>
/// Orleans Grain implementation for blockchain state management.
/// Replaces Akka.NET Blockchain Actor with header cache and inventory handling.
/// </summary>
public class BlockchainGrain : Grain, IBlockchainGrain
{
    private readonly IPersistentState<BlockchainState> _state;
    private readonly IBlockStorageService? _blockStorage;
    private readonly IGrainFactory _grainFactory;

    public BlockchainGrain(
        [PersistentState("blockchain", "BlockchainStore")]
        IPersistentState<BlockchainState> state,
        IGrainFactory grainFactory,
        IBlockStorageService? blockStorage = null)
    {
        _state = state;
        _grainFactory = grainFactory;
        _blockStorage = blockStorage;
    }

    public async Task<BlockVerifyResult> PersistBlockAsync(IBlockData block, string? senderAddress = null)
    {
        var hashHex = Convert.ToHexString(block.Hash.GetSpan());

        // Check if already exists
        if (block.Index <= _state.State.Height && _state.State.IsInitialized)
            return BlockVerifyResult.AlreadyExists;

        // Check if we can verify this block
        var headerHeight = _state.State.HeaderHeight > 0 ? _state.State.HeaderHeight : _state.State.Height;
        if (block.Index - 1 > headerHeight)
        {
            // Add to unverified blocks cache
            AddUnverifiedBlock(block, hashHex, senderAddress);
            await _state.WriteStateAsync();
            return BlockVerifyResult.UnableToVerify;
        }

        // Verify block matches header if we have it cached
        if (block.Index <= headerHeight && block.Index > _state.State.Height)
        {
            if (_state.State.HeaderCache.TryGetValue(block.Index, out var cachedHeader))
            {
                var blockHash = block.Hash.GetSpan().ToArray();
                if (!blockHash.SequenceEqual(cachedHeader.Hash))
                    return BlockVerifyResult.Invalid;
            }
        }

        // Add to block cache
        _state.State.BlockCache[hashHex] = block.ToArray();

        // Check if this is the next block to persist
        if (block.Index == _state.State.Height + 1 || !_state.State.IsInitialized)
        {
            // Persist this block and any subsequent cached blocks
            await PersistBlockChainAsync(block);
        }
        else if (block.Index == headerHeight + 1)
        {
            // Add header to cache
            AddHeaderFromBlock(block);
        }

        await _state.WriteStateAsync();
        return BlockVerifyResult.Succeed;
    }

    public Task<uint> GetHeightAsync() =>
        Task.FromResult(_state.State.Height);

    public Task<uint> GetHeaderHeightAsync() =>
        Task.FromResult(_state.State.HeaderHeight > 0 ? _state.State.HeaderHeight : _state.State.Height);

    public async Task<IBlockData?> GetBlockByHashAsync(byte[] hash)
    {
        if (_blockStorage == null)
            return null;

        return await _blockStorage.GetBlockByHashAsync(hash);
    }

    public async Task<IBlockData?> GetBlockByIndexAsync(uint index)
    {
        if (_blockStorage == null)
            return null;

        return await _blockStorage.GetBlockByIndexAsync(index);
    }

    public async Task<int> ImportBlocksAsync(IEnumerable<IBlockData> blocks, bool verify = true)
    {
        int count = 0;
        foreach (var block in blocks.OrderBy(b => b.Index))
        {
            var result = await PersistBlockAsync(block);
            if (result == BlockVerifyResult.Succeed)
                count++;
        }
        return count;
    }

    public Task<byte[]> GetCurrentBlockHashAsync() =>
        Task.FromResult(_state.State.CurrentBlockHash);

    public async Task<int> AddHeadersAsync(IEnumerable<HeaderCacheEntry> headers)
    {
        var added = 0;
        var currentHeaderHeight = _state.State.HeaderHeight > 0 ? _state.State.HeaderHeight : _state.State.Height;

        foreach (var header in headers.OrderBy(h => h.Index))
        {
            // Skip if already have this header or it's too far ahead
            if (header.Index <= currentHeaderHeight)
                continue;

            if (header.Index > currentHeaderHeight + 1)
                break;

            // Check cache capacity
            if (_state.State.HeaderCache.Count >= _state.State.MaxHeaderCacheSize)
                break;

            _state.State.HeaderCache[header.Index] = header;
            _state.State.HeaderHeight = header.Index;
            currentHeaderHeight = header.Index;
            added++;
        }

        if (added > 0)
            await _state.WriteStateAsync();

        return added;
    }

    public Task<HeaderCacheEntry?> GetHeaderAsync(uint index)
    {
        _state.State.HeaderCache.TryGetValue(index, out var header);
        return Task.FromResult(header);
    }

    public async Task<bool> ContainsTransactionAsync(byte[] hash)
    {
        if (_blockStorage == null)
            return false;

        return await _blockStorage.ContainsTransactionAsync(hash);
    }

    public Task<bool> ContainsBlockAsync(byte[] hash)
    {
        var hashHex = Convert.ToHexString(hash);

        // Check block cache first
        if (_state.State.BlockCache.ContainsKey(hashHex))
            return Task.FromResult(true);

        // Check header cache
        foreach (var header in _state.State.HeaderCache.Values)
        {
            if (header.Hash.SequenceEqual(hash))
                return Task.FromResult(true);
        }

        // Would need to check storage for persisted blocks
        return Task.FromResult(false);
    }

    public async Task FillMemoryPoolAsync(IEnumerable<byte[]> transactionHashes)
    {
        var memPool = _grainFactory.GetGrain<IMemoryPoolGrain>(0);

        // Invalidate all current transactions
        await memPool.InvalidateAllTransactionsAsync();

        // Note: In a full implementation, we would deserialize and re-add transactions
        // This requires access to transaction storage which is not yet implemented
    }

    public Task ReverifyInventoriesAsync(IEnumerable<byte[]> inventoryHashes)
    {
        // In a full implementation, this would re-verify blocks and transactions
        // For now, this is a placeholder
        return Task.CompletedTask;
    }

    public Task<BlockchainStateSummary> GetStateSummaryAsync()
    {
        var unverifiedCount = _state.State.UnverifiedBlocks.Values.Sum(list => list.Count);

        return Task.FromResult(new BlockchainStateSummary(
            _state.State.Height,
            _state.State.HeaderHeight > 0 ? _state.State.HeaderHeight : _state.State.Height,
            _state.State.CurrentBlockHash,
            _state.State.Timestamp,
            _state.State.IsInitialized,
            _state.State.HeaderCache.Count,
            unverifiedCount));
    }

    #region Private Methods

    private async Task PersistBlockChainAsync(IBlockData startBlock)
    {
        var currentBlock = startBlock;
        var headerHeight = _state.State.HeaderHeight > 0 ? _state.State.HeaderHeight : _state.State.Height;

        while (currentBlock != null)
        {
            // Store block in storage service if available
            if (_blockStorage != null)
            {
                await _blockStorage.StoreBlockAsync(currentBlock);
            }

            // Update state
            _state.State.Height = currentBlock.Index;
            _state.State.CurrentBlockHash = currentBlock.Hash.GetSpan().ToArray();
            _state.State.Timestamp = currentBlock.Timestamp;
            _state.State.IsInitialized = true;

            // Remove from caches
            var hashHex = Convert.ToHexString(currentBlock.Hash.GetSpan());
            _state.State.BlockCache.Remove(hashHex);
            _state.State.UnverifiedBlocks.Remove(currentBlock.Index);

            // Remove header from cache if present
            if (_state.State.HeaderCache.TryGetValue(currentBlock.Index, out _))
            {
                _state.State.HeaderCache.Remove(currentBlock.Index);
            }

            // Notify memory pool
            var memPool = _grainFactory.GetGrain<IMemoryPoolGrain>(0);
            var txHashes = GetTransactionHashes(currentBlock);
            await memPool.UpdateForBlockPersistedAsync(currentBlock.Index, txHashes);

            // Check for next block in cache
            if (currentBlock.Index + 1 > headerHeight)
                break;

            if (!_state.State.HeaderCache.TryGetValue(currentBlock.Index + 1, out var nextHeader))
                break;

            var nextHashHex = Convert.ToHexString(nextHeader.Hash);
            if (!_state.State.BlockCache.TryGetValue(nextHashHex, out var nextBlockData))
                break;

            // Deserialize next block - this would require IBlockData factory
            // For now, break the chain
            break;
        }

        // Process any unverified blocks that can now be verified
        await ProcessUnverifiedBlocksAsync();
    }

    private async Task ProcessUnverifiedBlocksAsync()
    {
        var nextIndex = _state.State.Height + 1;

        if (!_state.State.UnverifiedBlocks.TryGetValue(nextIndex, out var unverifiedList))
            return;

        // Process unverified blocks at the next index
        foreach (var entry in unverifiedList.ToList())
        {
            // Would need to deserialize and verify the block
            // This requires IBlockData factory which is not yet available
        }
    }

    private void AddUnverifiedBlock(IBlockData block, string hashHex, string? senderAddress)
    {
        if (!_state.State.UnverifiedBlocks.TryGetValue(block.Index, out var list))
        {
            list = new List<UnverifiedBlockEntry>();
            _state.State.UnverifiedBlocks[block.Index] = list;
        }

        // Check if this block hash already exists
        var existing = list.FirstOrDefault(e => e.HashHex == hashHex);
        if (existing != null)
        {
            if (senderAddress != null)
                existing.Senders.Add(senderAddress);
            return;
        }

        // Add new entry
        list.Add(new UnverifiedBlockEntry
        {
            HashHex = hashHex,
            Data = block.ToArray(),
            Senders = senderAddress != null ? new HashSet<string> { senderAddress } : new HashSet<string>()
        });
    }

    private void AddHeaderFromBlock(IBlockData block)
    {
        var header = new HeaderCacheEntry
        {
            Index = block.Index,
            Hash = block.Hash.GetSpan().ToArray(),
            PrevHash = block.PrevHash.GetSpan().ToArray(),
            Timestamp = block.Timestamp,
            Data = Array.Empty<byte>() // Would serialize header separately
        };

        _state.State.HeaderCache[block.Index] = header;
        _state.State.HeaderHeight = block.Index;
    }

    private static IEnumerable<byte[]> GetTransactionHashes(IBlockData block)
    {
        // In a full implementation, this would extract transaction hashes from the block
        // For now, return empty
        return Array.Empty<byte[]>();
    }

    #endregion
}
