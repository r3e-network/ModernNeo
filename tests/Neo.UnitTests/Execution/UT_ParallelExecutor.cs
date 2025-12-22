// Copyright (C) 2015-2025 The Neo Project.
//
// UT_ParallelExecutor.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.Core.Interfaces;
using Neo.Execution;
using Neo.IO;
using Neo.Network.P2P.Payloads;
using Neo.SmartContract;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Neo.UnitTests.Execution
{
    [TestClass]
    [SuppressMessage("Performance", "MSTEST0049:Consider using the overload that accepts a CancellationToken")]
    public class UT_ParallelExecutor
    {
        private ParallelExecutor _executor = null!;

        [TestInitialize]
        public void Setup()
        {
            _executor = new ParallelExecutor();
        }

        [TestMethod]
        public void Test_MaxDegreeOfParallelism_Default()
        {
            Assert.AreEqual(Environment.ProcessorCount, _executor.MaxDegreeOfParallelism);
        }

        [TestMethod]
        public void Test_MaxDegreeOfParallelism_Set()
        {
            _executor.MaxDegreeOfParallelism = 4;
            Assert.AreEqual(4, _executor.MaxDegreeOfParallelism);
        }

        [TestMethod]
        public void Test_LastExecutionStatistics_Initial()
        {
            Assert.IsNull(_executor.LastExecutionStatistics);
        }

        [TestMethod]
        public async Task Test_ExecuteAsync_EmptyTransactions()
        {
            var transactions = Array.Empty<ITransactionData>();

            var results = await _executor.ExecuteAsync(
                transactions,
                new object(), // mock snapshot
                CreateMockBlock(),
                new object()); // mock settings

            Assert.AreEqual(0, results.Count);
        }

        [TestMethod]
        public async Task Test_ExecuteAsync_WithCancellation()
        {
            var cts = new CancellationTokenSource();
            cts.Cancel();

            var transactions = CreateMockTransactions(5);

            await Assert.ThrowsExactlyAsync<OperationCanceledException>(async () =>
            {
                await _executor.ExecuteAsync(
                    transactions,
                    new object(),
                    CreateMockBlock(),
                    new object(),
                    cts.Token);
            });
        }

        [TestMethod]
        public async Task Test_ExecuteAsync_InvalidSnapshot_ReturnsFailure()
        {
            var transactions = CreateMockTransactions(1);

            var results = await _executor.ExecuteAsync(
                transactions,
                "invalid snapshot", // Not a StoreCache
                CreateMockBlock(),
                new object());

            Assert.AreEqual(1, results.Count);
            Assert.IsFalse(results[0].IsSuccess);
            Assert.IsNotNull(results[0].Exception);
        }

        [TestMethod]
        public async Task Test_ExecuteAsync_InvalidBlock_ReturnsFailure()
        {
            using var system = TestBlockchain.GetSystem();
            var snapshot = system.GetSnapshotCache();

            var transactions = CreateMockTransactions(1);

            var results = await _executor.ExecuteAsync(
                transactions,
                snapshot,
                new MockBlockData(), // Not a Block
                new object());

            Assert.AreEqual(1, results.Count);
            Assert.IsFalse(results[0].IsSuccess);
        }

        [TestMethod]
        public async Task Test_ExecuteAsync_Statistics_Updated()
        {
            var transactions = CreateMockTransactions(3);

            await _executor.ExecuteAsync(
                transactions,
                new object(),
                CreateMockBlock(),
                new object());

            var stats = _executor.LastExecutionStatistics;
            Assert.IsNotNull(stats);
            Assert.AreEqual(3, stats.TotalTransactions);
            Assert.IsTrue(stats.BatchCount >= 1);
        }

        [TestMethod]
        public void Test_Constructor_WithCustomAnalyzerAndScheduler()
        {
            var analyzer = new DependencyAnalyzer { IncludeHeuristicDependencies = false };
            var scheduler = new ExecutionScheduler { Strategy = SchedulingStrategy.Optimistic };

            var executor = new ParallelExecutor(analyzer, scheduler);

            Assert.IsNotNull(executor);
            Assert.AreEqual(Environment.ProcessorCount, executor.MaxDegreeOfParallelism);
        }

        private static List<ITransactionData> CreateMockTransactions(int count)
        {
            var transactions = new List<ITransactionData>();
            for (int i = 0; i < count; i++)
            {
                transactions.Add(new Transaction
                {
                    Version = 0,
                    Nonce = (uint)i,
                    SystemFee = 1000000,
                    NetworkFee = 100000,
                    ValidUntilBlock = 100,
                    Signers = new[]
                    {
                        new Signer
                        {
                            Account = UInt160.Parse($"0x000000000000000000000000000000000000000{i % 10}"),
                            Scopes = WitnessScope.CalledByEntry
                        }
                    },
                    Attributes = Array.Empty<TransactionAttribute>(),
                    Script = new byte[] { 0x01 },
                    Witnesses = Array.Empty<Witness>()
                });
            }
            return transactions;
        }

        private static IBlockData CreateMockBlock()
        {
            return new MockBlockData();
        }

        private class MockBlockData : IBlockData
        {
            public uint Version => 0;
            public UInt256 PrevHash => UInt256.Zero;
            public UInt256 MerkleRoot => UInt256.Zero;
            public ulong Timestamp => 0;
            public ulong Nonce => 0;
            public uint Index => 0;
            public byte PrimaryIndex => 0;
            public UInt160 NextConsensus => UInt160.Zero;
            public int TransactionsCount => 0;
            public UInt256 Hash => UInt256.Zero;
            public int Size => 0;

            public void Serialize(BinaryWriter writer) { }
            public void Deserialize(ref MemoryReader reader) { }
            public void SerializeUnsigned(BinaryWriter writer) { }
            public void DeserializeUnsigned(ref MemoryReader reader) { }
        }
    }
}
