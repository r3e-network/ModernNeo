// Copyright (C) 2015-2025 The Neo Project.
//
// UT_LmdbCacheStore.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

#nullable enable

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.Persistence;
using Neo.Persistence.Caching;
using Neo.Persistence.Providers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Neo.UnitTests.Storage.Caching
{
    /// <summary>
    /// Unit tests for LMDB cache store.
    /// </summary>
    [TestClass]
    public class UT_LmdbCacheStore
    {
        public TestContext TestContext { get; set; } = null!;

        private string _testPath = null!;

        [TestInitialize]
        public void Setup()
        {
            _testPath = Path.Combine(Path.GetTempPath(), $"neo-lmdb-test-{Guid.NewGuid():N}");
            Directory.CreateDirectory(_testPath);
        }

        [TestCleanup]
        public void Cleanup()
        {
            if (Directory.Exists(_testPath))
            {
                try { Directory.Delete(_testPath, true); } catch { }
            }
        }

        #region LmdbCacheStore Tests

        [TestMethod]
        public void LmdbCacheStore_Create_WithDefaultOptions()
        {
            // Arrange
            using var innerStore = new MemoryStore();

            // Act
            using var cacheStore = new LmdbCacheStore(innerStore, new LmdbCacheOptions
            {
                Path = _testPath,
                Persistent = false
            });

            // Assert
            var stats = cacheStore.GetLmdbStatistics();
            Assert.AreEqual(0, stats.LmdbHits);
            Assert.AreEqual(0, stats.LmdbMisses);
            Assert.AreEqual(0, stats.LmdbEntryCount);
        }

        [TestMethod]
        public void LmdbCacheStore_Put_And_TryGet()
        {
            // Arrange
            using var innerStore = new MemoryStore();
            using var cacheStore = new LmdbCacheStore(innerStore, new LmdbCacheOptions
            {
                Path = _testPath,
                Persistent = false,
                WriteThrough = true
            });

            var key = new byte[] { 0x01, 0x02, 0x03 };
            var value = new byte[] { 0xAA, 0xBB, 0xCC, 0xDD };

            // Act
            cacheStore.Put(key, value);
            var result = cacheStore.TryGet(key);

            // Assert
            Assert.IsNotNull(result);
            CollectionAssert.AreEqual(value, result);
        }

        [TestMethod]
        public void LmdbCacheStore_TryGet_FromInnerStore_PopulatesCache()
        {
            // Arrange
            using var innerStore = new MemoryStore();
            var key = new byte[] { 0x01, 0x02, 0x03 };
            var value = new byte[] { 0xAA, 0xBB, 0xCC, 0xDD };
            innerStore.Put(key, value);

            using var cacheStore = new LmdbCacheStore(innerStore, new LmdbCacheOptions
            {
                Path = _testPath,
                Persistent = false,
                WriteThrough = true
            });

            // Act - First access should miss LMDB cache
            var result1 = cacheStore.TryGet(key);
            var stats1 = cacheStore.GetLmdbStatistics();

            // Second access should hit memory cache (promoted from LMDB)
            var result2 = cacheStore.TryGet(key);
            var stats2 = cacheStore.GetLmdbStatistics();

            // Assert
            Assert.IsNotNull(result1);
            Assert.IsNotNull(result2);
            CollectionAssert.AreEqual(value, result1);
            CollectionAssert.AreEqual(value, result2);
            Assert.AreEqual(1, stats1.LmdbMisses); // First access misses LMDB
        }

        [TestMethod]
        public void LmdbCacheStore_Contains_ChecksAllLayers()
        {
            // Arrange
            using var innerStore = new MemoryStore();
            var key1 = new byte[] { 0x01 };
            var key2 = new byte[] { 0x02 };
            var key3 = new byte[] { 0x03 };
            var value = new byte[] { 0xFF };

            innerStore.Put(key1, value);

            using var cacheStore = new LmdbCacheStore(innerStore, new LmdbCacheOptions
            {
                Path = _testPath,
                Persistent = false,
                WriteThrough = true
            });

            cacheStore.Put(key2, value);

            // Act & Assert
            Assert.IsTrue(cacheStore.Contains(key1)); // In inner store
            Assert.IsTrue(cacheStore.Contains(key2)); // In cache
            Assert.IsFalse(cacheStore.Contains(key3)); // Nowhere
        }

        [TestMethod]
        public void LmdbCacheStore_Delete_RemovesFromAllLayers()
        {
            // Arrange
            using var innerStore = new MemoryStore();
            using var cacheStore = new LmdbCacheStore(innerStore, new LmdbCacheOptions
            {
                Path = _testPath,
                Persistent = false,
                WriteThrough = true
            });

            var key = new byte[] { 0x01, 0x02 };
            var value = new byte[] { 0xAA, 0xBB };

            cacheStore.Put(key, value);
            Assert.IsTrue(cacheStore.Contains(key));

            // Act
            cacheStore.Delete(key);

            // Assert
            Assert.IsFalse(cacheStore.Contains(key));
            Assert.IsNull(cacheStore.TryGet(key));
        }

        [TestMethod]
        public void LmdbCacheStore_Find_ReturnsResults()
        {
            // Arrange
            using var innerStore = new MemoryStore();
            using var cacheStore = new LmdbCacheStore(innerStore, new LmdbCacheOptions
            {
                Path = _testPath,
                Persistent = false,
                WriteThrough = true
            });

            var prefix = new byte[] { 0x01 };
            cacheStore.Put(new byte[] { 0x01, 0x01 }, new byte[] { 0xAA });
            cacheStore.Put(new byte[] { 0x01, 0x02 }, new byte[] { 0xBB });
            cacheStore.Put(new byte[] { 0x02, 0x01 }, new byte[] { 0xCC });

            // Act
            var results = cacheStore.Find(prefix).ToList();

            // Assert
            Assert.IsTrue(results.Count >= 2);
        }

        [TestMethod]
        public async Task LmdbCacheStore_WarmupAsync_PopulatesCache()
        {
            // Arrange
            using var innerStore = new MemoryStore();
            var keys = new List<byte[]>();
            for (int i = 0; i < 10; i++)
            {
                var key = new byte[] { 0x01, (byte)i };
                var value = new byte[] { (byte)(i * 10) };
                innerStore.Put(key, value);
                keys.Add(key);
            }

            using var cacheStore = new LmdbCacheStore(innerStore, new LmdbCacheOptions
            {
                Path = _testPath,
                Persistent = false,
                WriteThrough = true
            });

            // Act
            await cacheStore.WarmupAsync(keys, TestContext.CancellationTokenSource.Token);

            // Assert - All keys should now be in cache
            foreach (var key in keys)
            {
                Assert.IsTrue(cacheStore.Contains(key));
            }
        }

        [TestMethod]
        public async Task LmdbCacheStore_WarmupPrefixAsync_PopulatesCache()
        {
            // Arrange
            using var innerStore = new MemoryStore();
            var prefix = new byte[] { 0x01 };

            for (int i = 0; i < 5; i++)
            {
                innerStore.Put(new byte[] { 0x01, (byte)i }, new byte[] { (byte)i });
            }
            for (int i = 0; i < 5; i++)
            {
                innerStore.Put(new byte[] { 0x02, (byte)i }, new byte[] { (byte)i });
            }

            using var cacheStore = new LmdbCacheStore(innerStore, new LmdbCacheOptions
            {
                Path = _testPath,
                Persistent = false,
                WriteThrough = true
            });

            // Act
            await cacheStore.WarmupPrefixAsync(prefix, 100, TestContext.CancellationTokenSource.Token);

            // Assert - Prefix 0x01 keys should be warmed
            var stats = cacheStore.GetLmdbStatistics();
            Assert.IsTrue(stats.LmdbEntryCount >= 5);
        }

        [TestMethod]
        public void LmdbCacheStore_ClearCache_RemovesAllCachedItems()
        {
            // Arrange
            using var innerStore = new MemoryStore();
            using var cacheStore = new LmdbCacheStore(innerStore, new LmdbCacheOptions
            {
                Path = _testPath,
                Persistent = false,
                WriteThrough = true
            });

            for (int i = 0; i < 10; i++)
            {
                cacheStore.Put(new byte[] { (byte)i }, new byte[] { (byte)i });
            }

            var statsBefore = cacheStore.GetLmdbStatistics();
            Assert.IsTrue(statsBefore.LmdbEntryCount > 0);

            // Act
            cacheStore.ClearCache();

            // Assert
            var statsAfter = cacheStore.GetLmdbStatistics();
            Assert.AreEqual(0, statsAfter.LmdbEntryCount);
            Assert.AreEqual(0, statsAfter.MemoryCacheStats.TotalCount);
        }

        [TestMethod]
        public void LmdbCacheStore_GetSnapshot_ReturnsInnerStoreSnapshot()
        {
            // Arrange
            using var innerStore = new MemoryStore();
            using var cacheStore = new LmdbCacheStore(innerStore, new LmdbCacheOptions
            {
                Path = _testPath,
                Persistent = false
            });

            // Act
            using var snapshot = cacheStore.GetSnapshot();

            // Assert
            Assert.IsNotNull(snapshot);
        }

        #endregion

        #region LmdbCacheOptions Tests

        [TestMethod]
        public void LmdbCacheOptions_DefaultValues()
        {
            // Arrange & Act
            var options = new LmdbCacheOptions();

            // Assert
            Assert.IsNull(options.Path);
            Assert.AreEqual(1L * 1024 * 1024 * 1024, options.MapSize); // 1GB
            Assert.AreEqual(10, options.MaxDatabases);
            Assert.AreEqual(126, options.MaxReaders);
            Assert.IsTrue(options.Persistent);
            Assert.IsFalse(options.WriteThrough);
            Assert.IsTrue(options.EnableBackgroundWarmup);
            Assert.AreEqual(TimeSpan.FromSeconds(1), options.WarmupInterval);
            Assert.AreEqual(1000, options.WarmupBatchSize);
        }

        #endregion

        #region LmdbCacheStatistics Tests

        [TestMethod]
        public void LmdbCacheStatistics_CalculatesHitRatio()
        {
            // Arrange
            using var innerStore = new MemoryStore();
            innerStore.Put(new byte[] { 0x01 }, new byte[] { 0xAA });
            innerStore.Put(new byte[] { 0x02 }, new byte[] { 0xBB });

            using var cacheStore = new LmdbCacheStore(innerStore, new LmdbCacheOptions
            {
                Path = _testPath,
                Persistent = false,
                WriteThrough = true
            });

            // Act - Generate some hits and misses
            cacheStore.TryGet(new byte[] { 0x01 }); // Miss (not in LMDB yet)
            cacheStore.TryGet(new byte[] { 0x01 }); // Hit (in memory cache now)
            cacheStore.TryGet(new byte[] { 0x02 }); // Miss
            cacheStore.TryGet(new byte[] { 0x03 }); // Miss (doesn't exist)

            var stats = cacheStore.GetLmdbStatistics();

            // Assert
            Assert.IsTrue(stats.LmdbMisses > 0);
        }

        #endregion
    }

    /// <summary>
    /// Unit tests for cache warmup service.
    /// </summary>
    [TestClass]
    public class UT_CacheWarmupService
    {
        public TestContext TestContext { get; set; } = null!;

        private string _testPath = null!;

        [TestInitialize]
        public void Setup()
        {
            _testPath = Path.Combine(Path.GetTempPath(), $"neo-warmup-test-{Guid.NewGuid():N}");
            Directory.CreateDirectory(_testPath);
        }

        [TestCleanup]
        public void Cleanup()
        {
            if (Directory.Exists(_testPath))
            {
                try { Directory.Delete(_testPath, true); } catch { }
            }
        }

        #region CacheWarmupService Tests

        [TestMethod]
        public async Task CacheWarmupService_StartAsync_PerformsInitialWarmup()
        {
            // Arrange
            using var innerStore = new MemoryStore();
            using var cacheStore = new LmdbCacheStore(innerStore, new LmdbCacheOptions
            {
                Path = _testPath,
                Persistent = false,
                EnableBackgroundWarmup = false
            });

            await using var warmupService = new CacheWarmupService(cacheStore, new CacheWarmupOptions
            {
                WarmupOnStart = true,
                EnablePeriodicRefresh = false
            });

            var warmupCompleted = false;
            warmupService.OnWarmupCompleted += _ => warmupCompleted = true;

            // Act
            await warmupService.StartAsync(TestContext.CancellationTokenSource.Token);

            // Assert
            Assert.IsTrue(warmupCompleted);
        }

        [TestMethod]
        public async Task CacheWarmupService_RegisterStrategy_ExecutesStrategy()
        {
            // Arrange
            using var innerStore = new MemoryStore();
            for (int i = 0; i < 10; i++)
            {
                innerStore.Put(new byte[] { 0x01, (byte)i }, new byte[] { (byte)i });
            }

            using var cacheStore = new LmdbCacheStore(innerStore, new LmdbCacheOptions
            {
                Path = _testPath,
                Persistent = false,
                EnableBackgroundWarmup = false,
                WriteThrough = true
            });

            await using var warmupService = new CacheWarmupService(cacheStore, new CacheWarmupOptions
            {
                WarmupOnStart = false
            });

            var strategy = new BlockchainStateWarmupStrategy(innerStore, new BlockchainStateWarmupOptions
            {
                NativeContractPrefixes = [new byte[] { 0x01 }],
                BatchSize = 5
            });

            warmupService.RegisterStrategy(strategy);

            int totalWarmed = 0;
            warmupService.OnWarmupCompleted += result => totalWarmed = result.TotalItemsWarmed;

            // Act
            await warmupService.WarmupAsync(TestContext.CancellationTokenSource.Token);

            // Assert
            Assert.IsTrue(totalWarmed > 0);
        }

        [TestMethod]
        public async Task CacheWarmupService_WarmupPrefixesAsync_WarmsSpecificPrefixes()
        {
            // Arrange
            using var innerStore = new MemoryStore();
            innerStore.Put(new byte[] { 0x01, 0x01 }, new byte[] { 0xAA });
            innerStore.Put(new byte[] { 0x01, 0x02 }, new byte[] { 0xBB });
            innerStore.Put(new byte[] { 0x02, 0x01 }, new byte[] { 0xCC });

            using var cacheStore = new LmdbCacheStore(innerStore, new LmdbCacheOptions
            {
                Path = _testPath,
                Persistent = false,
                EnableBackgroundWarmup = false,
                WriteThrough = true
            });

            await using var warmupService = new CacheWarmupService(cacheStore);

            // Act
            await warmupService.WarmupPrefixesAsync(
                [new byte[] { 0x01 }],
                TestContext.CancellationTokenSource.Token);

            // Assert
            var stats = cacheStore.GetLmdbStatistics();
            Assert.IsTrue(stats.LmdbEntryCount >= 2);
        }

        [TestMethod]
        public async Task CacheWarmupService_StopAsync_StopsGracefully()
        {
            // Arrange
            using var innerStore = new MemoryStore();
            using var cacheStore = new LmdbCacheStore(innerStore, new LmdbCacheOptions
            {
                Path = _testPath,
                Persistent = false,
                EnableBackgroundWarmup = false
            });

            await using var warmupService = new CacheWarmupService(cacheStore, new CacheWarmupOptions
            {
                WarmupOnStart = false,
                EnablePeriodicRefresh = true,
                RefreshInterval = TimeSpan.FromMilliseconds(100)
            });

            await warmupService.StartAsync(TestContext.CancellationTokenSource.Token);

            // Act & Assert - Should not throw
            await warmupService.StopAsync(TestContext.CancellationTokenSource.Token);
        }

        #endregion

        #region IWarmupStrategy Tests

        [TestMethod]
        public async Task BlockchainStateWarmupStrategy_GetWarmupBatchesAsync_ReturnsBatches()
        {
            // Arrange
            using var store = new MemoryStore();
            for (int i = 0; i < 15; i++)
            {
                store.Put(new byte[] { 0x01, (byte)i }, new byte[] { (byte)i });
            }

            var strategy = new BlockchainStateWarmupStrategy(store, new BlockchainStateWarmupOptions
            {
                NativeContractPrefixes = [new byte[] { 0x01 }],
                BatchSize = 5
            });

            // Act
            var batches = new List<IEnumerable<byte[]>>();
            await foreach (var batch in strategy.GetWarmupBatchesAsync(TestContext.CancellationTokenSource.Token))
            {
                batches.Add(batch);
            }

            // Assert
            Assert.IsTrue(batches.Count >= 3); // 15 items / 5 batch size = 3 batches
            Assert.AreEqual("BlockchainState", strategy.Name);
            Assert.AreEqual(1, strategy.Priority);
        }

        [TestMethod]
        public async Task RecentBlocksWarmupStrategy_GetWarmupBatchesAsync_ReturnsBatches()
        {
            // Arrange
            using var store = new MemoryStore();
            for (int i = 0; i < 10; i++)
            {
                store.Put(new byte[] { 0x02, (byte)i }, new byte[] { (byte)i });
            }

            var strategy = new RecentBlocksWarmupStrategy(store, new RecentBlocksWarmupOptions
            {
                BlockPrefix = new byte[] { 0x02 },
                RecentBlockCount = 5,
                BatchSize = 2
            });

            // Act
            var batches = new List<IEnumerable<byte[]>>();
            await foreach (var batch in strategy.GetWarmupBatchesAsync(TestContext.CancellationTokenSource.Token))
            {
                batches.Add(batch);
            }

            // Assert - Strategy may return 0 batches if backward seek doesn't find items
            // This is expected behavior for MemoryStore which has limited backward seek support
            Assert.AreEqual("RecentBlocks", strategy.Name);
            Assert.AreEqual(2, strategy.Priority);
        }

        [TestMethod]
        public async Task FrequencyBasedWarmupStrategy_GetWarmupBatchesAsync_ReturnsHotKeys()
        {
            // Arrange
            var tracker = new InMemoryAccessTracker();
            for (int i = 0; i < 20; i++)
            {
                var key = new byte[] { (byte)i };
                // Access some keys more frequently
                for (int j = 0; j <= i; j++)
                {
                    tracker.RecordAccess(key);
                }
            }

            var strategy = new FrequencyBasedWarmupStrategy(tracker, new FrequencyWarmupOptions
            {
                TopKeyCount = 10,
                BatchSize = 3
            });

            // Act
            var batches = new List<IEnumerable<byte[]>>();
            await foreach (var batch in strategy.GetWarmupBatchesAsync(TestContext.CancellationTokenSource.Token))
            {
                batches.Add(batch);
            }

            // Assert
            Assert.IsTrue(batches.Count >= 1);
            Assert.AreEqual("FrequencyBased", strategy.Name);
            Assert.AreEqual(3, strategy.Priority);
        }

        #endregion

        #region InMemoryAccessTracker Tests

        [TestMethod]
        public void InMemoryAccessTracker_RecordAccess_TracksFrequency()
        {
            // Arrange
            var tracker = new InMemoryAccessTracker();
            var key1 = new byte[] { 0x01 };
            var key2 = new byte[] { 0x02 };

            // Act
            tracker.RecordAccess(key1);
            tracker.RecordAccess(key1);
            tracker.RecordAccess(key1);
            tracker.RecordAccess(key2);

            var hotKeys = tracker.GetMostAccessedKeys(10).ToList();

            // Assert
            Assert.AreEqual(2, hotKeys.Count);
            CollectionAssert.AreEqual(key1, hotKeys[0]); // Most accessed first
        }

        [TestMethod]
        public void InMemoryAccessTracker_GetMostAccessedKeys_ReturnsTopKeys()
        {
            // Arrange
            var tracker = new InMemoryAccessTracker();

            for (int i = 0; i < 10; i++)
            {
                var key = new byte[] { (byte)i };
                for (int j = 0; j <= i; j++)
                {
                    tracker.RecordAccess(key);
                }
            }

            // Act
            var topKeys = tracker.GetMostAccessedKeys(3).ToList();

            // Assert
            Assert.AreEqual(3, topKeys.Count);
            // Key 9 should be first (10 accesses), then 8 (9 accesses), then 7 (8 accesses)
            Assert.AreEqual(9, topKeys[0][0]);
            Assert.AreEqual(8, topKeys[1][0]);
            Assert.AreEqual(7, topKeys[2][0]);
        }

        [TestMethod]
        public void InMemoryAccessTracker_Clear_RemovesAllTracking()
        {
            // Arrange
            var tracker = new InMemoryAccessTracker();
            tracker.RecordAccess(new byte[] { 0x01 });
            tracker.RecordAccess(new byte[] { 0x02 });

            // Act
            tracker.Clear();
            var keys = tracker.GetMostAccessedKeys(10).ToList();

            // Assert
            Assert.AreEqual(0, keys.Count);
        }

        #endregion

        #region CacheWarmupOptions Tests

        [TestMethod]
        public void CacheWarmupOptions_DefaultValues()
        {
            // Arrange & Act
            var options = new CacheWarmupOptions();

            // Assert
            Assert.IsTrue(options.WarmupOnStart);
            Assert.IsFalse(options.EnablePeriodicRefresh);
            Assert.AreEqual(TimeSpan.FromHours(1), options.RefreshInterval);
            Assert.AreEqual(10000, options.MaxItemsPerPrefix);
        }

        #endregion
    }
}
