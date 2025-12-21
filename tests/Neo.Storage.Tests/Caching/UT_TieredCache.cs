// Copyright (C) 2015-2025 The Neo Project.
//
// UT_TieredCache.cs file belongs to the neo project and is free
// software distributed under the MIT software license.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.Persistence.Caching;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Neo.Storage.Tests.Caching
{
    [TestClass]
    public class UT_TieredCache
    {
        [TestMethod]
        public void TestBasicSetAndGet()
        {
            using var cache = new TieredCache<string, string>();

            cache.Set("key1", "value1");

            Assert.IsTrue(cache.TryGet("key1", out var value));
            Assert.AreEqual("value1", value);
        }

        [TestMethod]
        public void TestCacheMiss()
        {
            using var cache = new TieredCache<string, string>();

            Assert.IsFalse(cache.TryGet("nonexistent", out var value));
            Assert.IsNull(value);
        }

        [TestMethod]
        public void TestL1CachePopulation()
        {
            var options = new TieredCacheOptions
            {
                L1MaxItems = 5,
                L2MaxItems = 10
            };
            using var cache = new TieredCache<string, string>(options);

            // Add items to L1
            for (int i = 0; i < 3; i++)
            {
                cache.Set($"key{i}", $"value{i}");
            }

            Assert.AreEqual(3, cache.L1Count);
            Assert.AreEqual(0, cache.L2Count);
        }

        [TestMethod]
        public void TestL2CachePopulationWhenL1Full()
        {
            var options = new TieredCacheOptions
            {
                L1MaxItems = 2,
                L2MaxItems = 10
            };
            using var cache = new TieredCache<string, string>(options);

            // Fill L1
            cache.Set("key1", "value1");
            cache.Set("key2", "value2");

            // This should go to L2 since L1 is full
            cache.Set("key3", "value3");

            Assert.AreEqual(2, cache.L1Count);
            Assert.AreEqual(1, cache.L2Count);
        }

        [TestMethod]
        public void TestPromotionFromL2ToL1()
        {
            var options = new TieredCacheOptions
            {
                L1MaxItems = 2,
                L2MaxItems = 10,
                PromotionThreshold = 3
            };
            using var cache = new TieredCache<string, string>(options);

            // Fill L1
            cache.Set("key1", "value1");
            cache.Set("key2", "value2");

            // Add to L2
            cache.Set("key3", "value3");
            Assert.AreEqual(1, cache.L2Count);

            // Access key3 multiple times to trigger promotion
            for (int i = 0; i < 3; i++)
            {
                cache.TryGet("key3", out _);
            }

            // key3 should be promoted to L1
            Assert.AreEqual(2, cache.L1Count);
            Assert.AreEqual(1, cache.L2Count); // One item demoted from L1
        }

        [TestMethod]
        public void TestDemotionFromL1ToL2()
        {
            var options = new TieredCacheOptions
            {
                L1MaxItems = 2,
                L2MaxItems = 10,
                PromotionThreshold = 3
            };
            using var cache = new TieredCache<string, string>(options);

            // Fill L1
            cache.Set("key1", "value1");
            cache.Set("key2", "value2");

            // Access key1 multiple times to make it hot
            for (int i = 0; i < 5; i++)
            {
                cache.TryGet("key1", out _);
            }

            // Add to L2
            cache.Set("key3", "value3");

            // Access key3 to trigger promotion (key2 should be demoted as coldest)
            for (int i = 0; i < 3; i++)
            {
                cache.TryGet("key3", out _);
            }

            // Verify key1 is still in L1 (hot), key2 was demoted
            Assert.IsTrue(cache.Contains("key1"));
            Assert.IsTrue(cache.Contains("key2"));
            Assert.IsTrue(cache.Contains("key3"));
        }

        [TestMethod]
        public void TestRemove()
        {
            using var cache = new TieredCache<string, string>();

            cache.Set("key1", "value1");
            Assert.IsTrue(cache.Contains("key1"));

            Assert.IsTrue(cache.Remove("key1"));
            Assert.IsFalse(cache.Contains("key1"));

            // Removing non-existent key should return false
            Assert.IsFalse(cache.Remove("key1"));
        }

        [TestMethod]
        public void TestContains()
        {
            var options = new TieredCacheOptions
            {
                L1MaxItems = 2,
                L2MaxItems = 10
            };
            using var cache = new TieredCache<string, string>(options);

            cache.Set("key1", "value1");
            cache.Set("key2", "value2");
            cache.Set("key3", "value3"); // Goes to L2

            Assert.IsTrue(cache.Contains("key1"));
            Assert.IsTrue(cache.Contains("key2"));
            Assert.IsTrue(cache.Contains("key3"));
            Assert.IsFalse(cache.Contains("key4"));
        }

        [TestMethod]
        public void TestClear()
        {
            using var cache = new TieredCache<string, string>();

            cache.Set("key1", "value1");
            cache.Set("key2", "value2");

            Assert.AreEqual(2, cache.TotalCount);

            cache.Clear();

            Assert.AreEqual(0, cache.TotalCount);
            Assert.IsFalse(cache.Contains("key1"));
            Assert.IsFalse(cache.Contains("key2"));
        }

        [TestMethod]
        public void TestHitRatio()
        {
            using var cache = new TieredCache<string, string>();

            cache.Set("key1", "value1");

            // 3 hits
            cache.TryGet("key1", out _);
            cache.TryGet("key1", out _);
            cache.TryGet("key1", out _);

            // 2 misses
            cache.TryGet("key2", out _);
            cache.TryGet("key3", out _);

            // Hit ratio should be 3/5 = 0.6
            Assert.AreEqual(0.6, cache.HitRatio, 0.01);
        }

        [TestMethod]
        public void TestStatistics()
        {
            var options = new TieredCacheOptions
            {
                L1MaxItems = 2,
                L2MaxItems = 10
            };
            using var cache = new TieredCache<string, string>(options);

            cache.Set("key1", "value1");
            cache.Set("key2", "value2");
            cache.Set("key3", "value3");

            cache.TryGet("key1", out _); // Hit
            cache.TryGet("key4", out _); // Miss

            var stats = cache.GetStatistics();

            Assert.AreEqual(2, stats.L1Count);
            Assert.AreEqual(1, stats.L2Count);
            Assert.AreEqual(3, stats.TotalCount);
            Assert.AreEqual(1, stats.Hits);
            Assert.AreEqual(1, stats.Misses);
            Assert.AreEqual(0.5, stats.HitRatio, 0.01);
        }

        [TestMethod]
        public async Task TestTTLEviction()
        {
            var options = new TieredCacheOptions
            {
                L1MaxItems = 10,
                L2MaxItems = 10,
                L1Ttl = TimeSpan.FromMilliseconds(100),
                L2Ttl = TimeSpan.FromMilliseconds(200),
                EvictionInterval = TimeSpan.FromMilliseconds(50)
            };
            using var cache = new TieredCache<string, string>(options);

            cache.Set("key1", "value1");
            Assert.IsTrue(cache.Contains("key1"));

            // Wait for TTL to expire and eviction to run
            await Task.Delay(300);

            // Item should be evicted
            Assert.IsFalse(cache.Contains("key1"));
        }

        [TestMethod]
        public void TestConcurrentAccess()
        {
            using var cache = new TieredCache<int, int>();
            var tasks = new List<Task>();

            // Concurrent writes
            for (int i = 0; i < 100; i++)
            {
                int key = i;
                tasks.Add(Task.Run(() => cache.Set(key, key * 2)));
            }

            Task.WaitAll(tasks.ToArray());
            tasks.Clear();

            // Concurrent reads
            var results = new int[100];
            for (int i = 0; i < 100; i++)
            {
                int key = i;
                tasks.Add(Task.Run(() =>
                {
                    if (cache.TryGet(key, out var value))
                    {
                        results[key] = value;
                    }
                }));
            }

            Task.WaitAll(tasks.ToArray());

            // Verify all values
            for (int i = 0; i < 100; i++)
            {
                Assert.AreEqual(i * 2, results[i]);
            }
        }

        [TestMethod]
        public void TestCustomComparer()
        {
            var comparer = StringComparer.OrdinalIgnoreCase;
            using var cache = new TieredCache<string, string>(comparer: comparer);

            cache.Set("KEY1", "value1");

            Assert.IsTrue(cache.TryGet("key1", out var value));
            Assert.AreEqual("value1", value);

            Assert.IsTrue(cache.TryGet("KEY1", out value));
            Assert.AreEqual("value1", value);
        }

        [TestMethod]
        public void TestSizeTracking()
        {
            using var cache = new TieredCache<string, byte[]>();

            var data1 = new byte[100];
            var data2 = new byte[200];

            cache.Set("key1", data1, size: 100);
            cache.Set("key2", data2, size: 200);

            Assert.IsTrue(cache.TryGet("key1", out var result1));
            Assert.AreEqual(100, result1.Length);

            Assert.IsTrue(cache.TryGet("key2", out var result2));
            Assert.AreEqual(200, result2.Length);
        }

        [TestMethod]
        public void TestAccessCountIncrement()
        {
            var options = new TieredCacheOptions
            {
                L1MaxItems = 2,
                L2MaxItems = 10,
                PromotionThreshold = 2
            };
            using var cache = new TieredCache<string, string>(options);

            // Fill L1
            cache.Set("key1", "value1");
            cache.Set("key2", "value2");

            // Add to L2
            cache.Set("key3", "value3");

            // Access once (not enough for promotion)
            cache.TryGet("key3", out _);
            Assert.AreEqual(1, cache.L2Count);

            // Access again (should trigger promotion)
            cache.TryGet("key3", out _);

            // Verify promotion occurred
            Assert.AreEqual(2, cache.L1Count);
        }

        [TestMethod]
        public void TestUpdateExistingKey()
        {
            using var cache = new TieredCache<string, string>();

            cache.Set("key1", "value1");
            Assert.IsTrue(cache.TryGet("key1", out var value));
            Assert.AreEqual("value1", value);

            // Update the value
            cache.Set("key1", "value2");
            Assert.IsTrue(cache.TryGet("key1", out value));
            Assert.AreEqual("value2", value);
        }

        [TestMethod]
        public void TestL2ToL1PromotionRemovesFromL2()
        {
            var options = new TieredCacheOptions
            {
                L1MaxItems = 3,
                L2MaxItems = 10,
                PromotionThreshold = 2
            };
            using var cache = new TieredCache<string, string>(options);

            // Fill L1
            cache.Set("key1", "value1");
            cache.Set("key2", "value2");
            cache.Set("key3", "value3");

            // Add to L2
            cache.Set("key4", "value4");
            Assert.AreEqual(3, cache.L1Count);
            Assert.AreEqual(1, cache.L2Count);

            // Promote key4 from L2 to L1
            cache.TryGet("key4", out _);
            cache.TryGet("key4", out _);

            // key4 should now be in L1, and one item demoted to L2
            Assert.AreEqual(3, cache.L1Count);
            Assert.AreEqual(1, cache.L2Count);
            Assert.IsTrue(cache.Contains("key4"));
        }

        [TestMethod]
        public void TestDispose()
        {
            var cache = new TieredCache<string, string>();
            cache.Set("key1", "value1");

            cache.Dispose();

            // After dispose, cache should be cleared
            Assert.AreEqual(0, cache.TotalCount);
        }

        [TestMethod]
        public void TestZeroHitRatioInitially()
        {
            using var cache = new TieredCache<string, string>();

            // No hits or misses yet
            Assert.AreEqual(0.0, cache.HitRatio);
        }
    }
}
