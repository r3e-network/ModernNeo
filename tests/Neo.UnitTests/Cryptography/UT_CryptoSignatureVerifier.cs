// Copyright (C) 2015-2025 The Neo Project.
//
// UT_CryptoSignatureVerifier.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.Cryptography;
using Neo.Wallets;
using System;

namespace Neo.UnitTests.Cryptography
{
    [TestClass]
    public class UT_CryptoSignatureVerifier
    {
        [TestMethod]
        public void TestVerifyAndVerifyMultiple()
        {
            var privateKey = new byte[32];
            privateKey[0] = 0x01;

            var otherPrivateKey = new byte[32];
            otherPrivateKey[0] = 0x02;

            var key = new KeyPair(privateKey);
            var otherKey = new KeyPair(otherPrivateKey);

            var message = new byte[] { 0x01, 0x02, 0x03 };
            var signature = Crypto.Sign(message, privateKey);

            var verifier = CryptoSignatureVerifier.Default;

            Assert.IsTrue(verifier.Verify(message, signature, key.PublicKey.EncodePoint(true)));
            Assert.IsFalse(verifier.Verify(message, signature, otherKey.PublicKey.EncodePoint(true)));

            Assert.IsTrue(verifier.VerifyMultiple(message, [signature], [key.PublicKey.EncodePoint(true)]));
            Assert.IsFalse(verifier.VerifyMultiple(message, [signature], [otherKey.PublicKey.EncodePoint(true)]));
            Assert.IsFalse(verifier.VerifyMultiple(message, [signature], Array.Empty<byte[]>()));
        }
    }
}
