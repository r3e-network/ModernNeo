// Copyright (C) 2015-2025 The Neo Project.
//
// UT_SerializationProbe.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.Network.P2P.Payloads;
using System;

namespace Neo.UnitTests.IO
{
    [TestClass]
    public class UT_SerializationProbe
    {
        [TestMethod]
        [TestCategory("Probe")]
        public void Probe_Transaction_Simple_DumpHex()
        {
            var tx = TestUtils.GetTransaction(UInt160.Zero);
            var hex = Convert.ToHexString(tx.ToArray()).ToLowerInvariant();
            Console.WriteLine($"TX_SIMPLE_HEX={hex}");
            Assert.IsTrue(hex.Length > 0);
        }

        [TestMethod]
        [TestCategory("Probe")]
        public void Probe_Witness_Empty_DumpHex()
        {
            var w = Witness.Empty;
            var hex = Convert.ToHexString(w.ToArray()).ToLowerInvariant();
            Console.WriteLine($"WITNESS_EMPTY_HEX={hex}");
            Assert.IsTrue(hex.Length > 0);
        }
    }
}

