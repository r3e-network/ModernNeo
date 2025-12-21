// Copyright (C) 2015-2025 The Neo Project.
//
// ScriptHashExtensions.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using System;

namespace Neo.SmartContract
{
    /// <summary>
    /// Cryptography-backed script hash helpers.
    /// </summary>
    public static class ScriptHashExtensions
    {
        /// <summary>
        /// Computes the Hash160 of the specified script and returns it as a <see cref="UInt160"/>.
        /// </summary>
        /// <param name="script">The script bytes.</param>
        /// <returns>The script hash.</returns>
        public static UInt160 ToScriptHash(this byte[] script)
        {
            // Note: keep legacy behavior (null -> empty span) for binary compatibility.
            return new UInt160(Neo.Cryptography.Crypto.Hash160(script));
        }

        /// <summary>
        /// Computes the Hash160 of the specified script and returns it as a <see cref="UInt160"/>.
        /// </summary>
        /// <param name="script">The script bytes.</param>
        /// <returns>The script hash.</returns>
        public static UInt160 ToScriptHash(this ReadOnlySpan<byte> script)
        {
            return new UInt160(Neo.Cryptography.Crypto.Hash160(script));
        }
    }
}
