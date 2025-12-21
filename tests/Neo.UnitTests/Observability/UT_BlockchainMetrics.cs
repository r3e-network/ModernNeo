// Copyright (C) 2015-2025 The Neo Project.
//
// UT_BlockchainMetrics.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.Observability.Metrics;
using System;

namespace Neo.UnitTests.Observability
{
    [TestClass]
    public class UT_BlockchainMetrics
    {
        [TestMethod]
        public void TestCreateBlockchainMetrics()
        {
            using var provider = new OpenTelemetryMetricsProvider("Neo.Test");
            var metrics = new BlockchainMetrics(provider);

            Assert.IsNotNull(metrics);
            Assert.IsNotNull(metrics.BlocksProcessed);
            Assert.IsNotNull(metrics.BlockHeight);
            Assert.IsNotNull(metrics.BlockProcessingTime);
            Assert.IsNotNull(metrics.TransactionsProcessed);
            Assert.IsNotNull(metrics.MemPoolSize);
            Assert.IsNotNull(metrics.ConnectedPeers);
        }

        [TestMethod]
        public void TestCreateBlockchainMetricsNullProviderThrows()
        {
            Assert.ThrowsExactly<ArgumentNullException>(() => new BlockchainMetrics(null!));
        }

        [TestMethod]
        public void TestRecordBlockProcessed()
        {
            using var provider = new OpenTelemetryMetricsProvider("Neo.Test");
            var metrics = new BlockchainMetrics(provider);

            metrics.RecordBlockProcessed(height: 100, processingTimeMs: 50.0, sizeBytes: 4096, transactionCount: 10);

            Assert.AreEqual(1, metrics.BlocksProcessed.Value);
            Assert.AreEqual(100.0, metrics.BlockHeight.Value);
            Assert.AreEqual(10.0, metrics.BlockTransactionCount.Value);
            Assert.AreEqual(10, metrics.TransactionsProcessed.Value);
        }

        [TestMethod]
        public void TestRecordMultipleBlocks()
        {
            using var provider = new OpenTelemetryMetricsProvider("Neo.Test");
            var metrics = new BlockchainMetrics(provider);

            metrics.RecordBlockProcessed(height: 100, processingTimeMs: 50.0, sizeBytes: 4096, transactionCount: 10);
            metrics.RecordBlockProcessed(height: 101, processingTimeMs: 45.0, sizeBytes: 3000, transactionCount: 5);
            metrics.RecordBlockProcessed(height: 102, processingTimeMs: 60.0, sizeBytes: 5000, transactionCount: 15);

            Assert.AreEqual(3, metrics.BlocksProcessed.Value);
            Assert.AreEqual(102.0, metrics.BlockHeight.Value);
            Assert.AreEqual(30, metrics.TransactionsProcessed.Value); // 10 + 5 + 15
        }

        [TestMethod]
        public void TestRecordTransactionProcessed()
        {
            using var provider = new OpenTelemetryMetricsProvider("Neo.Test");
            var metrics = new BlockchainMetrics(provider);

            metrics.RecordTransactionProcessed(processingTimeMs: 5.0, success: true);
            metrics.RecordTransactionProcessed(processingTimeMs: 3.0, success: true);
            metrics.RecordTransactionProcessed(processingTimeMs: 10.0, success: false);

            Assert.AreEqual(1, metrics.TransactionsFailed.Value);
        }

        [TestMethod]
        public void TestUpdateMemPoolMetrics()
        {
            using var provider = new OpenTelemetryMetricsProvider("Neo.Test");
            var metrics = new BlockchainMetrics(provider);

            metrics.UpdateMemPoolMetrics(totalSize: 100, verifiedCount: 80, unverifiedCount: 20);

            Assert.AreEqual(100.0, metrics.MemPoolSize.Value);
            Assert.AreEqual(80.0, metrics.MemPoolVerifiedCount.Value);
            Assert.AreEqual(20.0, metrics.MemPoolUnverifiedCount.Value);
        }

        [TestMethod]
        public void TestUpdateNetworkMetrics()
        {
            using var provider = new OpenTelemetryMetricsProvider("Neo.Test");
            var metrics = new BlockchainMetrics(provider);

            metrics.UpdateNetworkMetrics(connectedPeers: 25);

            Assert.AreEqual(25.0, metrics.ConnectedPeers.Value);
        }

