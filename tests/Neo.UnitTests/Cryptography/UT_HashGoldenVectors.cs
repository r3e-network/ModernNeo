// Copyright (C) 2015-2025 The Neo Project.
//
// UT_HashGoldenVectors.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.Cryptography;
using System;
using System.Security.Cryptography;

namespace Neo.UnitTests.Cryptography
{
    [TestClass]
    public class UT_HashGoldenVectors
    {
        [TestMethod]
        [TestCategory("Golden")]
        public void Hash160_Empty_BitcoinVector()
        {
            // Hash160("") = RIPEMD160(SHA256(""))
            // Known Bitcoin vector: b472a266d0bd89c13706a4132ccfb16f7c3b9fcb
            var empty = Array.Empty<byte>();
            var hash = Crypto.Hash160(empty);
            var hex = Convert.ToHexString(hash).ToLowerInvariant();
            Assert.AreEqual("b472a266d0bd89c13706a4132ccfb16f7c3b9fcb", hex);
        }

        [TestMethod]
        [TestCategory("Golden")]
        public void Hash256_Empty_DoubleSha256()
        {
            // Hash256("") must equal double SHA-256 of empty
            var empty = Array.Empty<byte>();
            var expected = SHA256.HashData(SHA256.HashData(empty));
            var actual = Crypto.Hash256(empty);
            CollectionAssert.AreEqual(expected, actual);
        }
    }
}
