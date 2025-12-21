// Copyright (C) 2015-2025 The Neo Project.
//
// IContractStateData.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using System;

namespace Neo.Core.Interfaces
{
    /// <summary>
    /// Defines the data contract for a deployed contract without VM or Manifest dependencies.
    /// This interface allows lower layers to work with contract state data without depending
    /// on the full ContractState implementation in Neo.SmartContract.
    /// </summary>
    public interface IContractStateData : IInteroperableBase
    {
        /// <summary>
        /// The unique identifier of the contract.
        /// </summary>
        int Id { get; }

        /// <summary>
        /// Indicates the number of times the contract has been updated.
        /// </summary>
        ushort UpdateCounter { get; }

        /// <summary>
        /// The hash of the contract (script hash).
        /// </summary>
        UInt160 Hash { get; }

        /// <summary>
        /// The compiled script of the contract.
        /// </summary>
        ReadOnlyMemory<byte> Script { get; }

        /// <summary>
        /// The name of the contract from its manifest.
        /// </summary>
        string Name { get; }
    }
}
