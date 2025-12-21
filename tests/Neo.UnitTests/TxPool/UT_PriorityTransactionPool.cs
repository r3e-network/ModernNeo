// Copyright (C) 2015-2025 The Neo Project.
//
// UT_PriorityTransactionPool.cs file belongs to the neo project and is free
// software distributed under the MIT software license.

#nullable enable

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Neo.Core.Interfaces;
using Neo.TxPool;

namespace Neo.UnitTests.TxPool;

[TestClass]
public class UT_PriorityTransactionPool
{
    private PriorityTransactionPool _pool = null!;
    private PriorityPoolOptions _options = null!;

    [TestInitialize]
    public void Setup()
    {
        _options = new PriorityPoolOptions
        {
            MaxPoolSize = 100,
            HighPriorityThreshold = 1000,
            NormalPriorityThreshold = 100
        };
        _pool = new PriorityTransactionPool(_options);
    }

    [TestCleanup]
    public void Cleanup()
    {
        _pool.Dispose();
    }

    [TestMethod]
    public void TestConstructor_DefaultOptions()
    {
        using var pool = new PriorityTransactionPool();
        Assert.AreEqual(50_000, pool.Capacity);
        Assert.AreEqual(0, pool.Count);
    }

    [TestMethod]
    public void TestConstructor_CustomOptions()
    {
        Assert.AreEqual(100, _pool.Capacity);
        Assert.AreEqual(0, _pool.Count);
    }

    [TestMethod]
    public void TestTryAdd_ValidTransaction()
    {
        var tx = CreateMockTransaction(1, 500);
        var result = _pool.TryAdd(tx);

        Assert.IsTrue(result);
        Assert.AreEqual(1, _pool.Count);
    }

    [TestMethod]
    public void TestTryAdd_NullTransaction_ThrowsException()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => _pool.TryAdd(null!));
    }

    [TestMethod]
    public void TestContains_NonExistingTransaction()
    {
        Assert.IsFalse(_pool.Contains(new byte[] { 1, 2, 3 }));
    }

    [TestMethod]
    public void TestTryGet_NonExistingTransaction()
    {
        var result = _pool.TryGet(new byte[] { 1, 2, 3 }, out var retrieved);

        Assert.IsFalse(result);
        Assert.IsNull(retrieved);
    }

    [TestMethod]
    public void TestTryRemove_NonExistingTransaction()
    {
        var result = _pool.TryRemove(new byte[] { 1, 2, 3 });
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void TestClear()
    {
        _pool.TryAdd(CreateMockTransaction(1, 500));
        _pool.TryAdd(CreateMockTransaction(2, 600));
        _pool.TryAdd(CreateMockTransaction(3, 700));

        Assert.AreEqual(3, _pool.Count);

        _pool.Clear();

        Assert.AreEqual(0, _pool.Count);
    }

    [TestMethod]
    public void TestPriorityQueues_HighPriority()
    {
        var tx = CreateMockTransaction(1, 2000); // Above high threshold
        _pool.TryAdd(tx);

        Assert.AreEqual(1, _pool.HighPriorityCount);
        Assert.AreEqual(0, _pool.NormalPriorityCount);
        Assert.AreEqual(0, _pool.LowPriorityCount);
    }

    [TestMethod]
    public void TestPriorityQueues_NormalPriority()
    {
        var tx = CreateMockTransaction(1, 500); // Between normal and high
        _pool.TryAdd(tx);

        Assert.AreEqual(0, _pool.HighPriorityCount);
        Assert.AreEqual(1, _pool.NormalPriorityCount);
        Assert.AreEqual(0, _pool.LowPriorityCount);
    }

    [TestMethod]
    public void TestPriorityQueues_LowPriority()
    {
        var tx = CreateMockTransaction(1, 50); // Below normal threshold
        _pool.TryAdd(tx);

        Assert.AreEqual(0, _pool.HighPriorityCount);
        Assert.AreEqual(0, _pool.NormalPriorityCount);
        Assert.AreEqual(1, _pool.LowPriorityCount);
    }

    [TestMethod]
    public void TestGetVerifiedTransactions()
    {
        _pool.TryAdd(CreateMockTransaction(1, 500));
        _pool.TryAdd(CreateMockTransaction(2, 600));

        var verified = _pool.GetVerifiedTransactions(10).ToList();

        Assert.AreEqual(2, verified.Count);
    }

    [TestMethod]
    public void TestGetVerifiedTransactions_WithLimit()
    {
        _pool.TryAdd(CreateMockTransaction(1, 500));
        _pool.TryAdd(CreateMockTransaction(2, 600));
        _pool.TryAdd(CreateMockTransaction(3, 700));

        var verified = _pool.GetVerifiedTransactions(2).ToList();

        Assert.AreEqual(2, verified.Count);
    }

    [TestMethod]
    public void TestGetStatistics()
    {
        _pool.TryAdd(CreateMockTransaction(1, 2000)); // High
        _pool.TryAdd(CreateMockTransaction(2, 500));  // Normal
        _pool.TryAdd(CreateMockTransaction(3, 50));   // Low

        var stats = _pool.GetStatistics();

        Assert.AreEqual(3, stats.TotalCount);
        Assert.AreEqual(1, stats.HighPriorityCount);
        Assert.AreEqual(1, stats.NormalPriorityCount);
        Assert.AreEqual(1, stats.LowPriorityCount);
        Assert.AreEqual(100, stats.Capacity);
    }

    [TestMethod]
    public void TestCapacityEviction()
    {
        var smallOptions = new PriorityPoolOptions { MaxPoolSize = 3 };
        using var smallPool = new PriorityTransactionPool(smallOptions);

        // Add 3 low priority transactions
        smallPool.TryAdd(CreateMockTransaction(1, 10));
        smallPool.TryAdd(CreateMockTransaction(2, 20));
        smallPool.TryAdd(CreateMockTransaction(3, 30));

        Assert.AreEqual(3, smallPool.Count);

        // Add a 4th - should evict lowest priority
        smallPool.TryAdd(CreateMockTransaction(4, 40));

        Assert.AreEqual(3, smallPool.Count);
    }

    [TestMethod]
    public void TestTransactionAddedEvent()
    {
        var eventRaised = false;
        _pool.TransactionAdded += (_, _) => eventRaised = true;

        _pool.TryAdd(CreateMockTransaction(1, 500));

        Assert.IsTrue(eventRaised);
    }

    [TestMethod]
    public void TestDispose_PreventsFurtherOperations()
    {
        _pool.Dispose();

        Assert.ThrowsExactly<ObjectDisposedException>(() =>
            _pool.TryAdd(CreateMockTransaction(1, 500)));
    }

    private static ITransactionData CreateMockTransaction(int id, long feePerByte)
    {
        // Create a real UInt256 hash (32 bytes)
        var hashBytes = new byte[32];
        hashBytes[0] = (byte)id;
        hashBytes[1] = (byte)(id >> 8);
        var hash = new UInt256(hashBytes);

        // Create a real UInt160 sender (20 bytes)
        var senderBytes = new byte[20];
        senderBytes[0] = (byte)id;
        var sender = new UInt160(senderBytes);

        var mockTx = new Mock<ITransactionData>();
        mockTx.Setup(t => t.Hash).Returns(hash);
        mockTx.Setup(t => t.Sender).Returns(sender);
        mockTx.Setup(t => t.FeePerByte).Returns(feePerByte);
        mockTx.Setup(t => t.NetworkFee).Returns(feePerByte * 100);
        mockTx.Setup(t => t.Nonce).Returns((uint)id);
        return mockTx.Object;
    }
}

