// Copyright (C) 2015-2025 The Neo Project.
//
// UT_ExecutionScheduler.cs file belongs to the neo project and is free
// software distributed under the MIT software license.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.Core.Interfaces;
using Neo.Execution;
using Neo.Network.P2P.Payloads;
using Neo.SmartContract;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Neo.UnitTests.Execution
{
    [TestClass]
    public class UT_ExecutionScheduler
    {
        private ExecutionScheduler _scheduler = null!;

        [TestInitialize]
        public void Setup()
        {
            _scheduler = new ExecutionScheduler();
        }

        [TestMethod]
        public void Test_Schedule_EmptyGraph()
        {
            var graph = new DependencyGraph(Array.Empty<ITransactionData>());
            var batches = _scheduler.Schedule(graph).ToList();

            Assert.AreEqual(0, batches.Count);
        }

        [TestMethod]
        public void Test_Schedule_SingleTransaction()
        {
            var transactions = CreateMockTransactions(1);
            var graph = new DependencyGraph(transactions);

            var batches = _scheduler.Schedule(graph).ToList();

            Assert.AreEqual(1, batches.Count);
            Assert.AreEqual(1, batches[0].TransactionIndices.Count);
            Assert.AreEqual(0, batches[0].TransactionIndices[0]);
        }

        [TestMethod]
        public void Test_Schedule_IndependentTransactions_SingleBatch()
        {
            var transactions = CreateMockTransactions(5);
            var graph = new DependencyGraph(transactions);

            var batches = _scheduler.Schedule(graph).ToList();

            Assert.AreEqual(1, batches.Count);
            Assert.AreEqual(5, batches[0].TransactionIndices.Count);
        }

        [TestMethod]
        public void Test_Schedule_LinearDependencies_MultipleBatches()
        {
            var transactions = CreateMockTransactions(3);
            var graph = new DependencyGraph(transactions);

            // Create linear dependency: tx0 -> tx1 -> tx2
            graph.AddDependency(1, 0);
            graph.AddDependency(2, 1);

            var batches = _scheduler.Schedule(graph).ToList();

            Assert.AreEqual(3, batches.Count);
            Assert.AreEqual(0, batches[0].TransactionIndices[0]); // tx0 first
            Assert.AreEqual(1, batches[1].TransactionIndices[0]); // tx1 second
            Assert.AreEqual(2, batches[2].TransactionIndices[0]); // tx2 third
        }

        [TestMethod]
        public void Test_Schedule_DiamondDependencies()
        {
            var transactions = CreateMockTransactions(4);
            var graph = new DependencyGraph(transactions);

            // Diamond pattern: tx0 at top, tx1/tx2 in middle, tx3 at bottom
            graph.AddDependency(1, 0);
            graph.AddDependency(2, 0);
            graph.AddDependency(3, 1);
            graph.AddDependency(3, 2);

            var batches = _scheduler.Schedule(graph).ToList();

            // Should have 3 batches: [0], [1,2], [3]
            Assert.AreEqual(3, batches.Count);

            // First batch should contain only tx0
            Assert.AreEqual(1, batches[0].TransactionIndices.Count);
            Assert.IsTrue(batches[0].TransactionIndices.Contains(0));

            // Second batch should contain tx1 and tx2
            Assert.AreEqual(2, batches[1].TransactionIndices.Count);
            Assert.IsTrue(batches[1].TransactionIndices.Contains(1));
            Assert.IsTrue(batches[1].TransactionIndices.Contains(2));

            // Third batch should contain only tx3
            Assert.AreEqual(1, batches[2].TransactionIndices.Count);
            Assert.IsTrue(batches[2].TransactionIndices.Contains(3));
        }

        [TestMethod]
        public void Test_Schedule_CyclicDependencies_ThrowsException()
        {
            var transactions = CreateMockTransactions(3);
            var graph = new DependencyGraph(transactions);

            // Create cycle
            graph.AddDependency(1, 0);
            graph.AddDependency(2, 1);
            graph.AddDependency(0, 2);

            // Should throw InvalidOperationException
            Assert.ThrowsExactly<InvalidOperationException>(() => _scheduler.Schedule(graph).ToList());
        }

        [TestMethod]
        public void Test_Schedule_MaxBatchSize()
        {
            var transactions = CreateMockTransactions(100);
            var graph = new DependencyGraph(transactions);

            _scheduler.MaxBatchSize = 20;
            var batches = _scheduler.Schedule(graph).ToList();

            // Should split into 5 batches of 20
            Assert.AreEqual(5, batches.Count);
            foreach (var batch in batches)
            {
                Assert.AreEqual(20, batch.TransactionIndices.Count);
            }
        }

        [TestMethod]
        public void Test_Strategy_Default()
        {
            Assert.AreEqual(SchedulingStrategy.Conservative, _scheduler.Strategy);
        }

        [TestMethod]
        public void Test_Strategy_Optimistic()
        {
            _scheduler.Strategy = SchedulingStrategy.Optimistic;

            var transactions = CreateMockTransactions(3);
            var graph = new DependencyGraph(transactions);
            graph.AddWeakDependency(1, 0);

            var batches = _scheduler.Schedule(graph).ToList();

            // With optimistic strategy, weak dependencies may allow more parallelism
            Assert.IsTrue(batches.Count >= 1);
        }

        [TestMethod]
        public void Test_BatchNumber_Sequential()
        {
            var transactions = CreateMockTransactions(5);
            var graph = new DependencyGraph(transactions);

            graph.AddDependency(1, 0);
            graph.AddDependency(2, 1);

            var batches = _scheduler.Schedule(graph).ToList();

            for (int i = 0; i < batches.Count; i++)
            {
                Assert.AreEqual(i, batches[i].BatchNumber);
            }
        }

        [TestMethod]
        public void Test_ScheduleWithOrdering()
        {
            var transactions = CreateMockTransactions(5);
            var graph = new DependencyGraph(transactions);

            // Custom ordering: reverse order
            var batches = _scheduler.ScheduleWithOrdering(
                graph,
                indices => indices.Reverse().ToList()
            ).ToList();

            Assert.AreEqual(1, batches.Count);
            // First element should be 4 (reversed)
            Assert.AreEqual(4, batches[0].TransactionIndices[0]);
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
    }
}
