// Copyright (C) 2015-2025 The Neo Project.
//
// UT_BlockchainState.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

#nullable enable

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo;
using Neo.Ledger;
using Neo.Network.P2P.Payloads;

namespace Neo.UnitTests.Ledger
{
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
            var genesisBlock = CreateBlock(0);
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
            var genesis = CreateBlock(0);
            var block1 = CreateBlock(1);

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
            var genesis = CreateBlock(0);
            _state.Initialize(genesis);
            _state.SetSynchronized(true);

            var stats = _state.GetStatistics();

            Assert.AreEqual(0u, stats.Height);
            Assert.IsTrue(stats.IsSynchronized);
        }

        private static Block CreateBlock(uint index)
        {
            var prevHashBytes = new byte[UInt256.Length];
            prevHashBytes[0] = (byte)index;
            var prevHash = new UInt256(prevHashBytes);

            var header = new Header
            {
                Version = 0,
                PrevHash = prevHash,
                MerkleRoot = UInt256.Zero,
                Timestamp = index,
                Nonce = 0,
                Index = index,
                PrimaryIndex = 0,
                NextConsensus = UInt160.Zero,
                Witness = Witness.Empty
            };

            return new Block
            {
                Header = header,
                Transactions = Array.Empty<Transaction>()
            };
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
            var mockBlock = CreateBlock(0);

            var results = executor.Execute(
                new List<object>(),
                new object(),
                mockBlock,
                new object());

            Assert.AreEqual(0, results.Count);
        }

        [TestMethod]
        public void TestExecute_WithTransactions()
        {
            var executor = new SequentialBlockExecutor();
            var mockBlock = CreateBlock(0);
            var transactions = new List<object> { new object(), new object() };

            var results = executor.Execute(
                transactions,
                new object(),
                mockBlock,
                new object());

            Assert.AreEqual(2, results.Count);
        }

        [TestMethod]
        public void TestExecute_CallsOnExecuted()
        {
            var executor = new SequentialBlockExecutor();
            var mockBlock = CreateBlock(0);
            var transactions = new List<object> { new object() };
            var callCount = 0;

            executor.Execute(
                transactions,
                new object(),
                mockBlock,
                new object(),
                _ => callCount++);

            Assert.AreEqual(1, callCount);
        }

        [TestMethod]
        public void TestLastStatistics()
        {
            var executor = new SequentialBlockExecutor();
            var mockBlock = CreateBlock(0);
            var transactions = new List<object> { new object(), new object(), new object() };

            executor.Execute(transactions, new object(), mockBlock, new object());

            var stats = executor.LastStatistics;
            Assert.IsNotNull(stats);
            Assert.AreEqual(3, stats.TotalTransactions);
            Assert.AreEqual(1, stats.BatchCount);
            Assert.AreEqual(1, stats.PeakParallelism);
        }

        private static Block CreateBlock(uint index)
        {
            var prevHashBytes = new byte[UInt256.Length];
            prevHashBytes[0] = (byte)index;
            var prevHash = new UInt256(prevHashBytes);

            var header = new Header
            {
                Version = 0,
                PrevHash = prevHash,
                MerkleRoot = UInt256.Zero,
                Timestamp = index,
                Nonce = 0,
                Index = index,
                PrimaryIndex = 0,
                NextConsensus = UInt160.Zero,
                Witness = Witness.Empty
            };

            return new Block
            {
                Header = header,
                Transactions = Array.Empty<Transaction>()
            };
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
}
