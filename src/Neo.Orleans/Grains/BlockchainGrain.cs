// Copyright (C) 2015-2025 The Neo Project.
//
// BlockchainGrain.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo;
using Neo.Core.Interfaces;
using Neo.Extensions;
using Neo.IO;
using Neo.Network.P2P;
using Neo.Network.P2P.Payloads;
using Neo.Orleans.Hosting;
using Neo.Orleans.Interfaces;
using Neo.Orleans.Services;
using Neo.Orleans.States;
using Neo.Persistence;
using Neo.Protocol;
using Neo.SmartContract;
using Neo.SmartContract.Native;
using Neo.VM;
using Orleans.Runtime;
using System.Collections.Immutable;

namespace Neo.Orleans.Grains
{
    /// <summary>
    /// Orleans Grain implementation for blockchain state management.
    /// Manages headers, blocks, and inventory state.
    /// </summary>
    public class BlockchainGrain : Grain, IBlockchainGrain
    {
        private static readonly Script OnPersistScript;
        private static readonly Script PostPersistScript;
        private const byte LedgerPrefixBlock = 5;
        private const byte LedgerPrefixBlockHash = 9;
        private const byte LedgerPrefixCurrentBlock = 12;

        private readonly IPersistentState<BlockchainState> _state;
        private readonly IBlockStorageService? _blockStorage;
        private readonly IGrainFactory _grainFactory;
        private readonly NeoSystem _system;
        private readonly NeoOrleansOptions _options;
        private ImmutableHashSet<UInt160>? _extensibleWitnessWhiteList;

        static BlockchainGrain()
        {
            using var onPersistBuilder = new ScriptBuilder();
            onPersistBuilder.EmitSysCall(ApplicationEngine.System_Contract_NativeOnPersist);
            OnPersistScript = new Script(onPersistBuilder.ToArray(), true);

            using var postPersistBuilder = new ScriptBuilder();
            postPersistBuilder.EmitSysCall(ApplicationEngine.System_Contract_NativePostPersist);
            PostPersistScript = new Script(postPersistBuilder.ToArray(), true);
        }

        public BlockchainGrain(
            [PersistentState("blockchain", "BlockchainStore")]
            IPersistentState<BlockchainState> state,
            IGrainFactory grainFactory,
            NeoSystem system,
            NeoOrleansOptions? options = null,
            IBlockStorageService? blockStorage = null)
        {
            _state = state;
            _grainFactory = grainFactory;
            _blockStorage = blockStorage;
            _system = system;
            _options = options ?? new NeoOrleansOptions();
        }

        public override async Task OnActivateAsync(CancellationToken cancellationToken)
        {
            if (!IsLedgerInitialized() && !_state.State.IsInitialized)
                await EnsureGenesisPersistedAsync();

            await base.OnActivateAsync(cancellationToken);
        }

        public Task<BlockVerifyResult> PersistBlockAsync(Block block, string? senderAddress = null)
        {
            if (_options.ValidationMode == NeoValidationMode.None)
                return PersistBlockLegacyAsync(block, senderAddress);

            return PersistBlockValidatedAsync(block, senderAddress);
        }

        private async Task<BlockVerifyResult> PersistBlockLegacyAsync(Block block, string? senderAddress)
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
                    {
                        var taskManager = _grainFactory.GetGrain<ITaskManagerGrain>(0);
                        await taskManager.NotifyInvalidBlockAsync(blockHash, block.Index);
                        return BlockVerifyResult.Invalid;
                    }
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

