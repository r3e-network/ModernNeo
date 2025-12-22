// Copyright (C) 2015-2025 The Neo Project.
//
// UT_TransactionSignedGolden.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.Cryptography;
using Neo.Network.P2P.Payloads;
using Neo.SmartContract;
using Neo.UnitTests;
using Neo.VM;
using Neo.Wallets;
using System;

namespace Neo.UnitTests.Network.P2P.Payloads
{
    [TestClass]
    public class UT_TransactionSignedGolden
    {
        // Deterministic private key for golden vector
        private static readonly byte[] s_privKey = TestUtils.GetByteArray(32, 0x42);

        [TestMethod]
        [TestCategory("Golden")]
        public void SignedTransaction_Serialization_Golden()
        {
            // Build a minimal transaction with one signer and empty script
            var tx = new Transaction
            {
                Version = 0,
                Nonce = 1,
                SystemFee = 0,
                NetworkFee = 0,
                ValidUntilBlock = 1,
                Script = new byte[] { 0x51 }, // minimal PUSH1
                Signers = [new Signer { Account = UInt160.Zero, Scopes = WitnessScope.CalledByEntry }],
                Attributes = Array.Empty<TransactionAttribute>(),
                Witnesses = [Witness.Empty]
            };

            // Sign witness
            var key = new KeyPair(s_privKey);
            var sig = tx.Sign(key, TestProtocolSettings.Default.Network);
            using (var sb = new ScriptBuilder())
            {
                sb.EmitPush(sig);
                tx.Witnesses = [ new Witness
                {
                    InvocationScript = sb.ToArray(),
                    VerificationScript = Neo.SmartContract.Contract.CreateSignatureRedeemScript(key.PublicKey)
                }];
            }

            // Serialize and compare against golden hex
            // Invariants for signed golden:
            // - Single witness matches signer
            // - Verification script equals signature contract for the public key
            // - Signature verifies over the transaction sign data
            Assert.AreEqual(1, tx.Witnesses.Length);
            var wit = tx.Witnesses[0];
            CollectionAssert.AreEqual(Neo.SmartContract.Contract.CreateSignatureRedeemScript(key.PublicKey), wit.VerificationScript.ToArray());
            Assert.IsTrue(Crypto.VerifySignature(tx.GetSignData(TestProtocolSettings.Default.Network), sig, key.PublicKey));

            // Round-trip serialization must preserve bytes exactly
            var bytes = tx.ToArray();
            var round = bytes.AsSerializable<Transaction>();
            CollectionAssert.AreEqual(bytes, round.ToArray());
        }
    }
}
