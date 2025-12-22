// Copyright (C) 2015-2025 The Neo Project.
//
// UT_TieredCache.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.Persistence.Caching;
using System;
using System.Threading.Tasks;

namespace Neo.UnitTests.Storage.Caching
{
    [TestClass]
    public class UT_TieredCache
    {
        [TestMethod]
        public void TestTieredCacheOptions_DefaultValues()
        {
            var options = new TieredCacheOptions();

            Assert.AreEqual(10_000, options.L1MaxItems);
            Assert.AreEqual(100_000, options.L2MaxItems);
            Assert.AreEqual(TimeSpan.FromMinutes(5), options.L1Ttl);
            Assert.AreEqual(TimeSpan.FromMinutes(30), options.L2Ttl);
            Assert.AreEqual(3, options.PromotionThreshold);
            Assert.AreEqual(TimeSpan.FromMinutes(1), options.EvictionInterval);
        }

        [TestMethod]
        public void TestTieredCache_SetAndGet_Succeeds()
        {
            using var cache = new TieredCache<string, string>();

            cache.Set("key1", "value1");

            Assert.IsTrue(cache.TryGet("key1", out var value));
            Assert.AreEqual("value1", value);
        }

        [TestMethod]
        public void TestTieredCache_GetNonExistent_ReturnsFalse()
        {
            using var cache = new TieredCache<string, string>();

            Assert.IsFalse(cache.TryGet("nonexistent", out var value));
            Assert.IsNull(value);
        }

        [TestMethod]
        public void TestTieredCache_Remove_Succeeds()
        {
            using var cache = new TieredCache<string, string>();

            cache.Set("key1", "value1");
            Assert.IsTrue(cache.Contains("key1"));

            var removed = cache.Remove("key1");

            Assert.IsTrue(removed);
            Assert.IsFalse(cache.Contains("key1"));
        }

        [TestMethod]
        public void TestTieredCache_RemoveNonExistent_ReturnsFalse()
        {
            using var cache = new TieredCache<string, string>();

            var removed = cache.Remove("nonexistent");

            Assert.IsFalse(removed);
        }

        [TestMethod]
        public void TestTieredCache_Clear_RemovesAllItems()
        {
            using var cache = new TieredCache<string, string>();

            cache.Set("key1", "value1");
            cache.Set("key2", "value2");
            cache.Set("key3", "value3");

            Assert.AreEqual(3, cache.TotalCount);

            cache.Clear();

            Assert.AreEqual(0, cache.TotalCount);
            Assert.IsFalse(cache.Contains("key1"));
        }

        [TestMethod]
        public void TestTieredCache_Contains_ReturnsCorrectValue()
        {
            using var cache = new TieredCache<string, string>();

            cache.Set("key1", "value1");

            Assert.IsTrue(cache.Contains("key1"));
            Assert.IsFalse(cache.Contains("key2"));
        }

        [TestMethod]
        public void TestTieredCache_L1Count_TracksCorrectly()
        {
            var options = new TieredCacheOptions { L1MaxItems = 5 };
            using var cache = new TieredCache<string, string>(options);

            for (var i = 0; i < 5; i++)
            {
                cache.Set($"key{i}", $"value{i}");
            }

            Assert.AreEqual(5, cache.L1Count);
        }

        [TestMethod]
        public void TestTieredCache_L2Overflow_WhenL1Full()
        {
            var options = new TieredCacheOptions { L1MaxItems = 2, L2MaxItems = 10 };
            using var cache = new TieredCache<string, string>(options);

            cache.Set("key1", "value1");
            cache.Set("key2", "value2");
            cache.Set("key3", "value3"); // Should go to L2

            Assert.AreEqual(2, cache.L1Count);
            Assert.AreEqual(1, cache.L2Count);
            Assert.AreEqual(3, cache.TotalCount);
        }

        [TestMethod]
        public void TestTieredCache_HitRatio_CalculatesCorrectly()
        {
            using var cache = new TieredCache<string, string>();

            cache.Set("key1", "value1");

            // 2 hits
            cache.TryGet("key1", out _);
            cache.TryGet("key1", out _);

            // 1 miss
            cache.TryGet("nonexistent", out _);

            var ratio = cache.HitRatio;
            Assert.AreEqual(2.0 / 3.0, ratio, 0.001);
        }

        [TestMethod]
        public void TestTieredCache_GetStatistics_ReturnsValidData()
        {
            using var cache = new TieredCache<string, string>();

            cache.Set("key1", "value1");
            cache.TryGet("key1", out _);
            cache.TryGet("nonexistent", out _);

            var stats = cache.GetStatistics();

            Assert.AreEqual(1, stats.L1Count);
            Assert.AreEqual(0, stats.L2Count);
            Assert.AreEqual(1, stats.TotalCount);
            Assert.AreEqual(1, stats.Hits);
            Assert.AreEqual(1, stats.Misses);
        }

        [TestMethod]
        public void TestTieredCache_UpdateExistingKey_OverwritesValue()
        {
            using var cache = new TieredCache<string, string>();

            cache.Set("key1", "value1");
            cache.Set("key1", "value2");

            Assert.IsTrue(cache.TryGet("key1", out var value));
            Assert.AreEqual("value2", value);
        }

        [TestMethod]
        public void TestTieredCache_Dispose_ClearsCache()
        {
            var cache = new TieredCache<string, string>();
            cache.Set("key1", "value1");

            cache.Dispose();

            Assert.AreEqual(0, cache.TotalCount);
        }

        [TestMethod]
        public void TestCacheStatistics_DefaultValues()
        {
            var stats = new CacheStatistics();

            Assert.AreEqual(0, stats.L1Count);
            Assert.AreEqual(0, stats.L2Count);
            Assert.AreEqual(0, stats.TotalCount);
            Assert.AreEqual(0, stats.Hits);
            Assert.AreEqual(0, stats.Misses);
            Assert.AreEqual(0, stats.HitRatio);
        }
    }
}
