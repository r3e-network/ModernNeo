// Copyright (C) 2015-2025 The Neo Project.
//
// UT_StoreFactory.cs file belongs to the neo project and is free
// software distributed under the MIT software license.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.Persistence;
using Neo.Persistence.Providers;

namespace Neo.Storage.Tests
{
    [TestClass]
    public class UT_StoreFactory
    {
        [TestMethod]
        public void TestGetMemoryStoreProvider()
        {
            var provider = StoreFactory.GetStoreProvider("MemoryStore");

            Assert.IsNotNull(provider);
            Assert.AreEqual("MemoryStore", provider.Name);
        }

        [TestMethod]
        public void TestGetDefaultProvider()
        {
            // Empty string should return default (MemoryStore)
            var provider = StoreFactory.GetStoreProvider("");

            Assert.IsNotNull(provider);
            Assert.AreEqual("MemoryStore", provider.Name);
        }

        [TestMethod]
        public void TestGetNonExistentProvider()
        {
            var provider = StoreFactory.GetStoreProvider("NonExistentProvider");

            Assert.IsNull(provider);
        }

        [TestMethod]
        public void TestGetMemoryStore()
        {
            using var store = StoreFactory.GetStore("MemoryStore", "");

            Assert.IsNotNull(store);
            Assert.IsInstanceOfType(store, typeof(MemoryStore));
        }

        [TestMethod]
        public void TestGetDefaultStore()
        {
            using var store = StoreFactory.GetStore("", "");

            Assert.IsNotNull(store);
            Assert.IsInstanceOfType(store, typeof(MemoryStore));
        }

        [TestMethod]
        public void TestMemoryStoreOperations()
        {
            using var store = StoreFactory.GetStore("MemoryStore", "");

            var key = new byte[] { 1, 2, 3 };
            var value = new byte[] { 4, 5, 6 };

            store.Put(key, value);

            Assert.IsTrue(store.Contains(key));
            Assert.IsTrue(store.TryGet(key, out var result));
            CollectionAssert.AreEqual(value, result);
        }

        [TestMethod]
        public void TestLevelDbStoreProviderExists()
        {
            var provider = StoreFactory.GetStoreProvider("LevelDBStore");

            Assert.IsNotNull(provider);
            Assert.AreEqual("LevelDBStore", provider.Name);
        }

        [TestMethod]
        public void TestRocksDbStoreProviderExists()
        {
            var provider = StoreFactory.GetStoreProvider("RocksDBStore");

            Assert.IsNotNull(provider);
            Assert.AreEqual("RocksDBStore", provider.Name);
        }
    }

    [TestClass]
    public class UT_MemoryStoreProvider
    {
        [TestMethod]
        public void TestProviderName()
        {
            var provider = new MemoryStoreProvider();

            Assert.AreEqual("MemoryStore", provider.Name);
        }

        [TestMethod]
        public void TestGetStore()
        {
            var provider = new MemoryStoreProvider();

            using var store = provider.GetStore(null);

            Assert.IsNotNull(store);
            Assert.IsInstanceOfType(store, typeof(MemoryStore));
        }

        [TestMethod]
        public void TestGetStoreWithPath()
        {
            var provider = new MemoryStoreProvider();

            // Path is ignored for MemoryStore
            using var store = provider.GetStore("/some/path");

            Assert.IsNotNull(store);
            Assert.IsInstanceOfType(store, typeof(MemoryStore));
        }

        [TestMethod]
        public void TestMultipleStoresAreIndependent()
        {
            var provider = new MemoryStoreProvider();

            using var store1 = provider.GetStore(null);
            using var store2 = provider.GetStore(null);

            var key = new byte[] { 1 };
            var value = new byte[] { 10 };

            store1.Put(key, value);

            // store2 should not see store1's data
            Assert.IsTrue(store1.Contains(key));
            Assert.IsFalse(store2.Contains(key));
        }
    }
}
