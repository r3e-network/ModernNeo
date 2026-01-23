// Copyright (C) 2015-2025 The Neo Project.
//
// IBlockchainGrain.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Core.Interfaces;
using Neo.Network.P2P.Payloads;
using Neo.Orleans.States;

namespace Neo.Orleans.Interfaces
{
    /// <summary>
    /// Orleans Grain interface for blockchain state management.
    /// Manages headers, blocks, and inventory state.
    /// </summary>
    public interface IBlockchainGrain : IGrainWithIntegerKey
    {
        /// <summary>
        /// Persists a block to the blockchain.
        /// Returns the verification result.
        /// </summary>
        Task<BlockVerifyResult> PersistBlockAsync(Block block, string? senderAddress = null);

        /// <summary>
        /// Gets the current blockchain height.
        /// </summary>
        Task<uint> GetHeightAsync();

        /// <summary>
        /// Gets the current header height (may be ahead of block height during sync).
        /// </summary>
        Task<uint> GetHeaderHeightAsync();

        /// <summary>
        /// Gets a block by its hash.
        /// </summary>
        Task<Block?> GetBlockByHashAsync(byte[] hash);

        /// <summary>
        /// Gets a block by its index.
        /// </summary>
        Task<Block?> GetBlockByIndexAsync(uint index);

        /// <summary>
        /// Gets a block hash by its index.
        /// </summary>
        Task<byte[]?> GetBlockHashByIndexAsync(uint index);

        /// <summary>
        /// Imports multiple blocks.
        /// </summary>
        Task<int> ImportBlocksAsync(IEnumerable<Block> blocks, bool verify = true);

        /// <summary>
        /// Gets the current block hash.
        /// </summary>
        Task<byte[]> GetCurrentBlockHashAsync();

        /// <summary>
        /// Adds headers to the header cache for faster sync.
        /// </summary>
        Task<int> AddHeadersAsync(IEnumerable<HeaderCacheEntry> headers);

        /// <summary>
        /// Gets a header from the cache by index.
        /// </summary>
        Task<HeaderCacheEntry?> GetHeaderAsync(uint index);

        /// <summary>
        /// Checks if a transaction exists in the ledger.
        /// </summary>
        Task<bool> ContainsTransactionAsync(byte[] hash);

        /// <summary>
        /// Checks if a block exists in the ledger or cache.
        /// </summary>
        Task<bool> ContainsBlockAsync(byte[] hash);

        /// <summary>
        /// Fills the memory pool with transactions (called by consensus).
        /// </summary>
        Task FillMemoryPoolAsync(IEnumerable<ITransactionData> transactions);

        /// <summary>
        /// Fills the memory pool from transaction hashes (legacy/test-only).
        /// </summary>
        Task FillMemoryPoolAsync(IEnumerable<byte[]> transactionHashes);

        /// <summary>
        /// Re-verifies inventories after a view change or recovery.
        /// </summary>
        Task ReverifyInventoriesAsync(IEnumerable<byte[]> inventoryHashes);

        /// <summary>
        /// Verifies and stores an extensible payload.
        /// </summary>
        Task<Neo.Ledger.VerifyResult> VerifyExtensiblePayloadAsync(ExtensiblePayload payload);

        /// <summary>
        /// Gets the blockchain state summary.
        /// </summary>
        Task<BlockchainStateSummary> GetStateSummaryAsync();
    }

    /// <summary>
    /// Result of block verification and persistence.
    /// </summary>
    public enum BlockVerifyResult
    {
        /// <summary>Block was verified and persisted successfully.</summary>
        Succeed,
        /// <summary>Block already exists in the ledger.</summary>
        AlreadyExists,
        /// <summary>Block is invalid (failed verification).</summary>
        Invalid,
        /// <summary>Block cannot be verified yet (missing predecessor).</summary>
        UnableToVerify,
        /// <summary>Block has conflicts with existing data.</summary>
        HasConflicts
    }

    /// <summary>
    /// Summary of blockchain state.
    /// </summary>
    [GenerateSerializer]
    public record BlockchainStateSummary(
        [property: Id(0)] uint Height,
        [property: Id(1)] uint HeaderHeight,
        [property: Id(2)] byte[] CurrentBlockHash,
        [property: Id(3)] ulong Timestamp,
        [property: Id(4)] bool IsInitialized,
        [property: Id(5)] int HeaderCacheCount,
        [property: Id(6)] int UnverifiedBlockCount);
}