        private async Task<BlockVerifyResult> PersistBlockValidatedAsync(Block block, string? senderAddress)
        {
            if (block is not Block fullBlock)
                return BlockVerifyResult.Invalid;

            var blockHash = fullBlock.Hash;

            await EnsureGenesisPersistedAsync();

            var snapshot = _system.StoreView;
            var currentHeight = NativeContract.Ledger.CurrentIndex(snapshot);
            var headerHeight = _system.HeaderCache.Last?.Index ?? currentHeight;

            if (fullBlock.Index <= currentHeight)
                return BlockVerifyResult.AlreadyExists;

            var hashHex = Convert.ToHexString(blockHash.GetSpan());
            if (fullBlock.Index - 1 > headerHeight)
            {
                AddUnverifiedBlock(fullBlock, hashHex, senderAddress);
                await _state.WriteStateAsync();
                return BlockVerifyResult.UnableToVerify;
            }

            if (fullBlock.Index == headerHeight + 1)
            {
                if (!fullBlock.Verify(_system.Settings, snapshot, _system.HeaderCache))
                {
                    var taskManager = _grainFactory.GetGrain<ITaskManagerGrain>(0);
                    await taskManager.NotifyInvalidBlockAsync(blockHash.GetSpan().ToArray(), fullBlock.Index);
                    return BlockVerifyResult.Invalid;
                }
            }
            else
            {
                var header = _system.HeaderCache[fullBlock.Index];
                if (header == null || !blockHash.Equals(header.Hash))
                {
                    var taskManager = _grainFactory.GetGrain<ITaskManagerGrain>(0);
                    await taskManager.NotifyInvalidBlockAsync(blockHash.GetSpan().ToArray(), fullBlock.Index);
                    return BlockVerifyResult.Invalid;
                }
            }

            _state.State.BlockCache[hashHex] = fullBlock.ToArray();

            if (fullBlock.Index == currentHeight + 1)
            {
                var persistResult = await PersistBlockChainValidatedAsync(fullBlock);
                if (persistResult != BlockVerifyResult.Succeed)
                    return persistResult;
            }
            else if (fullBlock.Index == headerHeight + 1)
            {
                if (_system.HeaderCache.Add(fullBlock.Header))
                    UpdateHeaderCacheState(fullBlock.Header);
            }

            if (fullBlock.Index != currentHeight + 1 && fullBlock.Index + 99 >= headerHeight)
            {
                var localNode = _grainFactory.GetGrain<ILocalNodeGrain>(0);
                _ = localNode.RelayBlockAsync(blockHash.GetSpan().ToArray(), fullBlock.Index);
            }

            await _state.WriteStateAsync();
            return BlockVerifyResult.Succeed;
        }

        public Task<uint> GetHeightAsync()
        {
            if (_options.ValidationMode == NeoValidationMode.None)
                return Task.FromResult(_state.State.Height);

            return Task.FromResult(GetCurrentHeight());
        }

        public Task<uint> GetHeaderHeightAsync()
        {
            if (_options.ValidationMode == NeoValidationMode.None)
                return Task.FromResult(_state.State.HeaderHeight > 0 ? _state.State.HeaderHeight : _state.State.Height);

            var height = GetCurrentHeight();
            var headerHeight = _system.HeaderCache.Last?.Index ?? height;
            return Task.FromResult(headerHeight);
        }

        public async Task<Block?> GetBlockByHashAsync(byte[] hash)
        {
            if (_options.ValidationMode != NeoValidationMode.None)
            {
                if (!IsLedgerInitialized())
                    return null;

                var block = NativeContract.Ledger.GetBlock(_system.StoreView, new UInt256(hash));
                if (block != null)
                    return block;
            }

            if (_blockStorage == null)
                return null;

            return await _blockStorage.GetBlockByHashAsync(hash);
        }

        public async Task<Block?> GetBlockByIndexAsync(uint index)
        {
            if (_options.ValidationMode != NeoValidationMode.None)
            {
                if (!IsLedgerInitialized())
                    return null;

                var block = NativeContract.Ledger.GetBlock(_system.StoreView, index);
                if (block != null)
                    return block;
            }

            if (_blockStorage == null)
                return null;

            return await _blockStorage.GetBlockByIndexAsync(index);
        }

        public async Task<byte[]?> GetBlockHashByIndexAsync(uint index)
        {
            if (_options.ValidationMode != NeoValidationMode.None)
            {
                if (!IsLedgerInitialized())
                    return null;

                var hash = NativeContract.Ledger.GetBlockHash(_system.StoreView, index);
                if (hash != null)
                    return hash.GetSpan().ToArray();
            }

            if (_state.State.IsInitialized && index == _state.State.Height)
                return _state.State.CurrentBlockHash;

            if (_state.State.HeaderCache.TryGetValue(index, out var header))
                return header.Hash;

            if (_blockStorage == null)
                return null;

            var block = await _blockStorage.GetBlockByIndexAsync(index);
            return block?.Hash.GetSpan().ToArray();
        }

        public async Task<int> ImportBlocksAsync(IEnumerable<Block> blocks, bool verify = true)
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

