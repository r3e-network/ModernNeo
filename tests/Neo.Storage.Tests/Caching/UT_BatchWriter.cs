// Copyright (C) 2015-2025 The Neo Project.
//
// UT_BatchWriter.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.Persistence;
using Neo.Persistence.Caching;
using Neo.Persistence.Providers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Neo.Storage.Tests.Caching
{
    [TestClass]
    public class UT_BatchWriter
    {
        private MemoryStore _store = null!;
        private BatchWriter _batchWriter = null!;

        [TestInitialize]
        public void Setup()
        {
            _store = new MemoryStore();
        }

        [TestCleanup]
        public async Task Cleanup()
        {
            if (_batchWriter != null)
            {
                await _batchWriter.DisposeAsync();
            }
            _store?.Dispose();
        }

        [TestMethod]
        public void TestBasicPut()
        {
            var options = new BatchWriterOptions { BatchSize = 10 };
            _batchWriter = new BatchWriter(_store, options);

            var key = new byte[] { 1, 2, 3 };
            var value = new byte[] { 4, 5, 6 };

            _batchWriter.Put(key, value);

            Assert.AreEqual(1, _batchWriter.PendingCount);
        }

        [TestMethod]
        public void TestBasicDelete()
        {
            var options = new BatchWriterOptions { BatchSize = 10 };
            _batchWriter = new BatchWriter(_store, options);

            var key = new byte[] { 1, 2, 3 };

            _batchWriter.Delete(key);

            Assert.AreEqual(1, _batchWriter.PendingCount);
        }

        [TestMethod]
        public async Task TestAutoFlushOnBatchSize()
        {
            var options = new BatchWriterOptions { BatchSize = 3, FlushInterval = TimeSpan.FromSeconds(10) };
            _batchWriter = new BatchWriter(_store, options);

            // Add 3 items to trigger auto-flush
            _batchWriter.Put(new byte[] { 1 }, new byte[] { 10 });
            _batchWriter.Put(new byte[] { 2 }, new byte[] { 20 });
            _batchWriter.Put(new byte[] { 3 }, new byte[] { 30 });

            // Wait for flush to complete
            await Task.Delay(100);

            // Verify data was written to store
            Assert.IsTrue(_store.TryGet(new byte[] { 1 }, out var value));
            Assert.AreEqual(10, value[0]);
        }

        [TestMethod]
        public async Task TestManualFlush()
        {
            var options = new BatchWriterOptions { BatchSize = 100 };
            _batchWriter = new BatchWriter(_store, options);

            _batchWriter.Put(new byte[] { 1 }, new byte[] { 10 });
            _batchWriter.Put(new byte[] { 2 }, new byte[] { 20 });

            Assert.AreEqual(2, _batchWriter.PendingCount);

            await _batchWriter.FlushAsync();

            Assert.AreEqual(0, _batchWriter.PendingCount);

            // Verify data was written
            Assert.IsTrue(_store.TryGet(new byte[] { 1 }, out var value1));
            Assert.AreEqual(10, value1[0]);

            Assert.IsTrue(_store.TryGet(new byte[] { 2 }, out var value2));
            Assert.AreEqual(20, value2[0]);
        }

        [TestMethod]
        public async Task TestPutAsync()
        {
            var options = new BatchWriterOptions { BatchSize = 100 };
            _batchWriter = new BatchWriter(_store, options);

            var key = new byte[] { 1, 2, 3 };
            var value = new byte[] { 4, 5, 6 };

            var task = _batchWriter.PutAsync(key, value);

            // Manually flush to complete the async operation
            await _batchWriter.FlushAsync();

            await task;

            // Verify data was written
            Assert.IsTrue(_store.TryGet(key, out var result));
            CollectionAssert.AreEqual(value, result);
        }

        [TestMethod]
        public async Task TestDeleteAsync()
        {
            var options = new BatchWriterOptions { BatchSize = 100 };
            _batchWriter = new BatchWriter(_store, options);

            var key = new byte[] { 1, 2, 3 };
            var value = new byte[] { 4, 5, 6 };

            // First put the value
            _store.Put(key, value);
            Assert.IsTrue(_store.Contains(key));

            // Now delete it
            var task = _batchWriter.DeleteAsync(key);
            await _batchWriter.FlushAsync();
            await task;

            // Verify it was deleted
            Assert.IsFalse(_store.Contains(key));
        }

        [TestMethod]
        public async Task TestStatistics()
        {
            var options = new BatchWriterOptions { BatchSize = 100 };
            _batchWriter = new BatchWriter(_store, options);

            _batchWriter.Put(new byte[] { 1 }, new byte[] { 10 });
            _batchWriter.Put(new byte[] { 2 }, new byte[] { 20 });
            _batchWriter.Put(new byte[] { 3 }, new byte[] { 30 });

            await _batchWriter.FlushAsync();

            var stats = _batchWriter.GetStatistics();

            Assert.AreEqual(0, stats.PendingCount);
            Assert.AreEqual(3, stats.TotalWrites);
            Assert.AreEqual(1, stats.TotalBatches);
            Assert.AreEqual(3.0, stats.AverageBatchSize);
        }

        [TestMethod]
        public async Task TestMultipleBatches()
        {
            var options = new BatchWriterOptions { BatchSize = 2 };
            _batchWriter = new BatchWriter(_store, options);

            // First batch
            _batchWriter.Put(new byte[] { 1 }, new byte[] { 10 });
            _batchWriter.Put(new byte[] { 2 }, new byte[] { 20 });

            await Task.Delay(100); // Wait for auto-flush

            // Second batch
            _batchWriter.Put(new byte[] { 3 }, new byte[] { 30 });
            _batchWriter.Put(new byte[] { 4 }, new byte[] { 40 });

            await Task.Delay(100); // Wait for auto-flush

            var stats = _batchWriter.GetStatistics();

            Assert.AreEqual(4, stats.TotalWrites);
            Assert.AreEqual(2, stats.TotalBatches);
            Assert.AreEqual(2.0, stats.AverageBatchSize);
        }

        [TestMethod]
        public async Task TestBatchFlushedEvent()
        {
            var options = new BatchWriterOptions { BatchSize = 100 };
            _batchWriter = new BatchWriter(_store, options);

            int flushedCount = 0;
            int itemsInBatch = 0;

            _batchWriter.OnBatchFlushed += (count) =>
            {
                flushedCount++;
                itemsInBatch = count;
            };

            _batchWriter.Put(new byte[] { 1 }, new byte[] { 10 });
            _batchWriter.Put(new byte[] { 2 }, new byte[] { 20 });
            _batchWriter.Put(new byte[] { 3 }, new byte[] { 30 });

            await _batchWriter.FlushAsync();

            Assert.AreEqual(1, flushedCount);
            Assert.AreEqual(3, itemsInBatch);
        }

        [TestMethod]
        public async Task TestConcurrentWrites()
        {
            var options = new BatchWriterOptions { BatchSize = 1000 };
            _batchWriter = new BatchWriter(_store, options);

            var tasks = new List<Task>();

            // Concurrent writes
            for (int i = 0; i < 100; i++)
            {
                int key = i;
                tasks.Add(Task.Run(() => _batchWriter.Put(new byte[] { (byte)key }, new byte[] { (byte)(key * 2) })));
            }

            await Task.WhenAll(tasks);
            await _batchWriter.FlushAsync();

            // Verify all writes
            for (int i = 0; i < 100; i++)
            {
                Assert.IsTrue(_store.TryGet(new byte[] { (byte)i }, out var value));
                Assert.AreEqual((byte)(i * 2), value[0]);
            }
        }

        [TestMethod]
        public async Task TestMixedOperations()
        {
            var options = new BatchWriterOptions { BatchSize = 100 };
            _batchWriter = new BatchWriter(_store, options);

            var key1 = new byte[] { 1 };
            var key2 = new byte[] { 2 };
            var value1 = new byte[] { 10 };
            var value2 = new byte[] { 20 };

            // Put some values
            _batchWriter.Put(key1, value1);
            _batchWriter.Put(key2, value2);

            await _batchWriter.FlushAsync();

            // Verify they exist
            Assert.IsTrue(_store.Contains(key1));
            Assert.IsTrue(_store.Contains(key2));

            // Now delete one
            _batchWriter.Delete(key1);

            await _batchWriter.FlushAsync();

            // Verify deletion
            Assert.IsFalse(_store.Contains(key1));
            Assert.IsTrue(_store.Contains(key2));
        }

        [TestMethod]
        public async Task TestFlushEmptyBatch()
        {
            var options = new BatchWriterOptions { BatchSize = 100 };
            _batchWriter = new BatchWriter(_store, options);

            // Flush with no pending operations
            await _batchWriter.FlushAsync();

            var stats = _batchWriter.GetStatistics();
            Assert.AreEqual(0, stats.TotalWrites);
            Assert.AreEqual(0, stats.TotalBatches);
        }

        [TestMethod]
        public async Task TestDisposeFlushesData()
        {
            var options = new BatchWriterOptions { BatchSize = 100 };
            _batchWriter = new BatchWriter(_store, options);

            _batchWriter.Put(new byte[] { 1 }, new byte[] { 10 });
            _batchWriter.Put(new byte[] { 2 }, new byte[] { 20 });

            Assert.AreEqual(2, _batchWriter.PendingCount);

            // Dispose should flush pending data
            await _batchWriter.DisposeAsync();

            // Verify data was written
            Assert.IsTrue(_store.TryGet(new byte[] { 1 }, out var value1));
            Assert.AreEqual(10, value1[0]);

            Assert.IsTrue(_store.TryGet(new byte[] { 2 }, out var value2));
            Assert.AreEqual(20, value2[0]);
        }

        [TestMethod]
        [ExpectedException(typeof(ObjectDisposedException))]
        public async Task TestOperationAfterDispose()
        {
            var options = new BatchWriterOptions { BatchSize = 100 };
            _batchWriter = new BatchWriter(_store, options);

            await _batchWriter.DisposeAsync();

            // This should throw ObjectDisposedException
            _batchWriter.Put(new byte[] { 1 }, new byte[] { 10 });
        }

        [TestMethod]
        public async Task TestCancellationToken()
        {
            var options = new BatchWriterOptions { BatchSize = 100 };
            _batchWriter = new BatchWriter(_store, options);

            var cts = new CancellationTokenSource();
            cts.Cancel();

            try
            {
                await _batchWriter.PutAsync(new byte[] { 1 }, new byte[] { 10 }, cts.Token);
                Assert.Fail("Should have thrown OperationCanceledException");
            }
            catch (OperationCanceledException)
            {
                // Expected
            }
        }

        [TestMethod]
        public async Task TestUpdateExistingKey()
        {
            var options = new BatchWriterOptions { BatchSize = 100 };
            _batchWriter = new BatchWriter(_store, options);

            var key = new byte[] { 1 };

            // First write
            _batchWriter.Put(key, new byte[] { 10 });
            await _batchWriter.FlushAsync();

            Assert.IsTrue(_store.TryGet(key, out var value1));
            Assert.AreEqual(10, value1[0]);

            // Update
            _batchWriter.Put(key, new byte[] { 20 });
            await _batchWriter.FlushAsync();

            Assert.IsTrue(_store.TryGet(key, out var value2));
            Assert.AreEqual(20, value2[0]);
        }

        [TestMethod]
        public async Task TestLargeDataWrite()
        {
            var options = new BatchWriterOptions { BatchSize = 10 };
            _batchWriter = new BatchWriter(_store, options);

            var largeValue = new byte[10000];
            for (int i = 0; i < largeValue.Length; i++)
            {
                largeValue[i] = (byte)(i % 256);
            }

            _batchWriter.Put(new byte[] { 1 }, largeValue);
            await _batchWriter.FlushAsync();

            Assert.IsTrue(_store.TryGet(new byte[] { 1 }, out var result));
            Assert.AreEqual(10000, result.Length);
            CollectionAssert.AreEqual(largeValue, result);
        }

        [TestMethod]
        public async Task TestTimerBasedFlush()
        {
            var options = new BatchWriterOptions
            {
                BatchSize = 1000,
                FlushInterval = TimeSpan.FromMilliseconds(100)
            };
            _batchWriter = new BatchWriter(_store, options);

            _batchWriter.Put(new byte[] { 1 }, new byte[] { 10 });

            // Wait for timer-based flush
            await Task.Delay(200);

            // Data should be flushed by timer
            Assert.IsTrue(_store.TryGet(new byte[] { 1 }, out var value));
            Assert.AreEqual(10, value[0]);
        }

        [TestMethod]
        public async Task TestZeroAverageBatchSizeInitially()
        {
            var options = new BatchWriterOptions { BatchSize = 100 };
            _batchWriter = new BatchWriter(_store, options);

            var stats = _batchWriter.GetStatistics();

            Assert.AreEqual(0, stats.TotalWrites);
            Assert.AreEqual(0, stats.TotalBatches);
            Assert.AreEqual(0.0, stats.AverageBatchSize);
        }
    }
}
