// Copyright (C) 2015-2025 The Neo Project.
//
// IVerifiable.cs file belongs to the neo project and is free
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
    /// Represents an object that can be cryptographically verified.
    /// </summary>
    public interface IVerifiable
    {
        /// <summary>
        /// Gets the witnesses used to verify this object.
        /// </summary>
        IReadOnlyList<IWitnessData> Witnesses { get; }

        /// <summary>
        /// Gets the hash of the data to be signed.
        /// </summary>
        /// <returns>The hash as a byte array.</returns>
        byte[] GetSignData();

        /// <summary>
        /// Gets the script hashes that need to be verified.
        /// </summary>
        /// <returns>The script hashes as byte arrays.</returns>
        IReadOnlyList<byte[]> GetScriptHashesForVerifying();
    }
}