        [TestMethod]
        public void TestRecordMessageReceived()
        {
            using var provider = new OpenTelemetryMetricsProvider("Neo.Test");
            var metrics = new BlockchainMetrics(provider);

            metrics.RecordMessageReceived(latencyMs: 10.0);
            metrics.RecordMessageReceived(latencyMs: 15.0);
            metrics.RecordMessageReceived(); // No latency

            Assert.AreEqual(3, metrics.MessagesReceived.Value);
        }

        [TestMethod]
        public void TestRecordMessageSent()
        {
            using var provider = new OpenTelemetryMetricsProvider("Neo.Test");
            var metrics = new BlockchainMetrics(provider);

            metrics.RecordMessageSent();
            metrics.RecordMessageSent();

            Assert.AreEqual(2, metrics.MessagesSent.Value);
        }

        [TestMethod]
        public void TestRecordStorageOperations()
        {
            using var provider = new OpenTelemetryMetricsProvider("Neo.Test");
            var metrics = new BlockchainMetrics(provider);

            metrics.RecordStorageRead(durationMs: 0.5);
            metrics.RecordStorageRead(durationMs: 0.3);
            metrics.RecordStorageWrite(durationMs: 1.0);

            Assert.AreEqual(2, metrics.StorageReads.Value);
            Assert.AreEqual(1, metrics.StorageWrites.Value);
        }

        [TestMethod]
        public void TestBlockProcessingTimeHistogram()
        {
            using var provider = new OpenTelemetryMetricsProvider("Neo.Test");
            var metrics = new BlockchainMetrics(provider);

            metrics.RecordBlockProcessed(height: 100, processingTimeMs: 100.0, sizeBytes: 4096, transactionCount: 10);
            metrics.RecordBlockProcessed(height: 101, processingTimeMs: 200.0, sizeBytes: 4096, transactionCount: 10);

            Assert.AreEqual(2, metrics.BlockProcessingTime.Count);
            Assert.AreEqual(0.3, metrics.BlockProcessingTime.Sum, 0.001); // 100ms + 200ms = 300ms = 0.3s
        }

        [TestMethod]
        public void TestBlockSizeHistogram()
        {
            using var provider = new OpenTelemetryMetricsProvider("Neo.Test");
            var metrics = new BlockchainMetrics(provider);

            metrics.RecordBlockProcessed(height: 100, processingTimeMs: 50.0, sizeBytes: 1000, transactionCount: 5);
            metrics.RecordBlockProcessed(height: 101, processingTimeMs: 50.0, sizeBytes: 2000, transactionCount: 5);

            Assert.AreEqual(2, metrics.BlockSize.Count);
            Assert.AreEqual(3000.0, metrics.BlockSize.Sum);
        }

        [TestMethod]
        public void TestAllMetricsInitialized()
        {
            using var provider = new OpenTelemetryMetricsProvider("Neo.Test");
            var metrics = new BlockchainMetrics(provider);

            // Block metrics
            Assert.IsNotNull(metrics.BlocksProcessed);
            Assert.IsNotNull(metrics.BlockHeight);
            Assert.IsNotNull(metrics.BlockProcessingTime);
            Assert.IsNotNull(metrics.BlockSize);
            Assert.IsNotNull(metrics.BlockTransactionCount);

            // Transaction metrics
            Assert.IsNotNull(metrics.TransactionsProcessed);
            Assert.IsNotNull(metrics.TransactionsFailed);
            Assert.IsNotNull(metrics.TransactionProcessingTime);
            Assert.IsNotNull(metrics.MemPoolSize);
            Assert.IsNotNull(metrics.MemPoolVerifiedCount);
            Assert.IsNotNull(metrics.MemPoolUnverifiedCount);

            // Network metrics
            Assert.IsNotNull(metrics.ConnectedPeers);
            Assert.IsNotNull(metrics.MessagesReceived);
            Assert.IsNotNull(metrics.MessagesSent);
            Assert.IsNotNull(metrics.MessageLatency);

            // Storage metrics
            Assert.IsNotNull(metrics.StorageReadTime);
            Assert.IsNotNull(metrics.StorageWriteTime);
            Assert.IsNotNull(metrics.StorageReads);
            Assert.IsNotNull(metrics.StorageWrites);
        }
    }
}
