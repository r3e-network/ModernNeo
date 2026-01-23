// Copyright (C) 2015-2025 The Neo Project.
//
// UT_IMemoryPool.cs file belongs to the neo project and is free
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
using System;
using System.Collections.Generic;
using System.Linq;

namespace Neo.UnitTests.Ledger
{
    /// <summary>
    /// Unit tests for the IMemoryPool interface.
    /// </summary>
    [TestClass]
    public class UT_IMemoryPool
    {
        [TestMethod]
        public void TestIMemoryPoolInterfaceExists()
        {
            // Verify the interface exists and is accessible
            var interfaceType = typeof(IMemoryPool);
            Assert.IsNotNull(interfaceType);
            Assert.IsTrue(interfaceType.IsInterface);
        }

        [TestMethod]
        public void TestIMemoryPoolInheritsIReadOnlyCollection()
        {
            // Verify IMemoryPool inherits from IReadOnlyCollection<Transaction>
            var interfaceType = typeof(IMemoryPool);
            var baseInterface = typeof(IReadOnlyCollection<Transaction>);
            Assert.IsTrue(baseInterface.IsAssignableFrom(interfaceType));
        }

        [TestMethod]
        public void TestIMemoryPoolHasRequiredProperties()
        {
            // Verify required properties exist
            var interfaceType = typeof(IMemoryPool);

            var capacityProp = interfaceType.GetProperty("Capacity");
            Assert.IsNotNull(capacityProp);
            Assert.AreEqual(typeof(int), capacityProp.PropertyType);

            var verifiedCountProp = interfaceType.GetProperty("VerifiedCount");
            Assert.IsNotNull(verifiedCountProp);
            Assert.AreEqual(typeof(int), verifiedCountProp.PropertyType);

            var unverifiedCountProp = interfaceType.GetProperty("UnVerifiedCount");
            Assert.IsNotNull(unverifiedCountProp);
            Assert.AreEqual(typeof(int), unverifiedCountProp.PropertyType);
        }

        [TestMethod]
        public void TestIMemoryPoolHasRequiredMethods()
        {
            // Verify required methods exist
            var interfaceType = typeof(IMemoryPool);

            var containsKeyMethod = interfaceType.GetMethod("ContainsKey");
            Assert.IsNotNull(containsKeyMethod);

            var tryGetValueMethod = interfaceType.GetMethod("TryGetValue");
            Assert.IsNotNull(tryGetValueMethod);

            var getVerifiedMethod = interfaceType.GetMethod("GetVerifiedTransactions");
            Assert.IsNotNull(getVerifiedMethod);

            var getVerifiedAndUnverifiedMethod = interfaceType.GetMethod("GetVerifiedAndUnverifiedTransactions");
            Assert.IsNotNull(getVerifiedAndUnverifiedMethod);
        }

        [TestMethod]
        public void TestIMemoryPoolHasRequiredEvents()
        {
            // Verify required events exist
            var interfaceType = typeof(IMemoryPool);

            var addedEvent = interfaceType.GetEvent("TransactionAdded");
            Assert.IsNotNull(addedEvent);

            var removedEvent = interfaceType.GetEvent("TransactionRemoved");
            Assert.IsNotNull(removedEvent);

            var newTxEvent = interfaceType.GetEvent("NewTransaction");
            Assert.IsNotNull(newTxEvent);
        }

        [TestMethod]
        public void TestMemoryPoolImplementsIMemoryPool()
        {
            // Verify MemoryPool implements IMemoryPool
            var memoryPoolType = typeof(MemoryPool);
            var interfaceType = typeof(IMemoryPool);
            Assert.IsTrue(interfaceType.IsAssignableFrom(memoryPoolType));
        }
    }
}
