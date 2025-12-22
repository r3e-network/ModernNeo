// Copyright (C) 2015-2025 The Neo Project.
//
// UT_ExecutionResult.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.Execution;
using System;
using ExecutionStatistics = Neo.Execution.ExecutionStatistics;

namespace Neo.UnitTests.Execution
{
    [TestClass]
    public class UT_ExecutionResult
    {
        [TestMethod]
        public void Test_Success_CreatesSuccessResult()
        {
            var txHash = new byte[] { 1, 2, 3, 4 };
            var gasConsumed = 1000000L;

            var result = ExecutionResult.Success(txHash, gasConsumed);

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(1, result.State); // HALT
            Assert.AreEqual(gasConsumed, result.GasConsumed);
            Assert.IsNull(result.Exception);
            CollectionAssert.AreEqual(txHash, result.TransactionHash);
        }

        [TestMethod]
        public void Test_Success_WithNotifications()
        {
            var txHash = new byte[] { 1, 2, 3, 4 };
            var notifications = new object[] { "event1", "event2" };

            var result = ExecutionResult.Success(txHash, 1000, notifications);

            Assert.AreEqual(2, result.Notifications.Count);
        }

        [TestMethod]
        public void Test_Failure_CreatesFailureResult()
        {
            var txHash = new byte[] { 1, 2, 3, 4 };
            var exception = "Script execution failed";

            var result = ExecutionResult.Failure(txHash, exception);

            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual(2, result.State); // FAULT
            Assert.AreEqual(exception, result.Exception);
            CollectionAssert.AreEqual(txHash, result.TransactionHash);
        }

        [TestMethod]
        public void Test_Failure_WithGasConsumed()
        {
            var txHash = new byte[] { 1, 2, 3, 4 };
            var gasConsumed = 500000L;

            var result = ExecutionResult.Failure(txHash, "error", gasConsumed);

            Assert.AreEqual(gasConsumed, result.GasConsumed);
        }

        [TestMethod]
        public void Test_DefaultNotifications_IsEmpty()
        {
            var result = ExecutionResult.Success(new byte[] { 1 }, 0);

            Assert.IsNotNull(result.Notifications);
            Assert.AreEqual(0, result.Notifications.Count);
        }
    }

    [TestClass]
    public class UT_ExecutionStatistics
    {
        [TestMethod]
        public void Test_AverageTransactionsPerBatch_ZeroBatches()
        {
            var stats = new ExecutionStatistics
            {
                TotalTransactions = 10,
                BatchCount = 0
            };

            Assert.AreEqual(0, stats.AverageTransactionsPerBatch);
        }

        [TestMethod]
        public void Test_AverageTransactionsPerBatch_Calculation()
        {
            var stats = new ExecutionStatistics
            {
                TotalTransactions = 100,
                BatchCount = 4
            };

            Assert.AreEqual(25, stats.AverageTransactionsPerBatch);
        }

        [TestMethod]
        public void Test_AverageExecutionTimePerTxUs_Calculation()
        {
            var stats = new ExecutionStatistics
            {
                TotalTransactions = 10,
                TotalExecutionTimeMs = 100
            };

            Assert.AreEqual(10000, stats.AverageExecutionTimePerTxUs); // 100ms / 10tx * 1000 = 10000us
        }

        [TestMethod]
        public void Test_EstimatedSpeedup_Calculation()
        {
            var stats = new ExecutionStatistics
            {
                TotalTransactions = 100,
                BatchCount = 10
            };

            Assert.AreEqual(10, stats.EstimatedSpeedup);
        }

        [TestMethod]
        public void Test_ThreadSafeIncrement_Successful()
        {
            var stats = new ExecutionStatistics();

            stats.IncrementSuccessful();
            stats.IncrementSuccessful();
            stats.IncrementSuccessful();

            Assert.AreEqual(3, stats.SuccessfulTransactions);
        }

        [TestMethod]
        public void Test_ThreadSafeIncrement_Failed()
        {
            var stats = new ExecutionStatistics();

            stats.IncrementFailed();
            stats.IncrementFailed();

            Assert.AreEqual(2, stats.FailedTransactions);
        }

        [TestMethod]
        public void Test_ThreadSafeIncrement_Retried()
        {
            var stats = new ExecutionStatistics();

            stats.IncrementRetried();

            Assert.AreEqual(1, stats.RetriedTransactions);
        }

        [TestMethod]
        public void Test_ToString_Format()
        {
            var stats = new ExecutionStatistics
            {
                TotalTransactions = 100,
                BatchCount = 5,
                TotalExecutionTimeMs = 250,
                PeakParallelism = 20,
                ConflictsResolved = 3
            };

            var str = stats.ToString();

            Assert.IsTrue(str.Contains("100"));
            Assert.IsTrue(str.Contains("5"));
            Assert.IsTrue(str.Contains("250"));
            Assert.IsTrue(str.Contains("20"));
            Assert.IsTrue(str.Contains("3"));
        }
    }
}
