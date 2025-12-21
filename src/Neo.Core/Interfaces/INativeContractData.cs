// Copyright (C) 2015-2025 The Neo Project.
//
// INativeContractData.cs file belongs to the neo project and is free
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
    /// Defines the data contract for a native contract without VM or execution dependencies.
    /// This interface allows lower layers to work with native contract metadata without depending
    /// on the full NativeContract implementation in Neo.SmartContract.Native.
    /// </summary>
    public interface INativeContractData
    {
        /// <summary>
        /// The unique identifier of the native contract.
        /// </summary>
        int Id { get; }

        /// <summary>
        /// The hash of the native contract.
        /// </summary>
        UInt160 Hash { get; }

        /// <summary>
        /// The name of the native contract.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// The hardfork at which the native contract was first active.
        /// Returns null if the contract was active from genesis.
        /// </summary>
        byte? ActiveIn { get; }
    }
}
