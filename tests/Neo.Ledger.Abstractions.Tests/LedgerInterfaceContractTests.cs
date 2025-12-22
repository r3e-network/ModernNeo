// Copyright (C) 2015-2025 The Neo Project.
//
// LedgerInterfaceContractTests.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.Ledger.Abstractions;
using System;
using System.Linq;
using System.Reflection;

namespace Neo.Ledger.Abstractions.Tests
{
    [TestClass]
    public class LedgerInterfaceContractTests
    {
        private readonly Assembly _assembly = typeof(IBlockExecutionStrategy).Assembly;

        [TestMethod]
        public void Assembly_OnlyDependsOnCoreAbstractions()
        {
            // Neo.Ledger.Abstractions should only depend on Neo.Core.Abstractions
            var references = _assembly.GetReferencedAssemblies();

            foreach (var reference in references)
            {
                Assert.IsTrue(
                    reference.Name!.StartsWith("System") ||
                    reference.Name.StartsWith("Microsoft") ||
                    reference.Name.StartsWith("netstandard") ||
                    reference.Name == "Neo.Core.Abstractions" ||
                    reference.Name == "Neo.Ledger.Abstractions",
                    $"Unexpected dependency: {reference.Name}. Neo.Ledger.Abstractions should only depend on Neo.Core.Abstractions.");
            }
        }

        [TestMethod]
        public void IBlockExecutionStrategy_HasRequiredMembers()
        {
            var type = typeof(IBlockExecutionStrategy);

            Assert.IsNotNull(type.GetProperty("IsParallelEnabled"));
            Assert.IsNotNull(type.GetProperty("LastStatistics"));
            Assert.IsNotNull(type.GetMethod("Execute"));
        }

        [TestMethod]
        public void IBlockPersistenceStrategy_HasRequiredMembers()
        {
            var type = typeof(IBlockPersistenceStrategy);

            Assert.IsNotNull(type.GetEvent("BlockPersisted"));
            Assert.IsNotNull(type.GetMethod("PersistAsync"));
            Assert.IsNotNull(type.GetMethod("VerifyAsync"));
        }

        [TestMethod]
        public void IBlockchainStateProvider_HasRequiredMembers()
        {
            var type = typeof(IBlockchainStateProvider);

            Assert.IsNotNull(type.GetProperty("CurrentHeight"));
            Assert.IsNotNull(type.GetProperty("CurrentBlockHash"));
            Assert.IsNotNull(type.GetMethod("GetSnapshot"));
            Assert.IsNotNull(type.GetMethod("ContainsBlock"));
            Assert.IsNotNull(type.GetMethod("ContainsTransaction"));
        }

        [TestMethod]
        public void IHeaderCache_HasRequiredMembers()
        {
            var type = typeof(IHeaderCache);

            Assert.IsNotNull(type.GetProperty("Count"));
            Assert.IsNotNull(type.GetProperty("Full"));
            Assert.IsNotNull(type.GetProperty("Last"));
            Assert.IsNotNull(type.GetMethod("Add"));
            Assert.IsNotNull(type.GetMethod("GetAll"));
            Assert.IsNotNull(type.GetMethod("Clear"));
        }

        [TestMethod]
        public void IBlockExecutionResult_HasRequiredMembers()
        {
            var type = typeof(IBlockExecutionResult);

            Assert.IsNotNull(type.GetProperty("TransactionHash"));
            Assert.IsNotNull(type.GetProperty("ShouldCommit"));
            Assert.IsNotNull(type.GetProperty("Context"));
        }

        [TestMethod]
        public void ITransactionExecutionContext_HasRequiredMembers()
        {
            var type = typeof(ITransactionExecutionContext);

            Assert.IsNotNull(type.GetProperty("Transaction"));
            Assert.IsNotNull(type.GetProperty("State"));
            Assert.IsNotNull(type.GetProperty("GasConsumed"));
            Assert.IsNotNull(type.GetProperty("Exception"));
            Assert.IsNotNull(type.GetProperty("Notifications"));
        }

        [TestMethod]
        public void IExecutionStatistics_HasRequiredMembers()
        {
            var type = typeof(IExecutionStatistics);

            Assert.IsNotNull(type.GetProperty("TotalTransactions"));
            Assert.IsNotNull(type.GetProperty("SuccessfulTransactions"));
            Assert.IsNotNull(type.GetProperty("FailedTransactions"));
            Assert.IsNotNull(type.GetProperty("BatchCount"));
            Assert.IsNotNull(type.GetProperty("PeakParallelism"));
        }

        [TestMethod]
        public void INotification_HasRequiredMembers()
        {
            var type = typeof(INotification);

            Assert.IsNotNull(type.GetProperty("ScriptHash"));
            Assert.IsNotNull(type.GetProperty("EventName"));
        }

        [TestMethod]
        public void BlockPersistedEventArgs_CanBeCreated()
        {
            // Create a mock block data
            var mockBlock = new MockBlockData();
            var args = new BlockPersistedEventArgs(mockBlock);

            Assert.IsNotNull(args.Block);
            Assert.AreEqual(mockBlock.Index, args.Index);
        }

        [TestMethod]
        public void BlockPersistedEventArgs_ThrowsOnNullBlock()
        {
            Assert.ThrowsExactly<ArgumentNullException>(() => new BlockPersistedEventArgs(null!));
        }

        [TestMethod]
        public void AllPublicTypes_AreInCorrectNamespace()
        {
            var types = _assembly.GetTypes().Where(t => t.IsPublic);

            foreach (var type in types)
            {
                Assert.IsTrue(
                    type.Namespace!.StartsWith("Neo.Ledger.Abstractions"),
                    $"Type {type.Name} is in unexpected namespace {type.Namespace}");
            }
        }

        // Mock implementation for testing
        private class MockBlockData : Neo.Core.Abstractions.IBlockData
        {
            public byte[] Hash => new byte[32];
            public uint Index => 100;
            public ulong Timestamp => 1234567890;
            public byte[] PrevHash => new byte[32];
            public byte[] MerkleRoot => new byte[32];
            public byte PrimaryIndex => 0;
            public byte[] NextConsensus => new byte[20];
            public int TransactionCount => 0;
            public System.Collections.Generic.IReadOnlyList<Neo.Core.Abstractions.ITransactionData> Transactions =>
                Array.Empty<Neo.Core.Abstractions.ITransactionData>();
        }
    }
}
