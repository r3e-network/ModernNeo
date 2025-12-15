// Copyright (C) 2015-2025 The Neo Project.
//
// IVerifiableBase.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.IO;
using System.IO;

namespace Neo.Core.Interfaces
{
    /// <summary>
    /// Base interface for verifiable objects without persistence dependencies.
    /// This interface contains only serialization-related members, allowing
    /// protocol types to be defined without circular dependencies on DataCache.
    /// </summary>
    /// <remarks>
    /// The full IVerifiable interface (in Neo.Network.P2P.Payloads) extends this
    /// interface and adds the GetScriptHashesForVerifying method that requires DataCache,
    /// as well as the Witnesses property that uses the concrete Witness type.
    /// Verification logic is delegated to IVerificationService implementations.
    /// </remarks>
    public interface IVerifiableBase : ISerializable
    {
        /// <summary>
        /// The hash of the verifiable object.
        /// </summary>
        UInt256 Hash { get; }

        /// <summary>
        /// Deserializes the part of the object other than Witnesses.
        /// </summary>
        /// <param name="reader">The MemoryReader for reading data.</param>
        void DeserializeUnsigned(ref MemoryReader reader);

        /// <summary>
        /// Serializes the part of the object other than Witnesses.
        /// </summary>
        /// <param name="writer">The BinaryWriter for writing data.</param>
        void SerializeUnsigned(BinaryWriter writer);
    }
}
