// Copyright (C) 2015-2025 The Neo Project.
//
// IBlockPersistenceStrategy.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Core.Abstractions;
using Neo.Core.Abstractions.Blockchain;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Neo.Ledger.Abstractions
{
    /// <summary>
    /// Defines a strategy for persisting blocks to storage.
    /// </summary>
    public interface IBlockPersistenceStrategy
    {
        /// <summary>
        /// Event raised when a block is persisted.
        /// </summary>
        event EventHandler<BlockPersistedEventArgs>? BlockPersisted;

        /// <summary>
        /// Persists a block to storage.
        /// </summary>
        /// <param name="block">The block to persist.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The verification result.</returns>
        Task<VerifyResult> PersistAsync(IBlockData block, CancellationToken cancellationToken = default);

        /// <summary>
        /// Verifies a block before persistence.
        /// </summary>
        /// <param name="block">The block to verify.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The verification result.</returns>
        Task<VerifyResult> VerifyAsync(IBlockData block, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Event arguments for block persistence events.
    /// </summary>
    public class BlockPersistedEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the persisted block.
        /// </summary>
        public IBlockData Block { get; }

        /// <summary>
        /// Gets the block index.
        /// </summary>
        public uint Index => Block.Index;

        /// <summary>
        /// Creates a new instance of BlockPersistedEventArgs.
        /// </summary>
        /// <param name="block">The persisted block.</param>
        public BlockPersistedEventArgs(IBlockData block)
        {
            Block = block ?? throw new ArgumentNullException(nameof(block));
        }
    }
}
