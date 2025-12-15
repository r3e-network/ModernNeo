// Copyright (C) 2015-2025 The Neo Project.
//
// UT_DefaultExecutionMetrics.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.Observability.Metrics;
using Neo.SmartContract;
using System;

namespace Neo.UnitTests.SmartContract
{
    [TestClass]
    public class UT_DefaultExecutionMetrics
    {
        [TestMethod]
        public void TestCreateWithDefaultProvider()
        {
            var metrics = new DefaultExecutionMetrics();
            Assert.IsNotNull(metrics);
            Assert.IsNotNull(metrics.MetricsProvider);
            Assert.IsInstanceOfType(metrics.MetricsProvider, typeof(DiagnosticsMetricsProvider));
        }

        [TestMethod]
        public void TestCreateWithCustomProvider()
        {
            using var provider = new DiagnosticsMetricsProvider("CustomMeter");
            var metrics = new DefaultExecutionMetrics(provider);

            Assert.IsNotNull(metrics);
            Assert.AreSame(provider, metrics.MetricsProvider);
        }

        [TestMethod]
        public void TestImplementsIExecutionMetrics()
        {
            var metrics = new DefaultExecutionMetrics();
            Assert.IsInstanceOfType(metrics, typeof(IExecutionMetrics));
        }

        [TestMethod]
        public void TestRecordExecutionStart()
        {
            using var provider = new DiagnosticsMetricsProvider("TestMeter");
            var metrics = new DefaultExecutionMetrics(provider);
            var contractHash = UInt160.Zero;

            // Should not throw
            metrics.RecordExecutionStart(contractHash, "transfer");
            metrics.RecordExecutionStart(contractHash, "balanceOf");

            // Verify counter was incremented (via provider)
            var counter = provider.CreateCounter("neo_contract_executions_total", "");
            Assert.AreEqual(2, counter.Value);
        }

        [TestMethod]
        public void TestRecordExecutionCompleteSuccess()
        {
            using var provider = new DiagnosticsMetricsProvider("TestMeter");
            var metrics = new DefaultExecutionMetrics(provider);
            var contractHash = UInt160.Zero;

            metrics.RecordExecutionComplete(contractHash, "transfer", 1000000, true, TimeSpan.FromMilliseconds(50));

            var successCounter = provider.CreateCounter("neo_contract_executions_success_total", "");
            var failureCounter = provider.CreateCounter("neo_contract_executions_failure_total", "");

            Assert.AreEqual(1, successCounter.Value);
            Assert.AreEqual(0, failureCounter.Value);
        }

        [TestMethod]
        public void TestRecordExecutionCompleteFailure()
        {
            using var provider = new DiagnosticsMetricsProvider("TestMeter");
            var metrics = new DefaultExecutionMetrics(provider);
            var contractHash = UInt160.Zero;

            metrics.RecordExecutionComplete(contractHash, "transfer", 500000, false, TimeSpan.FromMilliseconds(25));

            var successCounter = provider.CreateCounter("neo_contract_executions_success_total", "");
            var failureCounter = provider.CreateCounter("neo_contract_executions_failure_total", "");

            Assert.AreEqual(0, successCounter.Value);
            Assert.AreEqual(1, failureCounter.Value);
        }

        [TestMethod]
        public void TestRecordExecutionDurationAndGas()
        {
            using var provider = new DiagnosticsMetricsProvider("TestMeter");
            var metrics = new DefaultExecutionMetrics(provider);
            var contractHash = UInt160.Zero;

            metrics.RecordExecutionComplete(contractHash, "transfer", 1000000, true, TimeSpan.FromMilliseconds(100));
            metrics.RecordExecutionComplete(contractHash, "balanceOf", 500000, true, TimeSpan.FromMilliseconds(50));

            var durationHistogram = provider.CreateHistogram("neo_contract_execution_duration_ms", "");
            var gasHistogram = provider.CreateHistogram("neo_contract_gas_consumed", "");

            Assert.AreEqual(2, durationHistogram.Count);
            Assert.AreEqual(150.0, durationHistogram.Sum); // 100 + 50

            Assert.AreEqual(2, gasHistogram.Count);
            Assert.AreEqual(1500000.0, gasHistogram.Sum); // 1000000 + 500000
        }

        [TestMethod]
        public void TestRecordSyscall()
        {
            using var provider = new DiagnosticsMetricsProvider("TestMeter");
            var metrics = new DefaultExecutionMetrics(provider);

            metrics.RecordSyscall("System.Runtime.GetTime", 500);
            metrics.RecordSyscall("System.Storage.Get", 1000);
            metrics.RecordSyscall("System.Runtime.Notify", 300);

            var syscallCounter = provider.CreateCounter("neo_syscalls_total", "");
            Assert.AreEqual(3, syscallCounter.Value);
        }

