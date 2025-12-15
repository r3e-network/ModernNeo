// Copyright (C) 2015-2025 The Neo Project.
//
// IWitness.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.IO;
using System;

namespace Neo.Core.Interfaces
{
    /// <summary>
    /// Represents a witness (signature) for a verifiable object.
    /// This interface abstracts the Witness class to break circular dependencies.
    /// </summary>
    public interface IWitness : ISerializable
    {
        /// <summary>
        /// The invocation script containing arguments for the verification script.
        /// </summary>
        ReadOnlyMemory<byte> InvocationScript { get; }

        /// <summary>
        /// The verification script. Can be empty if the contract is deployed.
        /// </summary>
        ReadOnlyMemory<byte> VerificationScript { get; }

        /// <summary>
        /// The hash of the verification script.
        /// </summary>
        UInt160 ScriptHash { get; }
    }
}
