// Copyright (C) 2015-2025 The Neo Project.
//
// UT_BlockGolden.cs file belongs to the neo project and is free
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
    public class UT_BlockGolden
    {
        [TestMethod]
        public void Serialize_Golden_EmptyTxs()
        {
            var header = new Header
            {
                Version = 0,
                PrevHash = UInt256.Zero,
                MerkleRoot = UInt256.Zero,
                Timestamp = 0x1122334455667788UL,
                Nonce = 0xAABBCCDDEEFF0011UL,
                Index = 123u,
                PrimaryIndex = 2,
                NextConsensus = UInt160.Zero,
                Witness = Witness.Empty
            };

            var block = new Block
            {
                Header = header,
                Transactions = Array.Empty<Transaction>()
            };

            var ms = new MemoryStream();
            using (var bw = new BinaryWriter(ms, Utility.StrictUTF8, true))
            {
                block.Serialize(bw);
            }
            var hex = Convert.ToHexString(ms.ToArray()).ToLowerInvariant();

            // Header (unsigned): see UT_HeaderGolden + witness array [1] + witness(0000), then tx array count=0
            var expected =
                // header unsigned
                "00000000" + new string('0', 64) + new string('0', 64) +
                "8877665544332211" + "1100ffeeddccbbaa" + "7b000000" + "02" + new string('0', 40) +
                // witnesses array count = 1
                "01" +
                // witness (empty): invocation varbytes(0) + verification varbytes(0)
                "0000" +
                // tx array count = 0
                "00";

            Assert.AreEqual(expected, hex);
        }
    }
}