        public Task<byte[]> GetCurrentBlockHashAsync()
        {
            if (_options.ValidationMode == NeoValidationMode.None)
                return Task.FromResult(_state.State.CurrentBlockHash);

            if (!IsLedgerInitialized())
                return Task.FromResult(Array.Empty<byte>());

            var hash = NativeContract.Ledger.CurrentHash(_system.StoreView).GetSpan().ToArray();
            return Task.FromResult(hash);
        }

        public async Task<int> AddHeadersAsync(IEnumerable<HeaderCacheEntry> headers)
        {
            if (_options.ValidationMode == NeoValidationMode.None)
                return await AddHeadersLegacyAsync(headers);

            if (!IsLedgerInitialized())
                await EnsureGenesisPersistedAsync();

            var added = 0;
            var snapshot = _system.StoreView;
            var currentHeaderHeight = _system.HeaderCache.Last?.Index ?? GetCurrentHeight();

            foreach (var header in headers.OrderBy(h => h.Index))
            {
                if (header.Index <= currentHeaderHeight)
                    continue;

                if (header.Index > currentHeaderHeight + 1)
                    break;

                if (_system.HeaderCache.Full)
                    break;

                if (!TryDeserializeHeader(header.Data, out var parsedHeader))
                    break;

                if (!parsedHeader.Verify(_system.Settings, snapshot, _system.HeaderCache))
                    break;

                if (!_system.HeaderCache.Add(parsedHeader))
                    break;

                UpdateHeaderCacheState(parsedHeader);
                currentHeaderHeight = parsedHeader.Index;
                added++;
            }

            if (added > 0)
                await _state.WriteStateAsync();

            return added;
        }

        public Task<HeaderCacheEntry?> GetHeaderAsync(uint index)
        {
            if (_options.ValidationMode != NeoValidationMode.None)
            {
                var header = _system.HeaderCache[index];
                if (header == null)
                    return Task.FromResult<HeaderCacheEntry?>(null);

                var entry = new HeaderCacheEntry
                {
                    Index = header.Index,
                    Hash = header.Hash.GetSpan().ToArray(),
                    PrevHash = header.PrevHash.GetSpan().ToArray(),
                    Timestamp = header.Timestamp,
                    Data = header.ToArray()
                };
                return Task.FromResult<HeaderCacheEntry?>(entry);
            }

            _state.State.HeaderCache.TryGetValue(index, out var legacyHeader);
            return Task.FromResult(legacyHeader);
        }

        public async Task<bool> ContainsTransactionAsync(byte[] hash)
        {
            if (_options.ValidationMode != NeoValidationMode.None)
            {
                if (hash.Length == UInt256.Length && _system.MemPool.ContainsKey(new UInt256(hash)))
                    return true;

                if (!IsLedgerInitialized())
                    return false;

                return NativeContract.Ledger.ContainsTransaction(_system.StoreView, new UInt256(hash));
            }

            var memPool = _grainFactory.GetGrain<IMemoryPoolGrain>(0);
            if (await memPool.ContainsAsync(hash))
                return true;

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

            if (_options.ValidationMode != NeoValidationMode.None)
            {
                if (IsLedgerInitialized() && NativeContract.Ledger.ContainsBlock(_system.StoreView, new UInt256(hash)))
                    return Task.FromResult(true);
            }

            if (_blockStorage == null)
                return Task.FromResult(false);

            return _blockStorage.ContainsBlockAsync(hash);
        }

        public async Task FillMemoryPoolAsync(IEnumerable<ITransactionData> transactions)
        {
            ArgumentNullException.ThrowIfNull(transactions);

            if (_options.ValidationMode != NeoValidationMode.None)
            {
                if (!IsLedgerInitialized())
                    await EnsureGenesisPersistedAsync();

                _system.MemPool.InvalidateAllTransactions();
                var snapshot = _system.StoreView;
                var maxTraceableBlocks = _system.GetMaxTraceableBlocks();

                foreach (var transaction in transactions)
                {
                    if (transaction == null)
                        continue;

                    if (!TryDeserializeTransaction(transaction, out var tx))
                        continue;

                    if (NativeContract.Ledger.ContainsTransaction(snapshot, tx.Hash))
                        continue;

                    if (NativeContract.Ledger.ContainsConflictHash(snapshot, tx.Hash, tx.Signers.Select(s => s.Account), maxTraceableBlocks))
                        continue;

                    _system.MemPool.TryRemoveUnverified(tx.Hash);
                    _system.MemPool.TryAdd(tx, snapshot);
                }
                return;
            }

            var memPool = _grainFactory.GetGrain<IMemoryPoolGrain>(0);

            // Invalidate all current transactions
            await memPool.InvalidateAllTransactionsAsync();

            foreach (var transaction in transactions)
            {
                if (transaction == null)
                    continue;

                await memPool.AddTransactionAsync(transaction);
            }
        }