[TestClass]
public class UT_DefaultConflictDetector
{
    [TestMethod]
    public void TestDetectConflicts_NoConflicts()
    {
        var detector = new DefaultConflictDetector();
        var newTx = CreateMockTransaction(1, "Sender1", 1);
        var existing = new[] { CreateMockTransaction(2, "Sender2", 2) };

        var conflicts = detector.DetectConflicts(newTx, existing).ToList();

        Assert.AreEqual(0, conflicts.Count);
    }

    [TestMethod]
    public void TestDetectConflicts_SameSenderSameNonce()
    {
        var detector = new DefaultConflictDetector();
        var newTx = CreateMockTransaction(1, "Sender1", 1);
        var existing = new[] { CreateMockTransaction(2, "Sender1", 1) };

        var conflicts = detector.DetectConflicts(newTx, existing).ToList();

        Assert.AreEqual(1, conflicts.Count);
    }

    [TestMethod]
    public void TestDetectConflicts_SameSenderDifferentNonce()
    {
        var detector = new DefaultConflictDetector();
        var newTx = CreateMockTransaction(1, "Sender1", 1);
        var existing = new[] { CreateMockTransaction(2, "Sender1", 2) };

        var conflicts = detector.DetectConflicts(newTx, existing).ToList();

        Assert.AreEqual(0, conflicts.Count);
    }

    private static ITransactionData CreateMockTransaction(int id, string sender, uint nonce)
    {
        // Create a real UInt256 hash (32 bytes)
        var hashBytes = new byte[32];
        hashBytes[0] = (byte)id;
        var hash = new UInt256(hashBytes);

        // Create a real UInt160 sender (20 bytes)
        var senderBytes = new byte[20];
        // Use sender string hash to differentiate senders
        var senderHash = sender.GetHashCode();
        senderBytes[0] = (byte)senderHash;
        senderBytes[1] = (byte)(senderHash >> 8);
        var senderUint = new UInt160(senderBytes);

        var mockTx = new Mock<ITransactionData>();
        mockTx.Setup(t => t.Hash).Returns(hash);
        mockTx.Setup(t => t.Nonce).Returns(nonce);
        mockTx.Setup(t => t.Sender).Returns(senderUint);

        return mockTx.Object;
    }
}
