// Copyright (C) 2015-2025 The Neo Project.
//
// UT_DependencyAnalyzer.cs file belongs to the neo project and is free
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
using Neo.Network.P2P.Payloads;
using Neo.SmartContract;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Neo.UnitTests.Execution
{
    [TestClass]
    public class UT_DependencyAnalyzer
    {
        private DependencyAnalyzer _analyzer = null!;

        [TestInitialize]
        public void Setup()
        {
            _analyzer = new DependencyAnalyzer();
        }

        [TestMethod]
        public void Test_Analyze_EmptyTransactions()
        {
            var transactions = Array.Empty<ITransactionData>();
            var graph = _analyzer.Analyze(transactions);

            Assert.IsNotNull(graph);
            Assert.AreEqual(0, graph.TransactionCount);
            Assert.AreEqual(0, graph.DependencyCount);
        }

        [TestMethod]
        public void Test_Analyze_SingleTransaction()
        {
            var tx = CreateMockTransaction(1);
            var transactions = new ITransactionData[] { tx };

            var graph = _analyzer.Analyze(transactions);

            Assert.AreEqual(1, graph.TransactionCount);
            Assert.AreEqual(0, graph.DependencyCount);
        }

        [TestMethod]
        public void Test_Analyze_IndependentTransactions()
        {
            // Create transactions with different senders
            var tx1 = CreateMockTransaction(1);
            var tx2 = CreateMockTransaction(2);
            var transactions = new ITransactionData[] { tx1, tx2 };

            var graph = _analyzer.Analyze(transactions);

            Assert.AreEqual(2, graph.TransactionCount);
            // Independent transactions should have no dependencies
            var deps0 = graph.GetDependencies(0);
            var deps1 = graph.GetDependencies(1);
            Assert.AreEqual(0, deps0.Count);
            Assert.AreEqual(0, deps1.Count);
        }

        [TestMethod]
        public void Test_Analyze_SameSender_CreatesDependency()
        {
            // Create two transactions with the same sender
            var sender = UInt160.Parse("0x0000000000000000000000000000000000000001");
            var tx1 = CreateMockTransactionWithSender(1, sender);
            var tx2 = CreateMockTransactionWithSender(2, sender);
            var transactions = new ITransactionData[] { tx1, tx2 };

            var graph = _analyzer.Analyze(transactions);

            Assert.AreEqual(2, graph.TransactionCount);
            // tx2 (index 1) should depend on tx1 (index 0) due to same sender
            var deps1 = graph.GetDependencies(1);
            Assert.IsTrue(deps1.Contains(0));
        }

        [TestMethod]
        public void Test_CheckDependency_SameSender()
        {
            var sender = UInt160.Parse("0x0000000000000000000000000000000000000001");
            var tx1 = CreateMockTransactionWithSender(1, sender);
            var tx2 = CreateMockTransactionWithSender(2, sender);

            var depType = _analyzer.CheckDependency(tx1, tx2);

            Assert.IsTrue(depType.HasFlag(DependencyType.SameSender));
        }

        [TestMethod]
        public void Test_CheckDependency_DifferentSender()
        {
            var tx1 = CreateMockTransaction(1);
            var tx2 = CreateMockTransaction(2);

            var depType = _analyzer.CheckDependency(tx1, tx2);

            Assert.IsFalse(depType.HasFlag(DependencyType.SameSender));
        }

        [TestMethod]
        public void Test_IncludeHeuristicDependencies_Default()
        {
            Assert.IsTrue(_analyzer.IncludeHeuristicDependencies);
        }

        [TestMethod]
        public void Test_IncludeHeuristicDependencies_Disabled()
        {
            _analyzer.IncludeHeuristicDependencies = false;

            var tx1 = CreateMockTransaction(1);
            var tx2 = CreateMockTransaction(2);
            var transactions = new ITransactionData[] { tx1, tx2 };

            var graph = _analyzer.Analyze(transactions);

            // Should still work with heuristics disabled
            Assert.AreEqual(2, graph.TransactionCount);
        }

        private static Transaction CreateMockTransaction(int nonce)
        {
            var sender = UInt160.Parse($"0x000000000000000000000000000000000000000{nonce}");
            return CreateMockTransactionWithSender(nonce, sender);
        }

        private static Transaction CreateMockTransactionWithSender(int nonce, UInt160 sender)
        {
            return new Transaction
            {
                Version = 0,
                Nonce = (uint)nonce,
                SystemFee = 1000000,
                NetworkFee = 100000,
                ValidUntilBlock = 100,
                Signers = new[]
                {
                    new Signer
                    {
                        Account = sender,
                        Scopes = WitnessScope.CalledByEntry
                    }
                },
                Attributes = Array.Empty<TransactionAttribute>(),
                Script = new byte[] { 0x01 },
                Witnesses = Array.Empty<Witness>()
            };
        }
    }
}
