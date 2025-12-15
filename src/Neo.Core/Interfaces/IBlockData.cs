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

namespace Neo.Core.Interfaces
{
    /// <summary>
    /// Defines the data contract for a block without any external dependencies.
    /// This interface allows lower layers to work with block data without depending
    /// on the full Block implementation in Neo.Network.P2P.Payloads.
    /// </summary>
    public interface IBlockData : IVerifiableBase
    {
        /// <summary>
        /// The version of the block (delegated from header).
        /// </summary>
        uint Version { get; }

        /// <summary>
        /// The hash of the previous block (delegated from header).
        /// </summary>
        UInt256 PrevHash { get; }

        /// <summary>
        /// The merkle root of the transactions (delegated from header).
        /// </summary>
        UInt256 MerkleRoot { get; }

        /// <summary>
        /// The timestamp of the block (delegated from header).
        /// </summary>
        ulong Timestamp { get; }

        /// <summary>
        /// The random nonce of the block (delegated from header).
        /// </summary>
        ulong Nonce { get; }

        /// <summary>
        /// The index (height) of the block (delegated from header).
        /// </summary>
        uint Index { get; }

        /// <summary>
        /// The primary index of the consensus node that generated this block (delegated from header).
        /// </summary>
        byte PrimaryIndex { get; }

        /// <summary>
        /// The multi-signature address of the consensus nodes that generates the next block (delegated from header).
        /// </summary>
        UInt160 NextConsensus { get; }

        /// <summary>
        /// The number of transactions in the block.
        /// </summary>
        int TransactionsCount { get; }
    }
}
