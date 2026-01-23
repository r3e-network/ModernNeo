// Copyright (C) 2015-2025 The Neo Project.
//
// IBlockchainState.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Core.Interfaces;
using Neo.Network.P2P.Payloads;

namespace Neo.Ledger
{
    /// <summary>
    /// Defines the interface for querying blockchain state.
    /// This is a dependency-free abstraction for blockchain state access.
    /// </summary>
    public interface IBlockchainState
    {
        /// <summary>
        /// Gets the current block height.
        /// </summary>
        uint Height { get; }

        /// <summary>
        /// Gets the hash of the current block.
        /// </summary>
        byte[] CurrentBlockHash { get; }

        /// <summary>
        /// Gets the hash of the genesis block.
        /// </summary>
        byte[] GenesisBlockHash { get; }

        /// <summary>
        /// Gets whether the blockchain is fully synchronized.
        /// </summary>
        bool IsSynchronized { get; }

        /// <summary>
        /// Gets the block header by hash.
        /// </summary>
        /// <param name="hash">The block hash (32 bytes).</param>
        /// <returns>The block header data, or null if not found.</returns>
        Task<Header?> GetHeaderAsync(byte[] hash);

        /// <summary>
        /// Gets the block header by index.
        /// </summary>
        /// <param name="index">The block index.</param>
        /// <returns>The block header data, or null if not found.</returns>
        Task<Header?> GetHeaderByIndexAsync(uint index);

        /// <summary>
        /// Gets the block by hash.
        /// </summary>
        /// <param name="hash">The block hash (32 bytes).</param>
        /// <returns>The block data, or null if not found.</returns>
        Task<Block?> GetBlockAsync(byte[] hash);

        /// <summary>
        /// Gets the block by index.
        /// </summary>
        /// <param name="index">The block index.</param>
        /// <returns>The block data, or null if not found.</returns>
        Task<Block?> GetBlockByIndexAsync(uint index);

        /// <summary>
        /// Checks if a block exists.
        /// </summary>
        /// <param name="hash">The block hash (32 bytes).</param>
        /// <returns>True if the block exists; otherwise, false.</returns>
        Task<bool> ContainsBlockAsync(byte[] hash);

        /// <summary>
        /// Gets the transaction by hash.
        /// </summary>
        /// <param name="hash">The transaction hash (32 bytes).</param>
        /// <returns>The transaction data, or null if not found.</returns>
        Task<ITransactionData?> GetTransactionAsync(byte[] hash);

        /// <summary>
        /// Checks if a transaction exists.
        /// </summary>
        /// <param name="hash">The transaction hash (32 bytes).</param>
        /// <returns>True if the transaction exists; otherwise, false.</returns>
        Task<bool> ContainsTransactionAsync(byte[] hash);
    }
}
