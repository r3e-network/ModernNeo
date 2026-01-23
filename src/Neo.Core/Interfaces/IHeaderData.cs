// Copyright (C) 2015-2025 The Neo Project.
//
// IHeaderData.cs file belongs to the neo project and is free
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
    /// Represents immutable block header data without concrete dependencies.
    /// </summary>
    public interface IHeaderData : IVerifiableBase
    {
        /// <summary>
        /// The index of the block.
        /// </summary>
        uint Index { get; }

        /// <summary>
        /// The timestamp of the block.
        /// </summary>
        ulong Timestamp { get; }

        /// <summary>
        /// The hash of the previous block.
        /// </summary>
        UInt256 PrevHash { get; }

        /// <summary>
        /// The merkle root of the transactions.
        /// </summary>
        UInt256 MerkleRoot { get; }

        /// <summary>
        /// The primary index of the consensus node that generated this block.
        /// </summary>
        byte PrimaryIndex { get; }

        /// <summary>
        /// The multi-signature address of the consensus nodes that generates the next block.
        /// </summary>
        UInt160 NextConsensus { get; }

        /// <summary>
        /// The witness of the block.
        /// </summary>
        IWitness? Witness { get; }
    }
}
