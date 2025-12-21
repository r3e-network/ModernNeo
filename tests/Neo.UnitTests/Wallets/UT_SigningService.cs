// Copyright (C) 2015-2025 The Neo Project.
//
// UT_SigningService.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.Core.Interfaces;
using Neo.Cryptography;
using Neo.Network.P2P;
using Neo.Sign;
using Neo.UnitTests;
using Neo.Wallets;
using System;
using System.Linq;

namespace Neo.UnitTests.Wallets
{
    [TestClass]
    public class UT_SigningService
    {
        [TestMethod]
        public void Wallet_Implements_ISigningService_Sign_Works()
        {
            var wallet = new MyWallet();
            var privateKey = new byte[32];
            privateKey[0] = 0x01;

            var key = new KeyPair(privateKey);
            wallet.CreateAccount(privateKey);

            var signingService = (ISigningService)wallet;
            var message = new byte[] { 0x42, 0x43, 0x44 };
            var signature = signingService.Sign(message, key.PublicKey.EncodePoint(true));

            Assert.AreEqual(64, signature.Length);
            Assert.IsTrue(Crypto.VerifySignature(message, signature.Span, key.PublicKey));
        }

        [TestMethod]
        public void Wallet_Implements_ISigningService_SignBlockHash_Works_And_EnforcesNetwork()
        {
            var wallet = new MyWallet();
            var privateKey = new byte[32];
            privateKey[0] = 0x02;

            var key = new KeyPair(privateKey);
            wallet.CreateAccount(privateKey);

            var snapshotCache = TestBlockchain.GetTestSnapshotCache();
            var network = TestProtocolSettings.Default.Network;
            var block = TestUtils.MakeBlock(snapshotCache, UInt256.Zero, 0);

            var signingService = (ISigningService)wallet;
            var signature = signingService.SignBlockHash(block.Hash.GetSpan(), key.PublicKey.EncodePoint(true), network);

            Assert.AreEqual(64, signature.Length);
            Assert.IsTrue(Crypto.VerifySignature(block.GetSignData(network), signature.Span, key.PublicKey));

            Assert.ThrowsExactly<SignException>(() =>
                signingService.SignBlockHash(block.Hash.GetSpan(), key.PublicKey.EncodePoint(true), network + 1));
        }

        [TestMethod]
        public void Wallet_Implements_ISigningService_CanSign_And_GetSignableKeys()
        {
            var wallet = new MyWallet();
            var privateKey = new byte[32];
            privateKey[0] = 0x03;

            var key = new KeyPair(privateKey);
            var pubKey = key.PublicKey.EncodePoint(true);

            var signingService = (ISigningService)wallet;
            Assert.IsFalse(signingService.CanSign(pubKey));
            Assert.IsFalse(signingService.CanSign(new byte[] { 0x01 }));

            wallet.CreateAccount(privateKey);
            Assert.IsTrue(signingService.CanSign(pubKey));

            var keys = signingService.GetSignablePublicKeys().ToArray();
            Assert.IsTrue(keys.Any(k => k.SequenceEqual(pubKey)));

            wallet.GetAccount(key.PublicKey)!.Lock = true;
            Assert.IsFalse(signingService.CanSign(pubKey));
            Assert.AreEqual(0, signingService.GetSignablePublicKeys().Count());
        }
    }
}
