// Copyright (C) 2015-2025 The Neo Project.
//
// UT_DependencyGraph.cs file belongs to the neo project and is free
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
    public class UT_DependencyGraph
    {
        [TestMethod]
        public void Test_Constructor_EmptyTransactions()
        {
            var transactions = Array.Empty<ITransactionData>();
            var graph = new DependencyGraph(transactions);

            Assert.AreEqual(0, graph.TransactionCount);
            Assert.AreEqual(0, graph.DependencyCount);
            Assert.AreEqual(0, graph.Transactions.Count);
        }

        [TestMethod]
        public void Test_Constructor_WithTransactions()
        {
            var transactions = CreateMockTransactions(5);
            var graph = new DependencyGraph(transactions);

            Assert.AreEqual(5, graph.TransactionCount);
            Assert.AreEqual(0, graph.DependencyCount);
            Assert.AreEqual(5, graph.Transactions.Count);
        }

        [TestMethod]
        public void Test_AddDependency_ValidIndices()
        {
            var transactions = CreateMockTransactions(3);
            var graph = new DependencyGraph(transactions);

            graph.AddDependency(1, 0); // tx1 depends on tx0
            graph.AddDependency(2, 1); // tx2 depends on tx1

            Assert.AreEqual(2, graph.DependencyCount);

            var deps1 = graph.GetDependencies(1);
            Assert.AreEqual(1, deps1.Count);
            Assert.IsTrue(deps1.Contains(0));

            var deps2 = graph.GetDependencies(2);
            Assert.AreEqual(1, deps2.Count);
            Assert.IsTrue(deps2.Contains(1));
        }

        [TestMethod]
        public void Test_AddDependency_SelfReference_Ignored()
        {
            var transactions = CreateMockTransactions(2);
            var graph = new DependencyGraph(transactions);

            graph.AddDependency(0, 0); // Self-reference should be ignored

            Assert.AreEqual(0, graph.DependencyCount);
        }

        [TestMethod]
        public void Test_AddDependency_InvalidIndices_Ignored()
        {
            var transactions = CreateMockTransactions(2);
            var graph = new DependencyGraph(transactions);

            graph.AddDependency(-1, 0);
            graph.AddDependency(0, -1);
            graph.AddDependency(5, 0);
            graph.AddDependency(0, 5);

            Assert.AreEqual(0, graph.DependencyCount);
        }

        [TestMethod]
        public void Test_AddWeakDependency()
        {
            var transactions = CreateMockTransactions(2);
            var graph = new DependencyGraph(transactions);

            graph.AddWeakDependency(1, 0);

            Assert.AreEqual(1, graph.DependencyCount);
            var depType = graph.GetDependencyType(1, 0);
            Assert.IsTrue(depType.HasFlag(DependencyType.Weak));
        }

        [TestMethod]
        public void Test_GetInDegree_NoDependencies()
        {
            var transactions = CreateMockTransactions(3);
            var graph = new DependencyGraph(transactions);

            Assert.AreEqual(0, graph.GetInDegree(0));
            Assert.AreEqual(0, graph.GetInDegree(1));
            Assert.AreEqual(0, graph.GetInDegree(2));
        }

        [TestMethod]
        public void Test_GetInDegree_WithDependencies()
        {
            var transactions = CreateMockTransactions(4);
            var graph = new DependencyGraph(transactions);

            graph.AddDependency(1, 0);
            graph.AddDependency(2, 0);
            graph.AddDependency(3, 1);
            graph.AddDependency(3, 2);

            Assert.AreEqual(0, graph.GetInDegree(0));
            Assert.AreEqual(1, graph.GetInDegree(1));
            Assert.AreEqual(1, graph.GetInDegree(2));
            Assert.AreEqual(2, graph.GetInDegree(3));
        }

        [TestMethod]
        public void Test_GetInDegree_WithRemainingIndices()
        {
            var transactions = CreateMockTransactions(4);
            var graph = new DependencyGraph(transactions);

            graph.AddDependency(1, 0);
            graph.AddDependency(2, 0);
            graph.AddDependency(3, 1);
            graph.AddDependency(3, 2);

            // Only consider indices 1, 2, 3 (exclude 0)
            var remaining = new HashSet<int> { 1, 2, 3 };

            Assert.AreEqual(0, graph.GetInDegree(1, remaining)); // tx0 not in remaining
            Assert.AreEqual(0, graph.GetInDegree(2, remaining));
            Assert.AreEqual(2, graph.GetInDegree(3, remaining)); // depends on 1 and 2
        }

        [TestMethod]
        public void Test_GetIndependentTransactions_AllIndependent()
        {
            var transactions = CreateMockTransactions(3);
            var graph = new DependencyGraph(transactions);

            var independent = graph.GetIndependentTransactions();

            Assert.AreEqual(3, independent.Count);
            Assert.IsTrue(independent.Contains(0));
            Assert.IsTrue(independent.Contains(1));
            Assert.IsTrue(independent.Contains(2));
        }

        [TestMethod]
        public void Test_GetIndependentTransactions_WithDependencies()
        {
            var transactions = CreateMockTransactions(3);
            var graph = new DependencyGraph(transactions);

            graph.AddDependency(1, 0);
            graph.AddDependency(2, 1);

            var independent = graph.GetIndependentTransactions();

            Assert.AreEqual(1, independent.Count);
            Assert.IsTrue(independent.Contains(0));
        }

        [TestMethod]
        public void Test_HasCycle_NoCycle()
        {
            var transactions = CreateMockTransactions(3);
            var graph = new DependencyGraph(transactions);

            graph.AddDependency(1, 0);
            graph.AddDependency(2, 1);

            Assert.IsFalse(graph.HasCycle());
        }

        [TestMethod]
        public void Test_HasCycle_WithCycle()
        {
            var transactions = CreateMockTransactions(3);
            var graph = new DependencyGraph(transactions);

            graph.AddDependency(1, 0);
            graph.AddDependency(2, 1);
            graph.AddDependency(0, 2); // Creates a cycle: 0 -> 1 -> 2 -> 0

            Assert.IsTrue(graph.HasCycle());
        }

        [TestMethod]
        public void Test_GetDependents()
        {
            var transactions = CreateMockTransactions(3);
            var graph = new DependencyGraph(transactions);

            graph.AddDependency(1, 0);
            graph.AddDependency(2, 0);

            var dependents = graph.GetDependents(0);

            Assert.AreEqual(2, dependents.Count);
            Assert.IsTrue(dependents.Contains(1));
            Assert.IsTrue(dependents.Contains(2));
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
                            Account = UInt160.Parse($"0x000000000000000000000000000000000000000{i}"),
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
