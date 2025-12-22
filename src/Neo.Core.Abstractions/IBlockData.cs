// Copyright (C) 2015-2025 The Neo Project.
//
// IBlockData.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using System.Collections.Generic;

namespace Neo.Core.Abstractions
{
    /// <summary>
    /// Represents immutable block data in the Neo blockchain.
    /// This interface provides read-only access to block properties
    /// without exposing implementation details.
    /// </summary>
    public interface IBlockData
    {
        /// <summary>
        /// Gets the block hash as a byte array.
        /// </summary>
        byte[] Hash { get; }

        /// <summary>
        /// Gets the block index (height) in the blockchain.
        /// </summary>
        uint Index { get; }

        /// <summary>
        /// Gets the timestamp when the block was created.
        /// </summary>
        ulong Timestamp { get; }

        /// <summary>
        /// Gets the hash of the previous block.
        /// </summary>
        byte[] PrevHash { get; }

        /// <summary>
        /// Gets the Merkle root of all transactions in the block.
        /// </summary>
        byte[] MerkleRoot { get; }

        /// <summary>
        /// Gets the index of the primary consensus node that proposed this block.
        /// </summary>
        byte PrimaryIndex { get; }

        /// <summary>
        /// Gets the next consensus node's script hash.
        /// </summary>
        byte[] NextConsensus { get; }

        /// <summary>
        /// Gets the number of transactions in this block.
        /// </summary>
        int TransactionCount { get; }

        /// <summary>
        /// Gets the transactions in this block.
        /// </summary>
        IReadOnlyList<ITransactionData> Transactions { get; }
    }
}