        public async Task FillMemoryPoolAsync(IEnumerable<byte[]> transactionHashes)
        {
            ArgumentNullException.ThrowIfNull(transactionHashes);

            var transactions = new List<ITransactionData>();
            foreach (var hash in transactionHashes)
            {
                if (hash.Length != UInt256.Length)
                    continue;

                Transaction? tx = null;
                if (_options.ValidationMode != NeoValidationMode.None)
                {
                    if (_system.MemPool.TryGetValue(new UInt256(hash), out var poolTx))
                    {
                        tx = poolTx;
                    }
                    else if (_blockStorage != null)
                    {
                        var storedTx = await _blockStorage.GetTransactionAsync(hash);
                        if (storedTx != null && TryDeserializeTransaction(storedTx, out var parsedTx))
                            tx = parsedTx;
                    }
                }
                else if (_blockStorage != null)
                {
                    var storedTx = await _blockStorage.GetTransactionAsync(hash);
                    if (storedTx != null)
                        tx = storedTx as Transaction ?? (TryDeserializeTransaction(storedTx, out var parsedTx) ? parsedTx : null);
                }

                if (tx != null)
                    transactions.Add(tx);
            }

            await FillMemoryPoolAsync(transactions);
        }

        public Task ReverifyInventoriesAsync(IEnumerable<byte[]> inventoryHashes)
        {
            ArgumentNullException.ThrowIfNull(inventoryHashes);
            return ReverifyInventoriesInternalAsync(inventoryHashes);
        }

        public Task<Neo.Ledger.VerifyResult> VerifyExtensiblePayloadAsync(ExtensiblePayload payload)
        {
            if (payload is null)
                return Task.FromResult(Neo.Ledger.VerifyResult.Invalid);

            if (!payload.TryGetHash(out _))
                return Task.FromResult(Neo.Ledger.VerifyResult.Invalid);

            var snapshot = _system.StoreView;
            _extensibleWitnessWhiteList ??= UpdateExtensibleWitnessWhiteList(_system.Settings, snapshot);

            if (!payload.Verify(_system.Settings, snapshot, _extensibleWitnessWhiteList))
                return Task.FromResult(Neo.Ledger.VerifyResult.Invalid);

            _system.RelayCache.Add(payload);
            return Task.FromResult(Neo.Ledger.VerifyResult.Succeed);
        }

        private async Task ReverifyInventoriesInternalAsync(IEnumerable<byte[]> inventoryHashes)
        {
            var memPool = _grainFactory.GetGrain<IMemoryPoolGrain>(0);
            var txRouter = _grainFactory.GetGrain<ITxRouterGrain>(0);

            foreach (var hash in inventoryHashes)
            {
                if (hash.Length == 0)
                    continue;

                if (!await memPool.ContainsAsync(hash))
                    continue;

                var tx = await memPool.GetTransactionAsync(hash) ??
                         (_blockStorage != null ? await _blockStorage.GetTransactionAsync(hash) : null);

                if (tx == null)
                    continue;

                var result = await txRouter.PreverifyAsync(tx, relay: false);
                if (!result.IsValid)
                {
                    await memPool.RemoveTransactionAsync(hash);
                    continue;
                }

                if (await memPool.TryRemoveUnverifiedAsync(hash))
                    await memPool.AddTransactionAsync(tx);
            }
        }

