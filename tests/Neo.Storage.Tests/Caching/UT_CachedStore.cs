// Copyright (C) 2015-2025 The Neo Project.
//
// UT_CachedStore.cs file belongs to the neo project and is free
// software distributed under the MIT software license.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.Persistence;
using Neo.Persistence.Caching;
using Neo.Persistence.Providers;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Neo.Storage.Tests.Caching
{
    [TestClass]
    public class UT_CachedStore
    {
        private MemoryStore _innerStore = null!;
        private CachedStore _cachedStore = null!;

        [TestInitialize]
        public void Setup()
        {
            _innerStore = new MemoryStore();
        }

        [TestCleanup]
        public void Cleanup()
        {
            _cachedStore?.Dispose();
            _innerStore?.Dispose();
        }

        [TestMethod]
        public void TestBasicPut()
        {
            var options = new CachedStoreOptions
            {
                EnableBatchWrites = false,
                EnableCompression = false
            };
            _cachedStore = new CachedStore(_innerStore, options);

            var key = new byte[] { 1, 2, 3 };
            var value = new byte[] { 4, 5, 6 };

            _cachedStore.Put(key, value);

            Assert.IsTrue(_cachedStore.Contains(key));
        }

        [TestMethod]
        public void TestCacheHit()
        {
            var options = new CachedStoreOptions
            {
                EnableBatchWrites = false,
                EnableCompression = false
            };
            _cachedStore = new CachedStore(_innerStore, options);

            var key = new byte[] { 1, 2, 3 };
            var value = new byte[] { 4, 5, 6 };

            // First write
            _cachedStore.Put(key, value);

            // First read (cache miss, loads from store)
            Assert.IsTrue(_cachedStore.TryGet(key, out var result1));
            CollectionAssert.AreEqual(value, result1);

            // Second read (cache hit)
            Assert.IsTrue(_cachedStore.TryGet(key, out var result2));
            CollectionAssert.AreEqual(value, result2);

            var stats = _cachedStore.CacheStats;
            Assert.IsTrue(stats.Hits > 0);
        }

        [TestMethod]
        public void TestCacheMiss()
        {
            var options = new CachedStoreOptions
            {
                EnableBatchWrites = false,
                EnableCompression = false
            };
            _cachedStore = new CachedStore(_innerStore, options);

            var key = new byte[] { 1, 2, 3 };

            Assert.IsFalse(_cachedStore.TryGet(key, out var result));
            Assert.IsNull(result);

            var stats = _cachedStore.CacheStats;
            Assert.AreEqual(1, stats.Misses);
        }

        [TestMethod]
        public void TestWriteThrough()
        {
            var options = new CachedStoreOptions
            {
                EnableBatchWrites = false,
                EnableCompression = false
            };
            _cachedStore = new CachedStore(_innerStore, options);

            var key = new byte[] { 1, 2, 3 };
            var value = new byte[] { 4, 5, 6 };

            _cachedStore.Put(key, value);

            // Verify data is in inner store
            Assert.IsTrue(_innerStore.TryGet(key, out var result));
            CollectionAssert.AreEqual(value, result);
        }

        [TestMethod]
        public async Task TestBatchWrite()
        {
            var options = new CachedStoreOptions
            {
                EnableBatchWrites = true,
                EnableCompression = false,
                BatchOptions = new BatchWriterOptions { BatchSize = 100 }
            };
            _cachedStore = new CachedStore(_innerStore, options);

            var key = new byte[] { 1, 2, 3 };
            var value = new byte[] { 4, 5, 6 };

            _cachedStore.Put(key, value);

            // Data not in inner store yet (batched)
            Assert.IsFalse(_innerStore.Contains(key));

            // Flush batch
            await _cachedStore.FlushAsync();

            // Now data should be in inner store
            Assert.IsTrue(_innerStore.Contains(key));
        }

        [TestMethod]
        public void TestDelete()
        {
            var options = new CachedStoreOptions
            {
                EnableBatchWrites = false,
                EnableCompression = false
            };
            _cachedStore = new CachedStore(_innerStore, options);

            var key = new byte[] { 1, 2, 3 };
            var value = new byte[] { 4, 5, 6 };

            _cachedStore.Put(key, value);
            Assert.IsTrue(_cachedStore.Contains(key));

            _cachedStore.Delete(key);
            Assert.IsFalse(_cachedStore.Contains(key));
        }

        [TestMethod]
        public void TestClearCache()
        {
            var options = new CachedStoreOptions
            {
                EnableBatchWrites = false,
                EnableCompression = false
            };
            _cachedStore = new CachedStore(_innerStore, options);

            var key = new byte[] { 1, 2, 3 };
            var value = new byte[] { 4, 5, 6 };

            _cachedStore.Put(key, value);

            // Read to populate cache
            _cachedStore.TryGet(key, out _);

            var statsBefore = _cachedStore.CacheStats;
            Assert.IsTrue(statsBefore.TotalCount > 0);

            _cachedStore.ClearCache();

            var statsAfter = _cachedStore.CacheStats;
            Assert.AreEqual(0, statsAfter.TotalCount);
        }

        [TestMethod]
        public void TestCompression()
        {
            var options = new CachedStoreOptions
            {
                EnableBatchWrites = false,
                EnableCompression = true,
                CompressionOptions = new Persistence.Compression.StorageCompressorOptions
                {
                    MinSizeForCompression = 10
                }
            };
            _cachedStore = new CachedStore(_innerStore, options);

            var key = new byte[] { 1 };
            var largeValue = new byte[1000];
            for (int i = 0; i < largeValue.Length; i++)
            {
                largeValue[i] = (byte)(i % 256);
            }

            _cachedStore.Put(key, largeValue);

            // Read back
            Assert.IsTrue(_cachedStore.TryGet(key, out var result));
            CollectionAssert.AreEqual(largeValue, result);

            var compressionStats = _cachedStore.CompressionStats;
            Assert.IsNotNull(compressionStats);
        }

        [TestMethod]
        public void TestFind()
        {
            var options = new CachedStoreOptions
            {
                EnableBatchWrites = false,
                EnableCompression = false
            };
            _cachedStore = new CachedStore(_innerStore, options);

            _cachedStore.Put(new byte[] { 1 }, new byte[] { 10 });
            _cachedStore.Put(new byte[] { 2 }, new byte[] { 20 });
            _cachedStore.Put(new byte[] { 3 }, new byte[] { 30 });

            var results = _cachedStore.Find(null, SeekDirection.Forward).ToList();

            Assert.AreEqual(3, results.Count);
            Assert.AreEqual(1, results[0].Key[0]);
            Assert.AreEqual(2, results[1].Key[0]);
            Assert.AreEqual(3, results[2].Key[0]);
        }

        [TestMethod]
        public void TestFindPopulatesCache()
        {
            var options = new CachedStoreOptions
            {
                EnableBatchWrites = false,
                EnableCompression = false
            };
            _cachedStore = new CachedStore(_innerStore, options);

            _cachedStore.Put(new byte[] { 1 }, new byte[] { 10 });
            _cachedStore.Put(new byte[] { 2 }, new byte[] { 20 });

            var statsBefore = _cachedStore.CacheStats;

            // Find should populate cache
            var results = _cachedStore.Find(null, SeekDirection.Forward).ToList();

            var statsAfter = _cachedStore.CacheStats;

            Assert.AreEqual(2, results.Count);
            Assert.IsTrue(statsAfter.TotalCount >= statsBefore.TotalCount);
        }

        [TestMethod]
        public void TestSnapshot()
        {
            var options = new CachedStoreOptions
            {
                EnableBatchWrites = false,
                EnableCompression = false
            };
            _cachedStore = new CachedStore(_innerStore, options);

            var key = new byte[] { 1, 2, 3 };
            var value = new byte[] { 4, 5, 6 };

            _cachedStore.Put(key, value);

            using var snapshot = _cachedStore.GetSnapshot();

            Assert.IsNotNull(snapshot);
            Assert.IsTrue(snapshot.Contains(key));
        }

        [TestMethod]
        public void TestOnNewSnapshotEvent()
        {
            var options = new CachedStoreOptions
            {
                EnableBatchWrites = false,
                EnableCompression = false
            };
            _cachedStore = new CachedStore(_innerStore, options);

            int eventCount = 0;
            _cachedStore.OnNewSnapshot += (sender, snapshot) => eventCount++;

            using var snapshot = _cachedStore.GetSnapshot();

            Assert.AreEqual(1, eventCount);
        }

        [TestMethod]
        public void TestStatistics()
        {
            var options = new CachedStoreOptions
            {
                EnableBatchWrites = true,
                EnableCompression = false
            };
            _cachedStore = new CachedStore(_innerStore, options);

            var key = new byte[] { 1 };
            var value = new byte[] { 10 };

            _cachedStore.Put(key, value);
            _cachedStore.TryGet(key, out _);

            var cacheStats = _cachedStore.CacheStats;
            var batchStats = _cachedStore.BatchStats;

            Assert.IsNotNull(cacheStats);
            Assert.IsNotNull(batchStats);
            Assert.IsTrue(cacheStats.TotalCount > 0);
        }

        [TestMethod]
        public void TestUpdateExistingKey()
        {
            var options = new CachedStoreOptions
            {
                EnableBatchWrites = false,
                EnableCompression = false
            };
            _cachedStore = new CachedStore(_innerStore, options);

            var key = new byte[] { 1 };

            _cachedStore.Put(key, new byte[] { 10 });
            Assert.IsTrue(_cachedStore.TryGet(key, out var value1));
            Assert.AreEqual(10, value1[0]);

            _cachedStore.Put(key, new byte[] { 20 });
            Assert.IsTrue(_cachedStore.TryGet(key, out var value2));
            Assert.AreEqual(20, value2[0]);
        }

        [TestMethod]
        public void TestCacheInvalidationOnDelete()
        {
            var options = new CachedStoreOptions
            {
                EnableBatchWrites = false,
                EnableCompression = false
            };
            _cachedStore = new CachedStore(_innerStore, options);

            var key = new byte[] { 1 };
            var value = new byte[] { 10 };

            _cachedStore.Put(key, value);

            // Read to populate cache
            _cachedStore.TryGet(key, out _);

            // Delete should invalidate cache
            _cachedStore.Delete(key);

            Assert.IsFalse(_cachedStore.Contains(key));
        }

        [TestMethod]
        public async Task TestDisposeFlushesData()
        {
            var options = new CachedStoreOptions
            {
                EnableBatchWrites = true,
                EnableCompression = false,
                BatchOptions = new BatchWriterOptions { BatchSize = 100 }
            };
            _cachedStore = new CachedStore(_innerStore, options);

            var key = new byte[] { 1 };
            var value = new byte[] { 10 };

            _cachedStore.Put(key, value);

            // Data not in inner store yet
            Assert.IsFalse(_innerStore.Contains(key));

            // Dispose should flush
            _cachedStore.Dispose();

            // Now data should be in inner store
            Assert.IsTrue(_innerStore.Contains(key));
        }

        [TestMethod]
        public void TestContainsChecksCache()
        {
            var options = new CachedStoreOptions
            {
                EnableBatchWrites = false,
                EnableCompression = false
            };
            _cachedStore = new CachedStore(_innerStore, options);

            var key = new byte[] { 1 };
            var value = new byte[] { 10 };

            _cachedStore.Put(key, value);

            // Read to populate cache
            _cachedStore.TryGet(key, out _);

            // Contains should check cache first
            Assert.IsTrue(_cachedStore.Contains(key));
        }

        [TestMethod]
        public void TestLargeDataWithCompression()
        {
            var options = new CachedStoreOptions
            {
                EnableBatchWrites = false,
                EnableCompression = true,
                CompressionOptions = new Persistence.Compression.StorageCompressorOptions
                {
                    MinSizeForCompression = 100,
                    Algorithm = Persistence.Compression.CompressionAlgorithm.Brotli
                }
            };
            _cachedStore = new CachedStore(_innerStore, options);

            var key = new byte[] { 1 };
            var largeValue = new byte[10000];
            for (int i = 0; i < largeValue.Length; i++)
            {
                largeValue[i] = (byte)(i % 256);
            }

            _cachedStore.Put(key, largeValue);

            Assert.IsTrue(_cachedStore.TryGet(key, out var result));
            Assert.AreEqual(10000, result.Length);
            CollectionAssert.AreEqual(largeValue, result);
        }

        [TestMethod]
        public async Task TestConcurrentAccess()
        {
            var options = new CachedStoreOptions
            {
                EnableBatchWrites = true,
                EnableCompression = false
            };
            _cachedStore = new CachedStore(_innerStore, options);

            var tasks = new System.Collections.Generic.List<Task>();

            // Concurrent writes
            for (int i = 0; i < 50; i++)
            {
                int key = i;
                tasks.Add(Task.Run(() => _cachedStore.Put(new byte[] { (byte)key }, new byte[] { (byte)(key * 2) })));
            }

            await Task.WhenAll(tasks);
            await _cachedStore.FlushAsync();

            tasks.Clear();

            // Concurrent reads
            for (int i = 0; i < 50; i++)
            {
                int key = i;
                tasks.Add(Task.Run(() =>
                {
                    _cachedStore.TryGet(new byte[] { (byte)key }, out var value);
                }));
            }

            await Task.WhenAll(tasks);

            // Verify cache stats
            var stats = _cachedStore.CacheStats;
            Assert.IsTrue(stats.TotalCount > 0);
        }

        [TestMethod]
        public void TestFindWithPrefix()
        {
            var options = new CachedStoreOptions
            {
                EnableBatchWrites = false,
                EnableCompression = false
            };
            _cachedStore = new CachedStore(_innerStore, options);

            _cachedStore.Put(new byte[] { 1, 1 }, new byte[] { 11 });
            _cachedStore.Put(new byte[] { 1, 2 }, new byte[] { 12 });
            _cachedStore.Put(new byte[] { 2, 1 }, new byte[] { 21 });

            var results = _cachedStore.Find(new byte[] { 1 }, SeekDirection.Forward).ToList();

            // Range query: returns all keys >= {1} in forward order
            Assert.AreEqual(3, results.Count);
            Assert.AreEqual(1, results[0].Key[0]); // {1, 1}
            Assert.AreEqual(1, results[1].Key[0]); // {1, 2}
            Assert.AreEqual(2, results[2].Key[0]); // {2, 1}
        }

        [TestMethod]
        public void TestCompressionDisabled()
        {
            var options = new CachedStoreOptions
            {
                EnableBatchWrites = false,
                EnableCompression = false
            };
            _cachedStore = new CachedStore(_innerStore, options);

            var key = new byte[] { 1 };
            var value = new byte[] { 10, 20, 30 };

            _cachedStore.Put(key, value);

            Assert.IsTrue(_cachedStore.TryGet(key, out var result));
            CollectionAssert.AreEqual(value, result);

            // Compression stats should be null
            Assert.IsNull(_cachedStore.CompressionStats);
        }

        [TestMethod]
        public async Task TestBatchStatistics()
        {
            var options = new CachedStoreOptions
            {
                EnableBatchWrites = true,
                EnableCompression = false,
                BatchOptions = new BatchWriterOptions { BatchSize = 5 }
            };
            _cachedStore = new CachedStore(_innerStore, options);

            for (int i = 0; i < 10; i++)
            {
                _cachedStore.Put(new byte[] { (byte)i }, new byte[] { (byte)(i * 2) });
            }

            await _cachedStore.FlushAsync();

            var batchStats = _cachedStore.BatchStats;

            Assert.AreEqual(10, batchStats.TotalWrites);
            Assert.IsTrue(batchStats.TotalBatches > 0);
        }

        [TestMethod]
        public void TestCacheHitRatio()
        {
            var options = new CachedStoreOptions
            {
                EnableBatchWrites = false,
                EnableCompression = false
            };
            _cachedStore = new CachedStore(_innerStore, options);

            var key = new byte[] { 1 };
            var value = new byte[] { 10 };

            _cachedStore.Put(key, value);

            // First read (miss)
            _cachedStore.TryGet(key, out _);

            // Second read (hit)
            _cachedStore.TryGet(key, out _);

            // Third read (hit)
            _cachedStore.TryGet(key, out _);

            var stats = _cachedStore.CacheStats;

            // Should have 2 hits and 1 miss (hit ratio = 2/3)
            Assert.IsTrue(stats.HitRatio > 0.5);
        }
    }
}
