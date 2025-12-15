// Copyright (C) 2015-2025 The Neo Project.
//
// UT_NeoSystemNode.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.Network.P2P;

namespace Neo.Node.Tests
{
    [TestClass]
    public class UT_NeoSystemNode
    {
        [TestMethod]
        public void TestNeoSystemNodeProperties()
        {
            // Verify NeoSystemNode class exists and has expected properties
            var nodeType = typeof(NeoSystemNode);

            Assert.IsNotNull(nodeType);
            Assert.IsNotNull(nodeType.GetProperty("System"));
            Assert.IsNotNull(nodeType.GetProperty("ChannelsConfig"));
            Assert.IsNotNull(nodeType.GetProperty("IsStarted"));
        }

        [TestMethod]
        public void TestNeoSystemNodeImplementsIDisposable()
        {
            var nodeType = typeof(NeoSystemNode);
            Assert.IsTrue(typeof(System.IDisposable).IsAssignableFrom(nodeType));
        }

        [TestMethod]
        public void TestNeoSystemNodeFactoryExists()
        {
            var factoryType = typeof(NeoSystemNodeFactory);
            Assert.IsNotNull(factoryType);

            var createMethod = factoryType.GetMethod("Create");
            Assert.IsNotNull(createMethod);
            Assert.IsTrue(createMethod.IsStatic);
        }

        [TestMethod]
        public void TestChannelsConfigFactoryExists()
        {
            var factoryType = typeof(ChannelsConfigFactory);
            Assert.IsNotNull(factoryType);

            var createMethod = factoryType.GetMethod("Create");
            Assert.IsNotNull(createMethod);
            Assert.IsTrue(createMethod.IsStatic);
        }
    }
}
