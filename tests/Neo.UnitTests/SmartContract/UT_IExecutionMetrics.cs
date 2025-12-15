// Copyright (C) 2015-2025 The Neo Project.
//
// UT_IExecutionMetrics.cs file belongs to the neo project and is free
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
    /// <summary>
    /// Unit tests for the IExecutionMetrics interface and NullExecutionMetrics implementation.
    /// </summary>
    [TestClass]
    public class UT_IExecutionMetrics
    {
        [TestMethod]
        public void TestIExecutionMetricsInterfaceExists()
        {
            // Verify the interface exists and is accessible
            var interfaceType = typeof(IExecutionMetrics);
            Assert.IsNotNull(interfaceType);
            Assert.IsTrue(interfaceType.IsInterface);
        }

        [TestMethod]
        public void TestIExecutionMetricsHasMetricsProviderProperty()
        {
            var interfaceType = typeof(IExecutionMetrics);
            var prop = interfaceType.GetProperty("MetricsProvider");
            Assert.IsNotNull(prop);
            Assert.AreEqual(typeof(IMetricsProvider), prop.PropertyType);
        }

        [TestMethod]
        public void TestIExecutionMetricsHasRecordExecutionStartMethod()
        {
            var interfaceType = typeof(IExecutionMetrics);
            var method = interfaceType.GetMethod("RecordExecutionStart");
            Assert.IsNotNull(method);
        }

        [TestMethod]
        public void TestIExecutionMetricsHasRecordExecutionCompleteMethod()
        {
            var interfaceType = typeof(IExecutionMetrics);
            var method = interfaceType.GetMethod("RecordExecutionComplete");
            Assert.IsNotNull(method);
        }

        [TestMethod]
        public void TestIExecutionMetricsHasRecordSyscallMethod()
        {
            var interfaceType = typeof(IExecutionMetrics);
            var method = interfaceType.GetMethod("RecordSyscall");
            Assert.IsNotNull(method);
        }

        [TestMethod]
        public void TestIExecutionMetricsHasRecordInstructionMethod()
        {
            var interfaceType = typeof(IExecutionMetrics);
            var method = interfaceType.GetMethod("RecordInstruction");
            Assert.IsNotNull(method);
        }

        [TestMethod]
        public void TestIExecutionMetricsHasRecordStorageOperationMethod()
        {
            var interfaceType = typeof(IExecutionMetrics);
            var method = interfaceType.GetMethod("RecordStorageOperation");
            Assert.IsNotNull(method);
        }

        [TestMethod]
        public void TestStorageOperationTypeEnum()
        {
            // Verify enum values
            Assert.AreEqual(0, (int)StorageOperationType.Read);
            Assert.AreEqual(1, (int)StorageOperationType.Write);
            Assert.AreEqual(2, (int)StorageOperationType.Delete);
            Assert.AreEqual(3, (int)StorageOperationType.Find);
        }

        [TestMethod]
        public void TestNullExecutionMetricsSingleton()
        {
            // Verify singleton pattern
            var instance1 = NullExecutionMetrics.Instance;
            var instance2 = NullExecutionMetrics.Instance;
            Assert.AreSame(instance1, instance2);
        }

        [TestMethod]
        public void TestNullExecutionMetricsImplementsInterface()
        {
            // Verify NullExecutionMetrics implements IExecutionMetrics
            var nullMetricsType = typeof(NullExecutionMetrics);
            var interfaceType = typeof(IExecutionMetrics);
            Assert.IsTrue(interfaceType.IsAssignableFrom(nullMetricsType));
        }

        [TestMethod]
        public void TestNullExecutionMetricsReturnsNullMetricsProvider()
        {
            var metrics = NullExecutionMetrics.Instance;
            Assert.IsNotNull(metrics.MetricsProvider);
            Assert.AreSame(NullMetricsProvider.Instance, metrics.MetricsProvider);
        }

        [TestMethod]
        public void TestNullExecutionMetricsMethodsDoNotThrow()
        {
            var metrics = NullExecutionMetrics.Instance;
            var testHash = UInt160.Zero;

            // All methods should complete without throwing
            metrics.RecordExecutionStart(testHash, "test");
            metrics.RecordExecutionComplete(testHash, "test", 1000, true, TimeSpan.FromMilliseconds(100));
            metrics.RecordSyscall("System.Runtime.GetTime", 500);
            metrics.RecordInstruction(0x00, 10);
            metrics.RecordStorageOperation(StorageOperationType.Read, 32, 0);
            metrics.RecordStorageOperation(StorageOperationType.Write, 32, 100);
            metrics.RecordStorageOperation(StorageOperationType.Delete, 32, 0);
            metrics.RecordStorageOperation(StorageOperationType.Find, 8, 0);

            // If we get here, all methods completed without throwing
            Assert.IsTrue(true);
        }

        [TestMethod]
        public void TestApplicationEngineHasMetricsProperty()
        {
            // Verify ApplicationEngine has Metrics property
            var engineType = typeof(ApplicationEngine);
            var metricsProp = engineType.GetProperty("Metrics");
            Assert.IsNotNull(metricsProp);
            Assert.AreEqual(typeof(IExecutionMetrics), metricsProp.PropertyType);
        }
    }
}
