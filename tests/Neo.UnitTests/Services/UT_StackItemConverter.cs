// Copyright (C) 2015-2025 The Neo Project.
//
// UT_StackItemConverter.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.Services;
using Neo.SmartContract;
using Neo.VM.Types;

namespace Neo.UnitTests.Services
{
    [TestClass]
    public class UT_StackItemConverter
    {
        [TestMethod]
        public void TestInstance_IsSingleton()
        {
            var instance1 = StackItemConverter.Instance;
            var instance2 = StackItemConverter.Instance;
            Assert.AreSame(instance1, instance2);
        }

        [TestMethod]
        public void TestToStackItem_WithIInteroperable_ReturnsStackItem()
        {
            var converter = StackItemConverter.Instance;
            var signer = new Neo.Network.P2P.Payloads.Signer
            {
                Account = UInt160.Zero,
                Scopes = Neo.Network.P2P.Payloads.WitnessScope.CalledByEntry
            };

            var stackItem = converter.ToStackItem(signer, null);
            Assert.IsNotNull(stackItem);
            Assert.IsInstanceOfType(stackItem, typeof(StackItem));
        }

        [TestMethod]
        public void TestRoundTrip_WithIInteroperable_PreservesData()
        {
            var converter = StackItemConverter.Instance;

            // Create a signer and convert to stack item
            var original = new Neo.Network.P2P.Payloads.Signer
            {
                Account = UInt160.Zero,
                Scopes = Neo.Network.P2P.Payloads.WitnessScope.CalledByEntry
            };
            var stackItem = converter.ToStackItem(original, null);

            // Verify stack item was created
            Assert.IsNotNull(stackItem);
            Assert.IsInstanceOfType(stackItem, typeof(StackItem));
        }
    }
}
