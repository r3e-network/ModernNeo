// Copyright (C) 2015-2025 The Neo Project.
//
// UT_WitnessGolden.cs file belongs to the neo project and is free
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

namespace Neo.UnitTests.Network.P2P.Payloads
{
    [TestClass]
    public class UT_WitnessGolden
    {
        [TestMethod]
        [TestCategory("Golden")]
        public void Serialize_Empty_Golden()
        {
            var w = Witness.Empty;
            var hex = Convert.ToHexString(w.ToArray()).ToLowerInvariant();

            // Empty witness: varbytes(0) + varbytes(0) => 00 00
            Assert.AreEqual("0000", hex);
        }
    }
}
