// Copyright (C) 2015-2025 The Neo Project.
//
// UT_IBlockchainOperations.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.Ledger;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using System;
using System.Reflection;

namespace Neo.UnitTests.Ledger
{
    /// <summary>
    /// Unit tests for the IBlockchainOperations interface.
    /// </summary>
    [TestClass]
    public class UT_IBlockchainOperations
    {
        [TestMethod]
        public void TestIBlockchainOperationsInterfaceExists()
        {
            // Verify the interface exists and is accessible
            var interfaceType = typeof(IBlockchainOperations);
            Assert.IsNotNull(interfaceType);
            Assert.IsTrue(interfaceType.IsInterface);
        }

        [TestMethod]
        public void TestIBlockchainOperationsHasMemPoolProperty()
        {
            var interfaceType = typeof(IBlockchainOperations);
            var memPoolProp = interfaceType.GetProperty("MemPool");
            Assert.IsNotNull(memPoolProp);
            Assert.AreEqual(typeof(MemoryPool), memPoolProp.PropertyType);
        }

        [TestMethod]
        public void TestIBlockchainOperationsHasHeaderCacheProperty()
        {
            var interfaceType = typeof(IBlockchainOperations);
            var headerCacheProp = interfaceType.GetProperty("HeaderCache");
            Assert.IsNotNull(headerCacheProp);
            Assert.AreEqual(typeof(HeaderCache), headerCacheProp.PropertyType);
        }

        [TestMethod]
        public void TestIBlockchainOperationsHasStoreViewProperty()
        {
            var interfaceType = typeof(IBlockchainOperations);
            var prop = interfaceType.GetProperty("StoreView");
            Assert.IsNotNull(prop);
            Assert.AreEqual(typeof(StoreCache), prop.PropertyType);
        }

        [TestMethod]
        public void TestIBlockchainOperationsHasGetSnapshotCacheMethod()
        {
            var interfaceType = typeof(IBlockchainOperations);
            var method = interfaceType.GetMethod("GetSnapshotCache");
            Assert.IsNotNull(method);
            Assert.AreEqual(typeof(StoreCache), method.ReturnType);
        }

        [TestMethod]
        public void TestIBlockchainOperationsHasContainsTransactionMethod()
        {
            var interfaceType = typeof(IBlockchainOperations);
            var method = interfaceType.GetMethod("ContainsTransaction");
            Assert.IsNotNull(method);
            Assert.AreEqual(typeof(ContainsTransactionType), method.ReturnType);
        }

        [TestMethod]
        public void TestNeoSystemImplementsIBlockchainOperations()
        {
            // Verify NeoSystem implements IBlockchainOperations
            var neoSystemType = typeof(NeoSystem);
            var interfaceType = typeof(IBlockchainOperations);
            Assert.IsTrue(interfaceType.IsAssignableFrom(neoSystemType));
        }
    }
}
