// Copyright (C) 2015-2025 The Neo Project.
//
// UT_MemoryStore.cs file belongs to the neo project and is free
// software distributed under the MIT software license.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.Persistence;
using Neo.Persistence.Providers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Neo.Storage.Tests.Providers
{
    [TestClass]
    public class UT_MemoryStore
    {
        private MemoryStore _store = null!;

        [TestInitialize]
        public void Setup()
        {
            _store = new MemoryStore();
        }

        [TestCleanup]
        public void Cleanup()
        {
            _store?.Dispose();
        }

        [TestMethod]
        public void TestBasicPut()
        {
            var key = new byte[] { 1, 2, 3 };
            var value = new byte[] { 4, 5, 6 };

            _store.Put(key, value);

            Assert.IsTrue(_store.Contains(key));
        }

        [TestMethod]
        public void TestBasicGet()
        {
            var key = new byte[] { 1, 2, 3 };
            var value = new byte[] { 4, 5, 6 };

            _store.Put(key, value);

            Assert.IsTrue(_store.TryGet(key, out var result));
            CollectionAssert.AreEqual(value, result);
        }

        [TestMethod]
        public void TestGetNonExistent()
        {
            var key = new byte[] { 1, 2, 3 };

            Assert.IsFalse(_store.TryGet(key, out var result));
            Assert.IsNull(result);
        }

        [TestMethod]
        public void TestDelete()
        {
            var key = new byte[] { 1, 2, 3 };
            var value = new byte[] { 4, 5, 6 };

            _store.Put(key, value);
            Assert.IsTrue(_store.Contains(key));

            _store.Delete(key);
            Assert.IsFalse(_store.Contains(key));
        }

        [TestMethod]
        public void TestDeleteNonExistent()
        {
            var key = new byte[] { 1, 2, 3 };

            // Should not throw
            _store.Delete(key);
            Assert.IsFalse(_store.Contains(key));
        }

        [TestMethod]
        public void TestContains()
        {
            var key1 = new byte[] { 1 };
            var key2 = new byte[] { 2 };
            var value = new byte[] { 10 };

            _store.Put(key1, value);

            Assert.IsTrue(_store.Contains(key1));
            Assert.IsFalse(_store.Contains(key2));
        }

        [TestMethod]
        public void TestUpdateExistingKey()
        {
            var key = new byte[] { 1, 2, 3 };
            var value1 = new byte[] { 4, 5, 6 };
            var value2 = new byte[] { 7, 8, 9 };

            _store.Put(key, value1);
            Assert.IsTrue(_store.TryGet(key, out var result1));
            CollectionAssert.AreEqual(value1, result1);

            _store.Put(key, value2);
            Assert.IsTrue(_store.TryGet(key, out var result2));
            CollectionAssert.AreEqual(value2, result2);
        }

        [TestMethod]
        public void TestFindForward()
        {
            _store.Put(new byte[] { 1 }, new byte[] { 10 });
            _store.Put(new byte[] { 2 }, new byte[] { 20 });
            _store.Put(new byte[] { 3 }, new byte[] { 30 });

            var results = _store.Find(null, SeekDirection.Forward).ToList();

            Assert.AreEqual(3, results.Count);
            Assert.AreEqual(1, results[0].Key[0]);
            Assert.AreEqual(2, results[1].Key[0]);
            Assert.AreEqual(3, results[2].Key[0]);
        }

        [TestMethod]
        public void TestFindBackward()
        {
            // Test backward search with prefix matching
            _store.Put(new byte[] { 1, 1 }, new byte[] { 11 });
            _store.Put(new byte[] { 1, 2 }, new byte[] { 12 });
            _store.Put(new byte[] { 1, 3 }, new byte[] { 13 });
            _store.Put(new byte[] { 2, 1 }, new byte[] { 21 });

            var results = _store.Find(new byte[] { 1 }, SeekDirection.Backward).ToList();

            // Should return only keys starting with {1}, in reverse order
            Assert.AreEqual(3, results.Count);
            Assert.AreEqual(3, results[0].Key[1]); // {1, 3}
            Assert.AreEqual(2, results[1].Key[1]); // {1, 2}
            Assert.AreEqual(1, results[2].Key[1]); // {1, 1}
        }

        [TestMethod]
        public void TestFindWithPrefix()
        {
            _store.Put(new byte[] { 1, 1 }, new byte[] { 11 });
            _store.Put(new byte[] { 1, 2 }, new byte[] { 12 });
            _store.Put(new byte[] { 2, 1 }, new byte[] { 21 });

            var results = _store.Find(new byte[] { 1 }, SeekDirection.Forward).ToList();

            Assert.AreEqual(2, results.Count);
            Assert.AreEqual(1, results[0].Key[0]);
            Assert.AreEqual(1, results[1].Key[0]);
        }

        [TestMethod]
        public void TestFindBackwardEmptyPrefix()
        {
            _store.Put(new byte[] { 1 }, new byte[] { 10 });

            var results = _store.Find(new byte[] { }, SeekDirection.Backward).ToList();

            // Backward with empty prefix should return nothing
            Assert.AreEqual(0, results.Count);
        }

        [TestMethod]
        public void TestSnapshot()
        {
            var key = new byte[] { 1, 2, 3 };
            var value = new byte[] { 4, 5, 6 };

            _store.Put(key, value);

            using var snapshot = _store.GetSnapshot();

            Assert.IsNotNull(snapshot);
            Assert.IsTrue(snapshot.Contains(key));
            Assert.IsTrue(snapshot.TryGet(key, out var result));
            CollectionAssert.AreEqual(value, result);
        }

        [TestMethod]
        public void TestSnapshotIsolation()
        {
            var key = new byte[] { 1 };
            var value1 = new byte[] { 10 };
            var value2 = new byte[] { 20 };

            _store.Put(key, value1);

            using var snapshot = _store.GetSnapshot();

            // Modify store after snapshot creation
            _store.Put(key, value2);

            // Snapshot should still see old value
            Assert.IsTrue(snapshot.TryGet(key, out var snapshotValue));
            CollectionAssert.AreEqual(value1, snapshotValue);

            // Store should see new value
            Assert.IsTrue(_store.TryGet(key, out var storeValue));
            CollectionAssert.AreEqual(value2, storeValue);
        }

        [TestMethod]
        public void TestSnapshotCommit()
        {
            var key1 = new byte[] { 1 };
            var key2 = new byte[] { 2 };
            var value1 = new byte[] { 10 };
            var value2 = new byte[] { 20 };

            using var snapshot = _store.GetSnapshot();

            snapshot.Put(key1, value1);
            snapshot.Put(key2, value2);

            // Changes not visible in store yet
            Assert.IsFalse(_store.Contains(key1));
            Assert.IsFalse(_store.Contains(key2));

            snapshot.Commit();

            // Now changes should be visible
            Assert.IsTrue(_store.Contains(key1));
            Assert.IsTrue(_store.Contains(key2));
        }

        [TestMethod]
        public void TestSnapshotDelete()
        {
            var key = new byte[] { 1 };
            var value = new byte[] { 10 };

            _store.Put(key, value);

            using var snapshot = _store.GetSnapshot();

            snapshot.Delete(key);
            snapshot.Commit();

            Assert.IsFalse(_store.Contains(key));
        }

        [TestMethod]
        public void TestConcurrentReads()
        {
            var key = new byte[] { 1 };
            var value = new byte[] { 10 };

            _store.Put(key, value);

            var tasks = new List<Task<bool>>();

            for (int i = 0; i < 100; i++)
            {
                tasks.Add(Task.Run(() => _store.TryGet(key, out var _)));
            }

            Task.WaitAll(tasks.ToArray());

            // All reads should succeed
            Assert.IsTrue(tasks.All(t => t.Result));
        }

        [TestMethod]
        public void TestConcurrentWrites()
        {
            var tasks = new List<Task>();

            for (int i = 0; i < 100; i++)
            {
                int key = i;
                tasks.Add(Task.Run(() => _store.Put(new byte[] { (byte)key }, new byte[] { (byte)(key * 2) })));
            }

            Task.WaitAll(tasks.ToArray());

            // Verify all writes
            for (int i = 0; i < 100; i++)
            {
                Assert.IsTrue(_store.TryGet(new byte[] { (byte)i }, out var value));
                Assert.AreEqual((byte)(i * 2), value[0]);
            }
        }

        [TestMethod]
        public void TestLargeData()
        {
            var key = new byte[] { 1 };
            var largeValue = new byte[100000];
            for (int i = 0; i < largeValue.Length; i++)
            {
                largeValue[i] = (byte)(i % 256);
            }

            _store.Put(key, largeValue);

            Assert.IsTrue(_store.TryGet(key, out var result));
            Assert.AreEqual(100000, result.Length);
            CollectionAssert.AreEqual(largeValue, result);
        }

        [TestMethod]
        public void TestMultipleSnapshots()
        {
            var key = new byte[] { 1 };
            var value1 = new byte[] { 10 };
            var value2 = new byte[] { 20 };

            _store.Put(key, value1);

            using var snapshot1 = _store.GetSnapshot();

            _store.Put(key, value2);

            using var snapshot2 = _store.GetSnapshot();

            // snapshot1 should see value1
            Assert.IsTrue(snapshot1.TryGet(key, out var result1));
            CollectionAssert.AreEqual(value1, result1);

            // snapshot2 should see value2
            Assert.IsTrue(snapshot2.TryGet(key, out var result2));
            CollectionAssert.AreEqual(value2, result2);
        }

        [TestMethod]
        public void TestOnNewSnapshotEvent()
        {
            int eventCount = 0;
            IStoreSnapshot? capturedSnapshot = null;

            _store.OnNewSnapshot += (sender, snapshot) =>
            {
                eventCount++;
                capturedSnapshot = snapshot;
            };

            using var snapshot = _store.GetSnapshot();

            Assert.AreEqual(1, eventCount);
            Assert.IsNotNull(capturedSnapshot);
            Assert.AreSame(snapshot, capturedSnapshot);
        }

        [TestMethod]
        public void TestEmptyStore()
        {
            var results = _store.Find(null, SeekDirection.Forward).ToList();
            Assert.AreEqual(0, results.Count);
        }

        [TestMethod]
        public void TestKeyIsolation()
        {
            var key1 = new byte[] { 1, 2, 3 };
            var key2 = new byte[] { 1, 2, 3 }; // Same content, different array
            var value = new byte[] { 4, 5, 6 };

            _store.Put(key1, value);

            // Should find with different array instance
            Assert.IsTrue(_store.TryGet(key2, out var result));
            CollectionAssert.AreEqual(value, result);
        }

        [TestMethod]
        public void TestValueIsolation()
        {
            var key = new byte[] { 1 };
            var value = new byte[] { 10 };

            _store.Put(key, value);

            // Modify original value array
            value[0] = 99;

            // Store should have original value
            Assert.IsTrue(_store.TryGet(key, out var result));
            Assert.AreEqual(10, result[0]);
        }

        [TestMethod]
        public void TestFindOrdering()
        {
            _store.Put(new byte[] { 3 }, new byte[] { 30 });
            _store.Put(new byte[] { 1 }, new byte[] { 10 });
            _store.Put(new byte[] { 2 }, new byte[] { 20 });

            var results = _store.Find(null, SeekDirection.Forward).ToList();

            // Should be ordered
            Assert.AreEqual(1, results[0].Key[0]);
            Assert.AreEqual(2, results[1].Key[0]);
            Assert.AreEqual(3, results[2].Key[0]);
        }

        [TestMethod]
        public void TestSnapshotFind()
        {
            _store.Put(new byte[] { 1 }, new byte[] { 10 });
            _store.Put(new byte[] { 2 }, new byte[] { 20 });

            using var snapshot = _store.GetSnapshot();

            var results = snapshot.Find(null, SeekDirection.Forward).ToList();

            Assert.AreEqual(2, results.Count);
            Assert.AreEqual(1, results[0].Key[0]);
            Assert.AreEqual(2, results[1].Key[0]);
        }

        [TestMethod]
        public void TestSnapshotFindWithPrefix()
        {
            _store.Put(new byte[] { 1, 1 }, new byte[] { 11 });
            _store.Put(new byte[] { 1, 2 }, new byte[] { 12 });
            _store.Put(new byte[] { 2, 1 }, new byte[] { 21 });

            using var snapshot = _store.GetSnapshot();

            var results = snapshot.Find(new byte[] { 1 }, SeekDirection.Forward).ToList();

            Assert.AreEqual(2, results.Count);
            Assert.AreEqual(1, results[0].Key[0]);
            Assert.AreEqual(1, results[1].Key[0]);
        }

        [TestMethod]
        public void TestSnapshotFindBackward()
        {
            _store.Put(new byte[] { 1, 1 }, new byte[] { 11 });
            _store.Put(new byte[] { 1, 2 }, new byte[] { 12 });
            _store.Put(new byte[] { 1, 3 }, new byte[] { 13 });

            using var snapshot = _store.GetSnapshot();

            var results = snapshot.Find(new byte[] { 1 }, SeekDirection.Backward).ToList();

            Assert.AreEqual(3, results.Count);
            Assert.AreEqual(3, results[0].Key[1]); // {1, 3}
            Assert.AreEqual(2, results[1].Key[1]); // {1, 2}
            Assert.AreEqual(1, results[2].Key[1]); // {1, 1}
        }

        [TestMethod]
        public void TestSnapshotFindBackwardEmptyPrefix()
        {
            _store.Put(new byte[] { 1 }, new byte[] { 10 });

            using var snapshot = _store.GetSnapshot();

            var results = snapshot.Find(new byte[] { }, SeekDirection.Backward).ToList();

            Assert.AreEqual(0, results.Count);
        }

        [TestMethod]
        public void TestSnapshotTryGetNonExistent()
        {
            using var snapshot = _store.GetSnapshot();

#pragma warning disable CS0618 // Type or member is obsolete
            var result = snapshot.TryGet(new byte[] { 1 });
#pragma warning restore CS0618

            Assert.IsNull(result);
        }

        [TestMethod]
        public void TestSnapshotContainsNonExistent()
        {
            using var snapshot = _store.GetSnapshot();

            Assert.IsFalse(snapshot.Contains(new byte[] { 1 }));
        }

        [TestMethod]
        public void TestTryGetObsoleteMethod()
        {
            var key = new byte[] { 1, 2, 3 };
            var value = new byte[] { 4, 5, 6 };

            _store.Put(key, value);

#pragma warning disable CS0618 // Type or member is obsolete
            var result = _store.TryGet(key);
#pragma warning restore CS0618

            Assert.IsNotNull(result);
            CollectionAssert.AreEqual(value, result);
        }

        [TestMethod]
        public void TestTryGetObsoleteMethodNonExistent()
        {
#pragma warning disable CS0618 // Type or member is obsolete
            var result = _store.TryGet(new byte[] { 1 });
#pragma warning restore CS0618

            Assert.IsNull(result);
        }

        [TestMethod]
        public void TestSnapshotTryGetWithOutParam()
        {
            var key = new byte[] { 1 };
            var value = new byte[] { 10 };

            _store.Put(key, value);

            using var snapshot = _store.GetSnapshot();

            Assert.IsTrue(snapshot.TryGet(key, out var result));
            CollectionAssert.AreEqual(value, result);
        }

        [TestMethod]
        public void TestSnapshotTryGetWithOutParamNonExistent()
        {
            using var snapshot = _store.GetSnapshot();

            Assert.IsFalse(snapshot.TryGet(new byte[] { 1 }, out var result));
            Assert.IsNull(result);
        }

        [TestMethod]
        public void TestIndexerGet()
        {
            var key = new byte[] { 1 };
            var value = new byte[] { 10 };

            _store.Put(key, value);

            var result = ((IReadOnlyStore<byte[], byte[]>)_store)[key];

            CollectionAssert.AreEqual(value, result);
        }

        [TestMethod]
        [ExpectedException(typeof(KeyNotFoundException))]
        public void TestIndexerGetNonExistent()
        {
            var _ = ((IReadOnlyStore<byte[], byte[]>)_store)[new byte[] { 1 }];
        }
    }
}
