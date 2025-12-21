// Copyright (C) 2015-2025 The Neo Project.
//
// CryptoSignatureVerifier.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Core.Interfaces;
using System;
using System.Collections.Generic;

namespace Neo.Cryptography
{
    /// <summary>
    /// Default <see cref="ISignatureVerifier"/> implementation backed by <see cref="Crypto"/>.
    /// </summary>
    public sealed class CryptoSignatureVerifier : ISignatureVerifier
    {
        /// <summary>
        /// Gets a shared instance of <see cref="CryptoSignatureVerifier"/>.
        /// </summary>
        public static readonly CryptoSignatureVerifier Default = new();

        public bool Verify(ReadOnlySpan<byte> data, ReadOnlySpan<byte> signature, ReadOnlySpan<byte> publicKey)
        {
            return Crypto.VerifySignature(data, signature, publicKey, ECC.ECCurve.Secp256r1);
        }

        public bool VerifyMultiple(ReadOnlySpan<byte> data, IReadOnlyList<byte[]> signatures, IReadOnlyList<byte[]> publicKeys)
        {
            ArgumentNullException.ThrowIfNull(signatures);
            ArgumentNullException.ThrowIfNull(publicKeys);
            if (signatures.Count != publicKeys.Count) return false;

            for (var i = 0; i < signatures.Count; i++)
            {
                if (!Verify(data, signatures[i], publicKeys[i])) return false;
            }

            return true;
        }
    }
}

