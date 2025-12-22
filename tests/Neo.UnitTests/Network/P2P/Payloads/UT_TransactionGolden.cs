// Copyright (C) 2015-2025 The Neo Project.
//
// UT_TransactionGolden.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.Extensions;
using Neo.IO;
using Neo.Network.P2P.Payloads;
using System;
using System.IO;

namespace Neo.UnitTests.Network.P2P.Payloads
{
    [TestClass]
    public class UT_TransactionGolden
    {
        [TestMethod]
        [TestCategory("Golden")]
        public void SerializeUnsigned_Golden()
        {
            // Deterministic, minimal valid unsigned transaction (no witnesses included)
            var tx = new Transaction
            {
                Version = 0,
                Nonce = 0x11223344,
                SystemFee = 1000,
                NetworkFee = 200,
                ValidUntilBlock = 123456u,
                Signers = [new Signer { Account = UInt160.Zero, Scopes = WitnessScope.CalledByEntry }],
                Attributes = Array.Empty<TransactionAttribute>(),
                Script = new byte[] { 0x51 }, // PUSH1
                Witnesses = Array.Empty<Witness>()
            };

            // SerializeUnsigned
            var ms = new MemoryStream();
            using (var bw = new BinaryWriter(ms, Utility.StrictUTF8, true))
            {
                // Use the same path as hashing (verifiable unsigned)
                ((Neo.Core.Interfaces.IVerifiableBase)tx).SerializeUnsigned(bw);
            }
            var bytes = ms.ToArray();
            var hex = Convert.ToHexString(bytes).ToLowerInvariant();

            // Freeze the current encoding as a golden vector so regressions are caught
            var expectedHex =
                // version
                "00"
                // nonce (little endian)
                + "44332211"
                // system fee (LE 8 bytes)
                + "e803000000000000"
                // network fee (LE 8 bytes)
                + "c800000000000000"
                // valid until block (LE 4 bytes)
                + "40e20100"
                // signers array (varint=1)
                + "01"
                // signer: UInt160.Zero (20 bytes zero) + scope=CalledByEntry(0x01) (no allowed lists serialized)
                + new string('0', 40) + "01"
                // attributes array (varint=0)
                + "00"
                // script var bytes (len=1) + 0x51
                + "01" + "51";

            Assert.AreEqual(expectedHex, hex);
        }
    }
}
