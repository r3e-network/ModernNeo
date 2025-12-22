// Copyright (C) 2015-2025 The Neo Project.
//
// IBlockchainQuery.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using System.Threading;
using System.Threading.Tasks;

namespace Neo.Core.Abstractions.Blockchain
{
    /// <summary>
    /// Provides read-only access to blockchain state.
    /// This interface enables querying blockchain data without
    /// exposing implementation details or creating circular dependencies.
    /// </summary>
    public interface IBlockchainQuery
    {
        /// <summary>
        /// Gets the current block height (index of the latest block).
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The current block height.</returns>
        Task<uint> GetHeightAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the current header height.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The current header height.</returns>
        Task<uint> GetHeaderHeightAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets a block by its hash.
        /// </summary>
        /// <param name="hash">The block hash.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The block data, or null if not found.</returns>
        Task<IBlockData?> GetBlockByHashAsync(byte[] hash, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets a block by its index.
        /// </summary>
        /// <param name="index">The block index.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The block data, or null if not found.</returns>
        Task<IBlockData?> GetBlockByIndexAsync(uint index, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets a block hash by its index.
        /// </summary>
        /// <param name="index">The block index.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The block hash, or null if not found.</returns>
        Task<byte[]?> GetBlockHashByIndexAsync(uint index, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets a header by its hash.
        /// </summary>
        /// <param name="hash">The header hash.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The header data, or null if not found.</returns>
        Task<IHeaderData?> GetHeaderByHashAsync(byte[] hash, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets a header by its index.
        /// </summary>
        /// <param name="index">The header index.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The header data, or null if not found.</returns>
        Task<IHeaderData?> GetHeaderByIndexAsync(uint index, CancellationToken cancellationToken = default);

        /// <summary>
        /// Checks if a block with the specified hash exists.
        /// </summary>
        /// <param name="hash">The block hash.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>True if the block exists, false otherwise.</returns>
        Task<bool> ContainsBlockAsync(byte[] hash, CancellationToken cancellationToken = default);

        /// <summary>
        /// Checks if a transaction with the specified hash exists.
        /// </summary>
        /// <param name="hash">The transaction hash.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>True if the transaction exists, false otherwise.</returns>
        Task<bool> ContainsTransactionAsync(byte[] hash, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets a transaction by its hash.
        /// </summary>
        /// <param name="hash">The transaction hash.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The transaction data, or null if not found.</returns>
        Task<ITransactionData?> GetTransactionAsync(byte[] hash, CancellationToken cancellationToken = default);
    }
}