        public Task<BlockchainStateSummary> GetStateSummaryAsync()
        {
            var unverifiedCount = _state.State.UnverifiedBlocks.Values.Sum(list => list.Count);

            if (_options.ValidationMode != NeoValidationMode.None)
            {
                var height = GetCurrentHeight();
                var headerHeight = _system.HeaderCache.Last?.Index ?? height;
                var currentHash = IsLedgerInitialized()
                    ? NativeContract.Ledger.CurrentHash(_system.StoreView).GetSpan().ToArray()
                    : Array.Empty<byte>();
                return Task.FromResult(new BlockchainStateSummary(
                    height,
                    headerHeight,
                    currentHash,
                    _state.State.Timestamp,
                    IsLedgerInitialized(),
                    _system.HeaderCache.Count,
                    unverifiedCount));
            }

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

        private async Task<int> AddHeadersLegacyAsync(IEnumerable<HeaderCacheEntry> headers)
        {
            var added = 0;
            var currentHeaderHeight = _state.State.HeaderHeight > 0 ? _state.State.HeaderHeight : _state.State.Height;

            foreach (var header in headers.OrderBy(h => h.Index))
            {
                if (header.Index <= currentHeaderHeight)
                    continue;

                if (header.Index > currentHeaderHeight + 1)
                    break;

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

        private void UpdateHeaderCacheState(Header header)
        {
            var entry = new HeaderCacheEntry
            {
                Index = header.Index,
                Hash = header.Hash.GetSpan().ToArray(),
                PrevHash = header.PrevHash.GetSpan().ToArray(),
                Timestamp = header.Timestamp,
                Data = header.ToArray()
            };

            _state.State.HeaderCache[header.Index] = entry;
            _state.State.HeaderHeight = header.Index;
        }

        private void RemoveHeaderCacheState(uint index)
        {
            if (!_state.State.HeaderCache.Remove(index))
                return;

            if (_state.State.HeaderHeight == index)
                _state.State.HeaderHeight = _state.State.HeaderCache.Count > 0 ? _state.State.HeaderCache.Keys.Max() : 0;
        }

        private static bool TryDeserializeHeader(byte[] data, out Header header)
        {
            header = null!;
            if (data.Length == 0)
                return false;

            try
            {
                var reader = new MemoryReader(data);
                header = reader.ReadSerializable<Header>();
                return true;
            }
            catch (FormatException)
            {
                return false;
            }
            catch
            {
                return false;
            }
        }

        private bool IsLedgerInitialized() =>
            NativeContract.Ledger.Initialized(_system.StoreView);

        private UInt256 GetLegacyPrevHash()
        {
            if (_state.State.CurrentBlockHash.Length == UInt256.Length)
                return new UInt256(_state.State.CurrentBlockHash);

            if (IsLedgerInitialized())
                return NativeContract.Ledger.CurrentHash(_system.StoreView);

            return _system.GenesisBlock.Hash;
        }

        private uint GetCurrentHeight()
        {
            if (!IsLedgerInitialized())
                return 0;

            return NativeContract.Ledger.CurrentIndex(_system.StoreView);
        }

        private void UpdateLegacyLedgerState(Block block, UInt256 prevHash)
        {
            var txHashes = block.Transactions.Select(p => p.Hash).ToArray();

            var prevHeader = NativeContract.Ledger.GetHeader(_system.StoreView, prevHash);
            var nextConsensus = block.NextConsensus;
            if (nextConsensus == UInt160.Zero && prevHeader != null)
                nextConsensus = prevHeader.NextConsensus;

            var header = new Header
            {
                Version = block.Version,
                PrevHash = prevHash,
                MerkleRoot = block.MerkleRoot,
                Timestamp = block.Timestamp,
                Nonce = block.Nonce,
                Index = block.Index,
                PrimaryIndex = block.PrimaryIndex,
                NextConsensus = nextConsensus,
                Witness = new Witness
                {
                    InvocationScript = Array.Empty<byte>(),
                    VerificationScript = Array.Empty<byte>()
                }
            };

            var trimmed = TrimmedBlock.Create(header, txHashes);

            using var snapshot = _system.GetSnapshotCache();
            snapshot.Add(StorageKey.Create(NativeContract.Ledger.Id, LedgerPrefixBlockHash, block.Index), new StorageItem(block.Hash.ToArray()));
            snapshot.Add(StorageKey.Create(NativeContract.Ledger.Id, LedgerPrefixBlock, block.Hash), new StorageItem(trimmed.ToArray()));

            var state = snapshot.GetAndChange(StorageKey.Create(NativeContract.Ledger.Id, LedgerPrefixCurrentBlock), () => new StorageItem(new HashIndexState()))
                .GetInteroperable<HashIndexState>();
            state.Hash = block.Hash;
            state.Index = block.Index;
            snapshot.Commit();
        }

        private async Task EnsureGenesisPersistedAsync()
        {
            if (IsLedgerInitialized())
                return;

            if (_system.GenesisBlock is not Block genesis)
                return;

            await PersistBlockInternalAsync(genesis);
            _state.State.Height = genesis.Index;
            _state.State.CurrentBlockHash = genesis.Hash.GetSpan().ToArray();
            _state.State.Timestamp = genesis.Timestamp;
            _state.State.IsInitialized = true;
            await _state.WriteStateAsync();
        }

        private async Task<BlockVerifyResult> PersistBlockChainValidatedAsync(Block startBlock)
        {
            var headerHeight = _system.HeaderCache.Last?.Index ?? GetCurrentHeight();
            var blocksToPersist = new List<Block>();
            var blockPersist = startBlock;
            var taskManager = _grainFactory.GetGrain<ITaskManagerGrain>(0);

            while (blockPersist != null)
            {
                blocksToPersist.Add(blockPersist);

                if (blockPersist.Index + 1 > headerHeight)
                    break;

                var nextHeader = _system.HeaderCache[blockPersist.Index + 1];
                if (nextHeader == null)
                    break;

                var nextHashHex = Convert.ToHexString(nextHeader.Hash.GetSpan());
                if (!_state.State.BlockCache.TryGetValue(nextHashHex, out var nextBlockData))
                    break;

                if (!TryDeserializeBlock(nextBlockData, out var nextBlock))
                {
                    _state.State.BlockCache.Remove(nextHashHex);
                    break;
                }

                blockPersist = nextBlock;
            }

            var blocksPersisted = 0;
            var timePerBlock = _system.GetTimePerBlock();
            var extraRelayingBlocks = timePerBlock.TotalMilliseconds < ProtocolSettings.Default.MillisecondsPerBlock
                ? (ProtocolSettings.Default.MillisecondsPerBlock - (uint)timePerBlock.TotalMilliseconds) / 1000
                : 0;
            var localNode = _grainFactory.GetGrain<ILocalNodeGrain>(0);

            foreach (var block in blocksToPersist)
            {
                if (!VerifyBlockTransactions(block, _system.StoreView))
                {
                    var invalidHashHex = Convert.ToHexString(block.Hash.GetSpan());
                    _state.State.BlockCache.Remove(invalidHashHex);
                    _state.State.UnverifiedBlocks.Remove(block.Index);
                    if (block.Index == startBlock.Index)
                        await taskManager.NotifyInvalidBlockAsync(block.Hash.GetSpan().ToArray(), block.Index);
                    return block.Index == startBlock.Index
                        ? BlockVerifyResult.Invalid
                        : BlockVerifyResult.Succeed;
                }

                await PersistBlockInternalAsync(block);

                var hashHex = Convert.ToHexString(block.Hash.GetSpan());
                _state.State.BlockCache.Remove(hashHex);
                _state.State.UnverifiedBlocks.Remove(block.Index);

                if (_system.HeaderCache.TryRemoveFirst(out var header))
                    RemoveHeaderCacheState(header.Index);

                _state.State.Height = block.Index;
                _state.State.CurrentBlockHash = block.Hash.GetSpan().ToArray();
                _state.State.Timestamp = block.Timestamp;
                _state.State.IsInitialized = true;

                if (blocksPersisted++ >= blocksToPersist.Count - (2 + extraRelayingBlocks))
                {
                    if (block.Index + 99 >= headerHeight)
                        _ = localNode.RelayBlockAsync(block.Hash.GetSpan().ToArray(), block.Index);
                }

                await taskManager.NotifyPersistCompletedAsync(block.Hash.GetSpan().ToArray(), block.Index);
            }

            await ProcessUnverifiedBlocksAsync();
            return BlockVerifyResult.Succeed;
        }

        private bool VerifyBlockTransactions(Block block, DataCache snapshot)
        {
            if ((uint)block.Transactions.Length > _system.Settings.MaxTransactionsPerBlock)
                return false;

            var maxTraceableBlocks = snapshot.GetMaxTraceableBlocks(_system.Settings);
            var context = new Neo.Ledger.TransactionVerificationContext();
            var seen = new HashSet<UInt256>();

            foreach (var tx in block.Transactions)
            {
                if (!seen.Add(tx.Hash))
                    return false;

                if (NativeContract.Ledger.ContainsTransaction(snapshot, tx.Hash))
                    return false;

                if (NativeContract.Ledger.ContainsConflictHash(snapshot, tx.Hash, tx.Signers.Select(s => s.Account), maxTraceableBlocks))
                    return false;

                Neo.Ledger.VerifyResult result;
                try
                {
                    result = tx.Verify(_system.Settings, snapshot, context, Array.Empty<Transaction>());
                }
                catch
                {
                    return false;
                }

                if (result != Neo.Ledger.VerifyResult.Succeed)
                    return false;

                context.AddTransaction(tx);
            }

            return true;
        }

        private async Task PersistBlockInternalAsync(Block block)
        {
            using var snapshot = _system.GetSnapshotCache();
            var allApplicationExecuted = new List<Neo.Ledger.Blockchain.ApplicationExecuted>();
            TransactionState[] transactionStates;

            using (var engine = ApplicationEngine.Create(TriggerType.OnPersist, null, snapshot, block, _system.Settings, 0))
            {
                engine.LoadScript(OnPersistScript);
                if (engine.Execute() != VMState.HALT)
                {
                    if (engine.FaultException != null)
                        throw engine.FaultException;
                    throw new InvalidOperationException("OnPersist failed.");
                }

                var applicationExecuted = new Neo.Ledger.Blockchain.ApplicationExecuted(engine);
                allApplicationExecuted.Add(applicationExecuted);
                transactionStates = engine.GetState<TransactionState[]>()!;
            }

            var clonedSnapshot = snapshot.CloneCache();
            foreach (var transactionState in transactionStates)
            {
                var tx = transactionState.Transaction!;
                using var engine = ApplicationEngine.Create(TriggerType.Application, tx, clonedSnapshot, block, _system.Settings, tx.SystemFee);
                engine.LoadScript(tx.Script);
                transactionState.State = engine.Execute();
                if (transactionState.State == VMState.HALT)
                {
                    clonedSnapshot.Commit();
                }
                else
                {
                    clonedSnapshot = snapshot.CloneCache();
                }

                var applicationExecuted = new Neo.Ledger.Blockchain.ApplicationExecuted(engine);
                allApplicationExecuted.Add(applicationExecuted);
            }

            using (var engine = ApplicationEngine.Create(TriggerType.PostPersist, null, snapshot, block, _system.Settings, 0))
            {
                engine.LoadScript(PostPersistScript);
                if (engine.Execute() != VMState.HALT)
                {
                    if (engine.FaultException != null)
                        throw engine.FaultException;
                    throw new InvalidOperationException("PostPersist failed.");
                }

                var applicationExecuted = new Neo.Ledger.Blockchain.ApplicationExecuted(engine);
                allApplicationExecuted.Add(applicationExecuted);
            }

            Neo.Ledger.Blockchain.InvokeCommitting(_system, block, snapshot, allApplicationExecuted);
            snapshot.Commit();
            Neo.Ledger.Blockchain.InvokeCommitted(_system, block);

            _system.MemPool.UpdatePoolForBlockPersisted(block, _system.StoreView);
            _extensibleWitnessWhiteList = null;

            if (_blockStorage != null)
                await _blockStorage.StoreBlockAsync(block);
        }

        private async Task PersistBlockChainAsync(Block startBlock)
        {
            var currentBlock = startBlock;
            var headerHeight = _state.State.HeaderHeight > 0 ? _state.State.HeaderHeight : _state.State.Height;
            var taskManager = _grainFactory.GetGrain<ITaskManagerGrain>(0);

            while (currentBlock != null)
            {
                // Legacy mode bypasses native OnPersist, so keep ledger state in sync for consensus/tests.
                var prevHash = GetLegacyPrevHash();
                UpdateLegacyLedgerState(currentBlock, prevHash);

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
                _extensibleWitnessWhiteList = null;

                Neo.Ledger.Blockchain.InvokeCommitted(_system, currentBlock);

                await taskManager.NotifyPersistCompletedAsync(currentBlock.Hash.GetSpan().ToArray(), currentBlock.Index);

                // Check for next block in cache
                if (currentBlock.Index + 1 > headerHeight)
                    break;

                if (!_state.State.HeaderCache.TryGetValue(currentBlock.Index + 1, out var nextHeader))
                    break;

                var nextHashHex = Convert.ToHexString(nextHeader.Hash);
                if (!_state.State.BlockCache.TryGetValue(nextHashHex, out var nextBlockData))
                    break;

                if (!TryDeserializeBlock(nextBlockData, out var nextBlock))
                {
                    _state.State.BlockCache.Remove(nextHashHex);
                    break;
                }

                currentBlock = nextBlock;
            }

            // Process any unverified blocks that can now be verified
            await ProcessUnverifiedBlocksAsync();
        }

        private async Task ProcessUnverifiedBlocksAsync()
        {
            var nextIndex = _state.State.Height + 1;

            if (!_state.State.UnverifiedBlocks.TryGetValue(nextIndex, out var unverifiedList))
                return;

            var stateChanged = false;
            // Process unverified blocks at the next index
            foreach (var entry in unverifiedList.ToList())
            {
                if (!TryDeserializeBlock(entry.Data, out var block))
                {
                    unverifiedList.Remove(entry);
                    stateChanged = true;
                    continue;
                }

                var result = await PersistBlockAsync(block);
                if (result == BlockVerifyResult.Succeed || result == BlockVerifyResult.AlreadyExists)
                    return;
            }

            if (unverifiedList.Count == 0)
            {
                _state.State.UnverifiedBlocks.Remove(nextIndex);
                stateChanged = true;
            }

            if (stateChanged)
                await _state.WriteStateAsync();
        }

        private void AddUnverifiedBlock(Block block, string hashHex, string? senderAddress)
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

        private void AddHeaderFromBlock(Block block)
        {
            var headerData = block.Header.ToArray();

            var header = new HeaderCacheEntry
            {
                Index = block.Index,
                Hash = block.Hash.GetSpan().ToArray(),
                PrevHash = block.PrevHash.GetSpan().ToArray(),
                Timestamp = block.Timestamp,
                Data = headerData
            };

            _state.State.HeaderCache[block.Index] = header;
            _state.State.HeaderHeight = block.Index;
        }

        private static IEnumerable<byte[]> GetTransactionHashes(Block block)
        {
            if (block.Transactions.Length == 0)
                return Array.Empty<byte[]>();

            return block.Transactions
                .Select(tx => tx.Hash.GetSpan().ToArray())
                .ToArray();
        }

        private static bool TryDeserializeTransaction(ITransactionData transaction, out Transaction tx)
        {
            tx = null!;
            if (transaction is Transaction fullTransaction)
            {
                tx = fullTransaction;
                return true;
            }

            var raw = transaction.ToArray();
            if (raw.Length == 0)
                return false;

            try
            {
                var reader = new MemoryReader(raw);
                tx = reader.ReadSerializable<Transaction>();
                return true;
            }
            catch (FormatException)
            {
                return false;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryDeserializeBlock(byte[] data, out Block block)
        {
            block = null!;
            if (data.Length == 0)
                return false;

            try
            {
                var reader = new MemoryReader(data);
                block = reader.ReadSerializable<Block>();
                return true;
            }
            catch (FormatException)
            {
                return false;
            }
            catch
            {
                return false;
            }
        }

        private static ImmutableHashSet<UInt160> UpdateExtensibleWitnessWhiteList(ProtocolSettings settings, DataCache snapshot)
        {
            var currentHeight = NativeContract.Ledger.CurrentIndex(snapshot);
            var builder = ImmutableHashSet.CreateBuilder<UInt160>();
            builder.Add(NativeContract.NEO.GetCommitteeAddress(snapshot));

            var validators = NativeContract.NEO.GetNextBlockValidators(snapshot, settings.ValidatorsCount);
            builder.Add(Contract.GetBFTAddress(validators));
            builder.UnionWith(validators.Select(u => Contract.CreateSignatureRedeemScript(u).ToScriptHash()));

            var stateValidators = NativeContract.RoleManagement.GetDesignatedByRole(snapshot, Role.StateValidator, currentHeight);
            if (stateValidators.Length > 0)
            {
                builder.Add(Contract.GetBFTAddress(stateValidators));
                builder.UnionWith(stateValidators.Select(u => Contract.CreateSignatureRedeemScript(u).ToScriptHash()));
            }

            return builder.ToImmutable();
        }

        #endregion
    }
}
