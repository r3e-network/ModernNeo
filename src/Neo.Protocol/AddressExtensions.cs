// Copyright (C) 2015-2025 The Neo Project.
//
// AddressExtensions.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Cryptography;
using System;

namespace Neo.Wallets
{
    /// <summary>
    /// Address encoding helpers (Base58Check).
    /// </summary>
    public static class AddressExtensions
    {
        /// <summary>
        /// Converts the specified script hash to an address.
        /// </summary>
        public static string ToAddress(this UInt160 scriptHash, byte version)
        {
            Span<byte> data = stackalloc byte[21];
            data[0] = version;
            scriptHash.Serialize(data[1..]);
            return Base58.Base58CheckEncode(data);
        }

        /// <summary>
        /// Converts the specified address to a script hash.
        /// </summary>
        public static UInt160 ToScriptHash(this string address, byte version)
        {
            var data = address.Base58CheckDecode();
            if (data.Length != 21)
                throw new FormatException($"Invalid address format: expected 21 bytes after Base58Check decoding, but got {data.Length} bytes. The address may be corrupted or in an invalid format.");
            if (data[0] != version)
                throw new FormatException($"Invalid address version: expected version {version}, but got {data[0]}. The address may be for a different network.");
            return new UInt160(data.AsSpan(1));
        }
    }
}
