// Copyright (C) 2015-2025 The Neo Project.
//
// IWitnessConditionContext.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Cryptography.ECC;
using Neo.SmartContract;

namespace Neo.Network.P2P.Payloads.Conditions
{
    /// <summary>
    /// Abstraction for evaluating witness conditions without requiring a direct dependency
    /// on the smart contract execution engine implementation.
    /// </summary>
    public interface IWitnessConditionContext
    {
        /// <summary>
        /// The script hash of the currently executing contract.
        /// </summary>
        UInt160? CurrentScriptHash { get; }

        /// <summary>
        /// The script hash of the calling contract.
        /// </summary>
        UInt160? CallingScriptHash { get; }

        /// <summary>
        /// Indicates whether the current execution context is the entry script or is called directly by it.
        /// </summary>
        bool IsCalledByEntry { get; }

        /// <summary>
        /// Validates the current call flags against the required flags.
        /// </summary>
        /// <param name="requiredCallFlags">The required flags.</param>
        void ValidateCallFlags(CallFlags requiredCallFlags);

        /// <summary>
        /// Checks whether the specified contract belongs to the specified group.
        /// </summary>
        /// <param name="contractHash">The contract hash to check.</param>
        /// <param name="group">The group public key.</param>
        /// <returns><see langword="true"/> if the contract is in the group; otherwise, <see langword="false"/>.</returns>
        bool ContractHasGroup(UInt160 contractHash, ECPoint group);
    }
}

