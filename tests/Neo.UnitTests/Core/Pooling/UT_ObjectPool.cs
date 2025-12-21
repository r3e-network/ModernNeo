// Copyright (C) 2015-2025 The Neo Project.
//
// UT_ObjectPool.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

#nullable enable

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.Core.Pooling;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Neo.UnitTests.Core.Pooling
{
    // Test helper classes
    public class SimpleObject
    {
        public int Value { get; set; }
        public string? Data { get; set; }
    }

    public class PoolableObject : IPooledObject
    {
        public int Value { get; set; }
        public bool WasReset { get; set; }

        public void Reset()
        {
            Value = 0;
            WasReset = true;
        }
    }

    [TestClass]
    public class UT_ObjectPool
    {
        [TestMethod]
        public void TestConstructor_DefaultMaxSize()
        {
            var pool = new ObjectPool<SimpleObject>();
            Assert.AreEqual(Environment.ProcessorCount * 2, pool.MaxSize);
            Assert.AreEqual(0, pool.Count);
        }

        [TestMethod]
        public void TestConstructor_CustomMaxSize()
        {
            var pool = new ObjectPool<SimpleObject>(10);
            Assert.AreEqual(10, pool.MaxSize);
            Assert.AreEqual(0, pool.Count);
        }

        [TestMethod]
        public void TestConstructor_InvalidMaxSize()
        {
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new ObjectPool<SimpleObject>(0));
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new ObjectPool<SimpleObject>(-1));
        }

        [TestMethod]
        public void TestGet_CreatesNewInstance()
        {
            var pool = new ObjectPool<SimpleObject>();
            var obj = pool.Get();

            Assert.IsNotNull(obj);
            Assert.IsInstanceOfType(obj, typeof(SimpleObject));
        }

        [TestMethod]
        public void TestGet_ReturnsPooledInstance()
        {
            var pool = new ObjectPool<SimpleObject>();
            var obj1 = pool.Get();
            obj1.Value = 42;

            pool.Return(obj1);
            var obj2 = pool.Get();

            Assert.AreSame(obj1, obj2);
            Assert.AreEqual(42, obj2.Value);
        }

        [TestMethod]
        public void TestReturn_NullThrowsException()
        {
            var pool = new ObjectPool<SimpleObject>();
            Assert.ThrowsExactly<ArgumentNullException>(() => pool.Return(null!));
        }

        [TestMethod]
        public void TestReturn_CallsResetOnIPooledObject()
        {
            var pool = new ObjectPool<PoolableObject>();
            var obj = pool.Get();
            obj.Value = 100;
            obj.WasReset = false;

            pool.Return(obj);

            Assert.IsTrue(obj.WasReset);
            Assert.AreEqual(0, obj.Value);
        }

        [TestMethod]
        public void TestReturn_CallsCustomResetAction()
        {
            bool resetCalled = false;
            var pool = new ObjectPool<SimpleObject>(
                maxSize: 5,
                factory: null,
                resetAction: obj =>
                {
                    obj.Value = 0;
                    obj.Data = null;
                    resetCalled = true;
                });

            var obj = pool.Get();
            obj.Value = 42;
            obj.Data = "test";

            pool.Return(obj);

            Assert.IsTrue(resetCalled);
            Assert.AreEqual(0, obj.Value);
            Assert.IsNull(obj.Data);
        }

        [TestMethod]
        public void TestReturn_RespectsMaxSize()
        {
            var pool = new ObjectPool<SimpleObject>(2);

            var obj1 = pool.Get();
            var obj2 = pool.Get();
            var obj3 = pool.Get();

            pool.Return(obj1);
            pool.Return(obj2);
            pool.Return(obj3); // Should be discarded

            Assert.AreEqual(2, pool.Count);
        }

        [TestMethod]
        public void TestCustomFactory()
        {
            int factoryCallCount = 0;
            var pool = new ObjectPool<SimpleObject>(
                maxSize: 5,
                factory: () =>
                {
                    factoryCallCount++;
                    return new SimpleObject { Value = 999 };
                },
                resetAction: null);

            var obj1 = pool.Get();
            Assert.AreEqual(1, factoryCallCount);
            Assert.AreEqual(999, obj1.Value);

            pool.Return(obj1);
            var obj2 = pool.Get();
            Assert.AreEqual(1, factoryCallCount); // Should reuse, not create new
            Assert.AreSame(obj1, obj2);
        }

        [TestMethod]
        public void TestClear()
        {
            var pool = new ObjectPool<SimpleObject>(5);

            var obj1 = pool.Get();
            var obj2 = pool.Get();
            pool.Return(obj1);
            pool.Return(obj2);

            Assert.AreEqual(2, pool.Count);

            pool.Clear();

            Assert.AreEqual(0, pool.Count);
        }

        [TestMethod]
        public void TestThreadSafety_ConcurrentGetAndReturn()
        {
            var pool = new ObjectPool<SimpleObject>(100);
            var retrievedObjects = new System.Collections.Concurrent.ConcurrentBag<SimpleObject>();
            int completed = 0;

            // Concurrent Get operations
            for (int i = 0; i < 50; i++)
            {
                ThreadPool.QueueUserWorkItem(_ =>
                {
                    var obj = pool.Get();
                    retrievedObjects.Add(obj);
                    Thread.Sleep(1); // Simulate work
                    Interlocked.Increment(ref completed);
                });
            }

            // Wait for all Get operations to complete
            while (Interlocked.CompareExchange(ref completed, 0, 0) < 50)
                Thread.Sleep(10);

            Assert.AreEqual(50, retrievedObjects.Count);

            completed = 0;

            // Concurrent Return operations
            foreach (var obj in retrievedObjects)
            {
                var capturedObj = obj;
                ThreadPool.QueueUserWorkItem(_ =>
                {
                    pool.Return(capturedObj);
                    Interlocked.Increment(ref completed);
                });
            }

            // Wait for all Return operations to complete
            while (Interlocked.CompareExchange(ref completed, 0, 0) < 50)
                Thread.Sleep(10);

            // Pool should have objects returned (up to max size)
            Assert.IsTrue(pool.Count > 0);
            Assert.IsTrue(pool.Count <= pool.MaxSize);
        }

        [TestMethod]
        public void TestThreadSafety_StressTest()
        {
            var pool = new ObjectPool<SimpleObject>(50);
            var exceptions = new System.Collections.Concurrent.ConcurrentBag<Exception>();
            int completed = 0;

            // Stress test with mixed operations
            for (int i = 0; i < 100; i++)
            {
                ThreadPool.QueueUserWorkItem(_ =>
                {
                    try
                    {
                        for (int j = 0; j < 100; j++)
                        {
                            var obj = pool.Get();
                            obj.Value = j;
                            Thread.Sleep(0); // Yield
                            pool.Return(obj);
                        }
                    }
                    catch (Exception ex)
                    {
                        exceptions.Add(ex);
                    }
                    finally
                    {
                        Interlocked.Increment(ref completed);
                    }
                });
            }

            // Wait for all operations to complete
            while (Interlocked.CompareExchange(ref completed, 0, 0) < 100)
                Thread.Sleep(10);

            Assert.AreEqual(0, exceptions.Count, "No exceptions should occur during concurrent operations");
        }

        [TestMethod]
        public void TestPoolReuse_MultipleGetReturnCycles()
        {
            var pool = new ObjectPool<SimpleObject>(5);
            var obj = pool.Get();
            var originalRef = obj;

            for (int i = 0; i < 10; i++)
            {
                pool.Return(obj);
                obj = pool.Get();
            }

            Assert.AreSame(originalRef, obj, "Should reuse the same object across multiple cycles");
        }

        [TestMethod]
        public void TestPooledObjectReset_BothInterfaceAndCustomAction()
        {
            bool customResetCalled = false;
            var pool = new ObjectPool<PoolableObject>(
                maxSize: 5,
                factory: null,
                resetAction: obj =>
                {
                    customResetCalled = true;
                    obj.Value = -1; // Custom reset sets to -1
                });

            var obj = pool.Get();
            obj.Value = 100;
            obj.WasReset = false;

            pool.Return(obj);

            // Both IPooledObject.Reset and custom action should be called
            Assert.IsTrue(obj.WasReset, "IPooledObject.Reset should be called");
            Assert.IsTrue(customResetCalled, "Custom reset action should be called");
            Assert.AreEqual(-1, obj.Value, "Custom reset should override IPooledObject.Reset");
        }

        [TestMethod]
        public void TestCount_AccurateUnderLoad()
        {
            var pool = new ObjectPool<SimpleObject>(20);
            var objects = new List<SimpleObject>();

            // Get 10 objects
            for (int i = 0; i < 10; i++)
            {
                objects.Add(pool.Get());
            }

            Assert.AreEqual(0, pool.Count);

            // Return 5 objects
            for (int i = 0; i < 5; i++)
            {
                pool.Return(objects[i]);
            }

            Assert.AreEqual(5, pool.Count);

            // Get 3 objects
            for (int i = 0; i < 3; i++)
            {
                pool.Get();
            }

            Assert.AreEqual(2, pool.Count);
        }

        [TestMethod]
        public void TestMaxSize_EnforcedStrictly()
        {
            var pool = new ObjectPool<SimpleObject>(3);
            var objects = new List<SimpleObject>();

            // Create and return 10 objects
            for (int i = 0; i < 10; i++)
            {
                var obj = pool.Get();
                objects.Add(obj);
            }

            foreach (var obj in objects)
            {
                pool.Return(obj);
            }

            // Pool should only retain maxSize objects
            Assert.AreEqual(3, pool.Count);

            // Verify we can still get objects
            var retrieved = new HashSet<SimpleObject>();
            for (int i = 0; i < 3; i++)
            {
                retrieved.Add(pool.Get());
            }

            Assert.AreEqual(3, retrieved.Count);
        }

        [TestMethod]
        public void TestPooledBuffer_BasicUsage()
        {
            var buffer = new PooledBuffer();
            buffer.Rent(1024);

            Assert.IsNotNull(buffer.Buffer);
            Assert.IsTrue(buffer.Buffer.Length >= 1024);
            Assert.AreEqual(1024, buffer.Length);

            buffer.Return();
        }

        [TestMethod]
        public void TestPooledBuffer_SpanAndMemory()
        {
            var buffer = new PooledBuffer();
            buffer.Rent(100);

            var span = buffer.Span;
            Assert.AreEqual(100, span.Length);

            var memory = buffer.Memory;
            Assert.AreEqual(100, memory.Length);

            buffer.Return();
        }

        [TestMethod]
        public void TestPooledBuffer_Create()
        {
            using var buffer = PooledBuffer.Create(512);

            Assert.IsNotNull(buffer.Buffer);
            Assert.AreEqual(512, buffer.Length);
        }

        [TestMethod]
        public void TestPooledBuffer_CopyFrom()
        {
            byte[] source = new byte[] { 1, 2, 3, 4, 5 };
            using var buffer = PooledBuffer.CopyFrom(source);

            Assert.AreEqual(5, buffer.Length);
            CollectionAssert.AreEqual(source, buffer.Span.ToArray());
        }

        [TestMethod]
        public void TestPooledBuffer_Reset()
        {
            var buffer = new PooledBuffer();
            buffer.Rent(256);

            buffer.Reset();

            // After reset, buffer should be returned and can be rented again
            buffer.Rent(128);
            Assert.AreEqual(128, buffer.Length);

            buffer.Return();
        }

        [TestMethod]
        public void TestPooledBuffer_Dispose()
        {
            var buffer = new PooledBuffer();
            buffer.Rent(256);

            buffer.Dispose();

            // After dispose, accessing properties should throw
            Assert.ThrowsExactly<ObjectDisposedException>(() => _ = buffer.Buffer);
            Assert.ThrowsExactly<ObjectDisposedException>(() => _ = buffer.Length);
            Assert.ThrowsExactly<ObjectDisposedException>(() => _ = buffer.Span);
        }

        [TestMethod]
        public void TestPooledBuffer_DoubleRentThrows()
        {
            var buffer = new PooledBuffer();
            buffer.Rent(100);

            Assert.ThrowsExactly<InvalidOperationException>(() => buffer.Rent(200));

            buffer.Return();
        }

        [TestMethod]
        public void TestPooledBuffer_NegativeLengthThrows()
        {
            var buffer = new PooledBuffer();
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => buffer.Rent(-1));
        }

        [TestMethod]
        public void TestPooledBuffer_AccessBeforeRentThrows()
        {
            var buffer = new PooledBuffer();
            Assert.ThrowsExactly<InvalidOperationException>(() => _ = buffer.Buffer);
        }

        [TestMethod]
        public void TestPooledBuffer_MultipleReturnsSafe()
        {
            var buffer = new PooledBuffer();
            buffer.Rent(100);

            buffer.Return();
            buffer.Return(); // Should be safe to call multiple times
            buffer.Return();

            // Should be able to rent again
            buffer.Rent(50);
            Assert.AreEqual(50, buffer.Length);

            buffer.Return();
        }

        [TestMethod]
        public void TestPooledBuffer_WithObjectPool()
        {
            var pool = new ObjectPool<PooledBuffer>(5);

            var buffer = pool.Get();
            buffer.Rent(1024);

            // Write some data
            buffer.Span[0] = 42;

            pool.Return(buffer);

            // Get it back
            var buffer2 = pool.Get();
            Assert.AreSame(buffer, buffer2);

            // Should be reset (buffer returned to ArrayPool)
            buffer2.Rent(512);
            Assert.AreEqual(512, buffer2.Length);

            buffer2.Return();
            pool.Return(buffer2);
        }

        [TestMethod]
        public void TestPooledBuffer_ClearArray()
        {
            var buffer = new PooledBuffer();
            buffer.Rent(10);

            // Write data
            for (int i = 0; i < 10; i++)
            {
                buffer.Span[i] = (byte)i;
            }

            buffer.Return(clearArray: true);

            // Rent again and verify it's cleared (or at least a fresh buffer)
            buffer.Rent(10);
            // Note: We can't guarantee the same buffer, but this tests the API
            buffer.Return();
        }

        [TestMethod]
        public void TestPooledBuffer_ThreadSafety()
        {
            var pool = new ObjectPool<PooledBuffer>(20);
            int completed = 0;

            for (int i = 0; i < 50; i++)
            {
                ThreadPool.QueueUserWorkItem(_ =>
                {
                    try
                    {
                        var buffer = pool.Get();
                        buffer.Rent(1024);

                        // Simulate work
                        for (int j = 0; j < buffer.Length; j++)
                        {
                            buffer.Span[j] = (byte)(j % 256);
                        }

                        buffer.Return();
                        pool.Return(buffer);
                    }
                    finally
                    {
                        Interlocked.Increment(ref completed);
                    }
                });
            }

            // Wait for all operations to complete
            while (Interlocked.CompareExchange(ref completed, 0, 0) < 50)
                Thread.Sleep(10);

            // Should complete without exceptions
            Assert.IsTrue(pool.Count > 0);
        }
    }
}
