// Copyright (C) 2015-2025 The Neo Project.
//
// UT_BlockchainState.cs file belongs to the neo project and is free
// software distributed under the MIT software license.

#nullable enable

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Neo.Core.Interfaces;
using Neo.Ledger;

namespace Neo.UnitTests.Ledger;

[TestClass]
public class UT_BlockchainState
{
    private BlockchainState _state = null!;

    [TestInitialize]
    public void Setup()
    {
        _state = new BlockchainState();
    }

    [TestMethod]
    public void TestConstructor_DefaultState()
    {
        Assert.AreEqual(0u, _state.Height);
        Assert.IsFalse(_state.IsSynchronized);
    }

    [TestMethod]
    public void TestInitialize()
    {
        var genesisBlock = CreateMockBlock(0);
        _state.Initialize(genesisBlock);

        Assert.AreEqual(0u, _state.Height);
    }

    [TestMethod]
    public void TestInitialize_NullBlock_ThrowsException()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => _state.Initialize(null!));
    }

    [TestMethod]
    public void TestOnBlockPersisted()
    {
        var genesis = CreateMockBlock(0);
        var block1 = CreateMockBlock(1);

        _state.Initialize(genesis);
        _state.OnBlockPersisted(block1);

        Assert.AreEqual(1u, _state.Height);
    }

    [TestMethod]
    public void TestSetSynchronized()
    {
        Assert.IsFalse(_state.IsSynchronized);

        _state.SetSynchronized(true);
        Assert.IsTrue(_state.IsSynchronized);

        _state.SetSynchronized(false);
        Assert.IsFalse(_state.IsSynchronized);
    }

    [TestMethod]
    public async Task TestGetBlockAsync_NonExisting()
    {
        var result = await _state.GetBlockAsync(new byte[] { 9, 9, 9 });
        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task TestGetBlockByIndexAsync_NonExisting()
    {
        var result = await _state.GetBlockByIndexAsync(999);
        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task TestContainsBlockAsync_NonExisting()
    {
        var result = await _state.ContainsBlockAsync(new byte[] { 9, 9, 9 });
        Assert.IsFalse(result);
    }

    [TestMethod]
    public async Task TestGetTransactionAsync_NonExisting()
    {
        var result = await _state.GetTransactionAsync(new byte[] { 1, 2, 3 });
        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task TestContainsTransactionAsync_NonExisting()
    {
        var result = await _state.ContainsTransactionAsync(new byte[] { 1, 2, 3 });
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void TestGetStatistics()
    {
        var genesis = CreateMockBlock(0);
        _state.Initialize(genesis);
        _state.SetSynchronized(true);

        var stats = _state.GetStatistics();

        Assert.AreEqual(0u, stats.Height);
        Assert.IsTrue(stats.IsSynchronized);
    }

    private static IBlockData CreateMockBlock(uint index)
    {
        // Create a real UInt256 for Hash (32 bytes)
        var hashBytes = new byte[32];
        hashBytes[0] = (byte)index;
        hashBytes[1] = (byte)(index >> 8);
        var hash = new UInt256(hashBytes);

        // Create a real UInt256 for MerkleRoot (32 bytes)
        var merkleBytes = new byte[32];
        merkleBytes[0] = (byte)(index + 100);
        merkleBytes[1] = (byte)((index + 100) >> 8);
        var merkleRoot = new UInt256(merkleBytes);

        var mockBlock = new Mock<IBlockData>();
        mockBlock.Setup(b => b.Index).Returns(index);
        mockBlock.Setup(b => b.Hash).Returns(hash);
        mockBlock.Setup(b => b.MerkleRoot).Returns(merkleRoot);
        return mockBlock.Object;
    }
}

[TestClass]
public class UT_SequentialBlockExecutor
{
    [TestMethod]
    public void TestIsParallelEnabled()
    {
        var executor = new SequentialBlockExecutor();
        Assert.IsFalse(executor.IsParallelEnabled);
    }

    [TestMethod]
    public void TestExecute_EmptyTransactions()
    {
        var executor = new SequentialBlockExecutor();
        var mockBlock = new Mock<IBlockData>();

        var results = executor.Execute(
            new List<object>(),
            new object(),
            mockBlock.Object,
            new object());

        Assert.AreEqual(0, results.Count);
    }

    [TestMethod]
    public void TestExecute_WithTransactions()
    {
        var executor = new SequentialBlockExecutor();
        var mockBlock = new Mock<IBlockData>();
        var transactions = new List<object> { new object(), new object() };

        var results = executor.Execute(
            transactions,
            new object(),
            mockBlock.Object,
            new object());

        Assert.AreEqual(2, results.Count);
    }

    [TestMethod]
    public void TestExecute_CallsOnExecuted()
    {
        var executor = new SequentialBlockExecutor();
        var mockBlock = new Mock<IBlockData>();
        var transactions = new List<object> { new object() };
        var callCount = 0;

        executor.Execute(
            transactions,
            new object(),
            mockBlock.Object,
            new object(),
            _ => callCount++);

        Assert.AreEqual(1, callCount);
    }

    [TestMethod]
    public void TestLastStatistics()
    {
        var executor = new SequentialBlockExecutor();
        var mockBlock = new Mock<IBlockData>();
        var transactions = new List<object> { new object(), new object(), new object() };

        executor.Execute(transactions, new object(), mockBlock.Object, new object());

        var stats = executor.LastStatistics;
        Assert.IsNotNull(stats);
        Assert.AreEqual(3, stats.TotalTransactions);
        Assert.AreEqual(1, stats.BatchCount);
        Assert.AreEqual(1, stats.PeakParallelism);
    }
}

[TestClass]
public class UT_ExecutionStatistics
{
    [TestMethod]
    public void TestAverageTransactionsPerBatch()
    {
        var stats = new ExecutionStatistics
        {
            TotalTransactions = 10,
            BatchCount = 2
        };

        Assert.AreEqual(5.0, stats.AverageTransactionsPerBatch);
    }

    [TestMethod]
    public void TestAverageTransactionsPerBatch_ZeroBatches()
    {
        var stats = new ExecutionStatistics
        {
            TotalTransactions = 10,
            BatchCount = 0
        };

        Assert.AreEqual(0.0, stats.AverageTransactionsPerBatch);
    }

    [TestMethod]
    public void TestIncrementSuccessful()
    {
        var stats = new ExecutionStatistics();
        stats.IncrementSuccessful();
        stats.IncrementSuccessful();
        Assert.AreEqual(2, stats.SuccessfulTransactions);
    }

    [TestMethod]
    public void TestIncrementFailed()
    {
        var stats = new ExecutionStatistics();
        stats.IncrementFailed();
        Assert.AreEqual(1, stats.FailedTransactions);
    }

    [TestMethod]
    public void TestUpdatePeakParallelism()
    {
        var stats = new ExecutionStatistics { PeakParallelism = 5 };
        stats.UpdatePeakParallelism(10);
        Assert.AreEqual(10, stats.PeakParallelism);

        // Should not decrease
        stats.UpdatePeakParallelism(3);
        Assert.AreEqual(10, stats.PeakParallelism);
    }
}

[TestClass]
public class UT_BlockExecutionResultImpl
{
    [TestMethod]
    public void TestProperties()
    {
        var result = new BlockExecutionResultImpl
        {
            TransactionHash = new byte[] { 1, 2, 3 },
            State = 0x01,
            ShouldCommit = true,
            ApplicationExecuted = "test"
        };

        CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, result.TransactionHash);
        Assert.AreEqual((byte)0x01, result.State);
        Assert.IsTrue(result.ShouldCommit);
        Assert.AreEqual("test", result.ApplicationExecuted);
    }
}

[TestClass]
public class UT_BlockchainStateStatistics
{
    [TestMethod]
    public void TestProperties()
    {
        var stats = new BlockchainStateStatistics
        {
            Height = 100,
            CachedBlocks = 50,
            CachedHeaders = 100,
            CachedTransactions = 200,
            IsSynchronized = true
        };

        Assert.AreEqual(100u, stats.Height);
        Assert.AreEqual(50, stats.CachedBlocks);
        Assert.AreEqual(100, stats.CachedHeaders);
        Assert.AreEqual(200, stats.CachedTransactions);
        Assert.IsTrue(stats.IsSynchronized);
    }
}
