// Copyright (C) 2015-2025 The Neo Project.
//
// IBlockchainMutator.cs file belongs to the neo project and is free
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
    /// Provides write operations for blockchain state.
    /// This interface enables modifying blockchain data without
    /// exposing implementation details or creating circular dependencies.
    /// </summary>
    public interface IBlockchainMutator
    {
        /// <summary>
        /// Persists a block to the blockchain.
        /// </summary>
        /// <param name="block">The block to persist.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>True if the block was persisted successfully, false otherwise.</returns>
        Task<bool> PersistBlockAsync(IBlockData block, CancellationToken cancellationToken = default);

        /// <summary>
        /// Adds a header to the header cache.
        /// </summary>
        /// <param name="header">The header to add.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>True if the header was added successfully, false otherwise.</returns>
        Task<bool> AddHeaderAsync(IHeaderData header, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Represents the result of a verification operation.
    /// </summary>
    public enum VerifyResult : byte
    {
        /// <summary>
        /// Verification succeeded.
        /// </summary>
        Succeed = 0,

        /// <summary>
        /// The item already exists in the pool.
        /// </summary>
        AlreadyInPool = 1,

        /// <summary>
        /// The item already exists on the blockchain.
        /// </summary>
        AlreadyOnChain = 2,

        /// <summary>
        /// The item is expired.
        /// </summary>
        Expired = 3,

        /// <summary>
        /// The item has insufficient funds.
        /// </summary>
        InsufficientFunds = 4,

        /// <summary>
        /// The item has an invalid attribute.
        /// </summary>
        InvalidAttribute = 5,

        /// <summary>
        /// The item has an invalid script.
        /// </summary>
        InvalidScript = 6,

        /// <summary>
        /// The item has an invalid signature.
        /// </summary>
        InvalidSignature = 7,

        /// <summary>
        /// The memory pool is full.
        /// </summary>
        MemoryPoolFull = 8,

        /// <summary>
        /// A policy check failed.
        /// </summary>
        PolicyFail = 9,

        /// <summary>
        /// An unknown error occurred.
        /// </summary>
        Unknown = 255
    }
}
