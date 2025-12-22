// Copyright (C) 2015-2025 The Neo Project.
//
// UT_BlockExecutorAdapter.cs file belongs to the neo project and is free
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

namespace Neo.UnitTests.Ledger
{
    [TestClass]
    public class UT_BlockExecutorAdapter
    {
        [TestMethod]
        public void Constructor_DefaultEnablesParallel()
        {
            var adapter = new BlockExecutorAdapter();

            Assert.IsTrue(adapter.IsParallelEnabled);
            Assert.AreEqual(3, adapter.ParallelThreshold);
            Assert.IsFalse(adapter.ForceSequential);
        }

        [TestMethod]
        public void Constructor_DisableParallel()
        {
            var adapter = new BlockExecutorAdapter(enableParallel: false);

            Assert.IsFalse(adapter.IsParallelEnabled);
        }

        [TestMethod]
        public void ForceSequential_DisablesParallel()
        {
            var adapter = new BlockExecutorAdapter();
            Assert.IsTrue(adapter.IsParallelEnabled);

            adapter.ForceSequential = true;
            Assert.IsFalse(adapter.IsParallelEnabled);
        }

        [TestMethod]
        public void ParallelThreshold_CanBeModified()
        {
            var adapter = new BlockExecutorAdapter
            {
                ParallelThreshold = 10
            };

            Assert.AreEqual(10, adapter.ParallelThreshold);
        }

        [TestMethod]
        public void LastStatistics_InitiallyNull()
        {
            var adapter = new BlockExecutorAdapter();

            Assert.IsNull(adapter.LastStatistics);
        }

        [TestMethod]
        public void IBlockExecutor_InterfaceContract()
        {
            IBlockExecutor executor = new BlockExecutorAdapter();

            Assert.IsNotNull(executor);
            Assert.IsTrue(executor.IsParallelEnabled);
            Assert.IsNull(executor.LastStatistics);
        }

        [TestMethod]
        public void IBlockExecutionResult_InterfaceContract()
        {
            var result = BlockExecutionResult.Success(new byte[] { 1, 2, 3 }, new object());

            Assert.IsNotNull(result.TransactionHash);
            Assert.AreEqual(3, result.TransactionHash.Length);
            Assert.AreEqual(1, result.State); // HALT
            Assert.IsTrue(result.ShouldCommit);
            Assert.IsNotNull(result.ApplicationExecuted);
        }

        [TestMethod]
        public void BlockExecutionResult_Success_SetsCorrectValues()
        {
            var hash = new byte[] { 0xAB, 0xCD, 0xEF };
            var appExecuted = new object();

            var result = BlockExecutionResult.Success(hash, appExecuted);

            Assert.AreEqual(hash, result.TransactionHash);
            Assert.AreEqual(1, result.State); // HALT = 1
            Assert.IsTrue(result.ShouldCommit);
            Assert.AreEqual(appExecuted, result.ApplicationExecuted);
        }

        [TestMethod]
        public void BlockExecutionResult_Failure_SetsCorrectValues()
        {
            var hash = new byte[] { 0x11, 0x22, 0x33 };
            var appExecuted = new object();

            var result = BlockExecutionResult.Failure(hash, appExecuted);

            Assert.AreEqual(hash, result.TransactionHash);
            Assert.AreEqual(2, result.State); // FAULT = 2
            Assert.IsFalse(result.ShouldCommit);
            Assert.AreEqual(appExecuted, result.ApplicationExecuted);
        }

        [TestMethod]
        public void BlockExecutionResult_DefaultValues()
        {
            var result = new BlockExecutionResult();

            Assert.IsNotNull(result.TransactionHash);
            Assert.AreEqual(0, result.TransactionHash.Length);
            Assert.AreEqual(0, result.State);
            Assert.IsFalse(result.ShouldCommit);
            Assert.IsNull(result.ApplicationExecuted);
        }

        [TestMethod]
        public void ParallelThreshold_AffectsParallelDecision()
        {
            var adapter = new BlockExecutorAdapter();

            // Default threshold is 3
            Assert.AreEqual(3, adapter.ParallelThreshold);

            // Can set higher threshold
            adapter.ParallelThreshold = 100;
            Assert.AreEqual(100, adapter.ParallelThreshold);

            // Can set lower threshold
            adapter.ParallelThreshold = 1;
            Assert.AreEqual(1, adapter.ParallelThreshold);
        }
    }
}