        [TestMethod]
        public void TestRecordInstruction()
        {
            using var provider = new DiagnosticsMetricsProvider("TestMeter");
            var metrics = new DefaultExecutionMetrics(provider);

            metrics.RecordInstruction(0x00, 10); // NOP
            metrics.RecordInstruction(0x10, 20); // PUSH0
            metrics.RecordInstruction(0x40, 30); // SYSCALL

            var instructionCounter = provider.CreateCounter("neo_instructions_total", "");
            Assert.AreEqual(3, instructionCounter.Value);
        }

        [TestMethod]
        public void TestRecordStorageRead()
        {
            using var provider = new DiagnosticsMetricsProvider("TestMeter");
            var metrics = new DefaultExecutionMetrics(provider);

            metrics.RecordStorageOperation(StorageOperationType.Read, 32, 100);
            metrics.RecordStorageOperation(StorageOperationType.Read, 20, 50);

            var readCounter = provider.CreateCounter("neo_storage_reads_total", "");
            var keySizeHistogram = provider.CreateHistogram("neo_storage_key_size_bytes", "");

            Assert.AreEqual(2, readCounter.Value);
            Assert.AreEqual(2, keySizeHistogram.Count);
            Assert.AreEqual(52.0, keySizeHistogram.Sum); // 32 + 20
        }

        [TestMethod]
        public void TestRecordStorageWrite()
        {
            using var provider = new DiagnosticsMetricsProvider("TestMeter");
            var metrics = new DefaultExecutionMetrics(provider);

            metrics.RecordStorageOperation(StorageOperationType.Write, 32, 100);
            metrics.RecordStorageOperation(StorageOperationType.Write, 20, 200);

            var writeCounter = provider.CreateCounter("neo_storage_writes_total", "");
            var keySizeHistogram = provider.CreateHistogram("neo_storage_key_size_bytes", "");
            var valueSizeHistogram = provider.CreateHistogram("neo_storage_value_size_bytes", "");

            Assert.AreEqual(2, writeCounter.Value);
            Assert.AreEqual(2, keySizeHistogram.Count);
            Assert.AreEqual(52.0, keySizeHistogram.Sum); // 32 + 20
            Assert.AreEqual(2, valueSizeHistogram.Count);
            Assert.AreEqual(300.0, valueSizeHistogram.Sum); // 100 + 200
        }

        [TestMethod]
        public void TestRecordStorageDelete()
        {
            using var provider = new DiagnosticsMetricsProvider("TestMeter");
            var metrics = new DefaultExecutionMetrics(provider);

            metrics.RecordStorageOperation(StorageOperationType.Delete, 32, 0);
            metrics.RecordStorageOperation(StorageOperationType.Delete, 20, 0);

            var deleteCounter = provider.CreateCounter("neo_storage_deletes_total", "");
            Assert.AreEqual(2, deleteCounter.Value);
        }

        [TestMethod]
        public void TestRecordStorageFind()
        {
            using var provider = new DiagnosticsMetricsProvider("TestMeter");
            var metrics = new DefaultExecutionMetrics(provider);

            metrics.RecordStorageOperation(StorageOperationType.Find, 8, 0);
            metrics.RecordStorageOperation(StorageOperationType.Find, 16, 0);

            var findCounter = provider.CreateCounter("neo_storage_finds_total", "");
            Assert.AreEqual(2, findCounter.Value);
        }

        [TestMethod]
        public void TestAllStorageOperationTypes()
        {
            using var provider = new DiagnosticsMetricsProvider("TestMeter");
            var metrics = new DefaultExecutionMetrics(provider);

            metrics.RecordStorageOperation(StorageOperationType.Read, 10, 50);
            metrics.RecordStorageOperation(StorageOperationType.Write, 20, 100);
            metrics.RecordStorageOperation(StorageOperationType.Delete, 30, 0);
            metrics.RecordStorageOperation(StorageOperationType.Find, 5, 0);

            var readCounter = provider.CreateCounter("neo_storage_reads_total", "");
            var writeCounter = provider.CreateCounter("neo_storage_writes_total", "");
            var deleteCounter = provider.CreateCounter("neo_storage_deletes_total", "");
            var findCounter = provider.CreateCounter("neo_storage_finds_total", "");

            Assert.AreEqual(1, readCounter.Value);
            Assert.AreEqual(1, writeCounter.Value);
            Assert.AreEqual(1, deleteCounter.Value);
            Assert.AreEqual(1, findCounter.Value);
        }
    }
}
