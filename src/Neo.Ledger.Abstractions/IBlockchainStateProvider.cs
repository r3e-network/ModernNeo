// Copyright (C) 2015-2025 The Neo Project.
//
// IBlockchainStateProvider.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Core.Abstractions;
using Neo.Core.Abstractions.Storage;
using System.Threading;
using System.Threading.Tasks;

namespace Neo.Ledger.Abstractions
{
    /// <summary>
    /// Provides access to blockchain state without exposing implementation details.
    /// This interface enables querying blockchain state from any layer without
    /// creating circular dependencies.
    /// </summary>
    public interface IBlockchainStateProvider
    {
        /// <summary>
        /// Gets the current block height.
        /// </summary>
        uint CurrentHeight { get; }

        /// <summary>
        /// Gets the current block hash.
        /// </summary>
        byte[] CurrentBlockHash { get; }

        /// <summary>
        /// Gets a read-only snapshot of the current state.
        /// </summary>
        /// <returns>A storage snapshot.</returns>
        IStorageSnapshot GetSnapshot();

        /// <summary>
        /// Gets a block by its hash.
        /// </summary>
        /// <param name="hash">The block hash.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The block data, or null if not found.</returns>
        Task<IBlockData?> GetBlockAsync(byte[] hash, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets a block by its index.
        /// </summary>
        /// <param name="index">The block index.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The block data, or null if not found.</returns>
        Task<IBlockData?> GetBlockAsync(uint index, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets a header by its hash.
        /// </summary>
        /// <param name="hash">The header hash.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The header data, or null if not found.</returns>
        Task<IHeaderData?> GetHeaderAsync(byte[] hash, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets a header by its index.
        /// </summary>
        /// <param name="index">The header index.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The header data, or null if not found.</returns>
        Task<IHeaderData?> GetHeaderAsync(uint index, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets a transaction by its hash.
        /// </summary>
        /// <param name="hash">The transaction hash.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The transaction data, or null if not found.</returns>
        Task<ITransactionData?> GetTransactionAsync(byte[] hash, CancellationToken cancellationToken = default);

        /// <summary>
        /// Checks if a block exists.
        /// </summary>
        /// <param name="hash">The block hash.</param>
        /// <returns>True if the block exists.</returns>
        bool ContainsBlock(byte[] hash);

        /// <summary>
        /// Checks if a transaction exists.
        /// </summary>
        /// <param name="hash">The transaction hash.</param>
        /// <returns>True if the transaction exists.</returns>
        bool ContainsTransaction(byte[] hash);
    }
}
