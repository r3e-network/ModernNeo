// Copyright (C) 2015-2025 The Neo Project.
//
// UT_HeaderGolden.cs file belongs to the neo project and is free
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
    public class UT_HeaderGolden
    {
        [TestMethod]
        [TestCategory("Golden")]
        public void SerializeUnsigned_Golden()
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

            var ms = new MemoryStream();
            using (var bw = new BinaryWriter(ms, Utility.StrictUTF8, true))
            {
                ((Neo.Core.Interfaces.IVerifiableBase)header).SerializeUnsigned(bw);
            }
            var hex = Convert.ToHexString(ms.ToArray()).ToLowerInvariant();

            var expectedHex =
                // version (LE)
                "00000000"
                // prev hash (zero)
                + new string('0', 64)
                // merkle root (zero)
                + new string('0', 64)
                // timestamp (LE)
                + "8877665544332211"
                // nonce (LE)
                + "1100ffeeddccbbaa"
                // index (LE)
                + "7b000000"
                // primary index
                + "02"
                // next consensus (UInt160.Zero)
                + new string('0', 40);

            Assert.AreEqual(expectedHex, hex);
        }
    }
}
