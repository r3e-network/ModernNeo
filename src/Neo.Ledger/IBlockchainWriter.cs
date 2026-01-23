// Copyright (C) 2015-2025 The Neo Project.
//
// IBlockchainWriter.cs file belongs to the neo project and is free
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
    /// Defines the interface for blockchain write operations.
    /// This is a dependency-free abstraction for blockchain modifications.
    /// </summary>
    public interface IBlockchainWriter
    {
        /// <summary>
        /// Persists a block to the blockchain.
        /// </summary>
        /// <param name="block">The block to persist.</param>
        /// <returns>True if the block was persisted; false if rejected.</returns>
        Task<bool> PersistBlockAsync(Block block);

        /// <summary>
        /// Imports multiple blocks.
        /// </summary>
        /// <param name="blocks">The blocks to import.</param>
        /// <param name="verify">Whether to verify blocks during import.</param>
        /// <returns>The number of blocks successfully imported.</returns>
        Task<int> ImportBlocksAsync(IEnumerable<Block> blocks, bool verify = true);

        /// <summary>
        /// Verifies a block without persisting it.
        /// </summary>
        /// <param name="block">The block to verify.</param>
        /// <returns>The verification result.</returns>
        Task<VerifyResult> VerifyBlockAsync(Block block);

        /// <summary>
        /// Verifies a transaction.
        /// </summary>
        /// <param name="transaction">The transaction to verify.</param>
        /// <returns>The verification result.</returns>
        Task<VerifyResult> VerifyTransactionAsync(ITransactionData transaction);
    }

    /// <summary>
    /// Combines read and write blockchain operations.
    /// </summary>
    public interface IBlockchainService : IBlockchainState, IBlockchainWriter
    {
        /// <summary>
        /// Raised when a block is persisted.
        /// </summary>
        event EventHandler<BlockPersistedEventArgs>? BlockPersisted;

        /// <summary>
        /// Raised when the blockchain is synchronized.
        /// </summary>
        event EventHandler? Synchronized;
    }

    /// <summary>
    /// Event arguments for block persistence.
    /// </summary>
    public class BlockPersistedEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the persisted block.
        /// </summary>
        public Block Block { get; }

        /// <summary>
        /// Gets the block index.
        /// </summary>
        public uint Index { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="BlockPersistedEventArgs"/> class.
        /// </summary>
        public BlockPersistedEventArgs(Block block, uint index)
        {
            Block = block;
            Index = index;
        }
    }
}
