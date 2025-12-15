// Copyright (C) 2015-2025 The Neo Project.
//
// Helper.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Extensions;
using Neo.Network.P2P.Payloads;
using Neo.Wallets;
using System;
using System.Linq;
using System.Security.Cryptography;
using ECPoint = Neo.Cryptography.ECC.ECPoint;

namespace Neo.Cryptography
{
    /// <summary>
    /// A helper class for cryptography operations that depend on Neo types.
    /// </summary>
    /// <remarks>
    /// Pure hash extension methods have been moved to <see cref="HashExtensions"/>.
    /// </remarks>
    public static class Helper
    {
        /// <summary>
        /// Derives a shared key using ECDH key exchange.
        /// </summary>
        public static byte[] ECDHDeriveKey(KeyPair local, ECPoint remote)
        {
            ReadOnlySpan<byte> pubkeyLocal = local.PublicKey.EncodePoint(false);
            ReadOnlySpan<byte> pubkeyRemote = remote.EncodePoint(false);
            using var ecdh1 = ECDiffieHellman.Create(new ECParameters
            {
                Curve = ECCurve.NamedCurves.nistP256,
                D = local.PrivateKey,
                Q = new System.Security.Cryptography.ECPoint
                {
                    X = pubkeyLocal[1..][..32].ToArray(),
                    Y = pubkeyLocal[1..][32..].ToArray()
                }
            });
            using var ecdh2 = ECDiffieHellman.Create(new ECParameters
            {
                Curve = ECCurve.NamedCurves.nistP256,
                Q = new System.Security.Cryptography.ECPoint
                {
                    X = pubkeyRemote[1..][..32].ToArray(),
                    Y = pubkeyRemote[1..][32..].ToArray()
                }
            });
            return ecdh1.DeriveKeyMaterial(ecdh2.PublicKey).Sha256();
        }

        /// <summary>
        /// Tests if a transaction matches the bloom filter.
        /// </summary>
        internal static bool Test(this BloomFilter filter, Transaction tx)
        {
            if (filter.Check(tx.Hash.ToArray())) return true;
            if (tx.Signers.Any(p => filter.Check(p.Account.ToArray())))
                return true;
            return false;
        }
    }
}
