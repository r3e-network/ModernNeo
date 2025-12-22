// Copyright (C) 2015-2025 The Neo Project.
//
// UT_BlockExecutionTracing.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

#nullable enable

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.Observability.Tracing;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Neo.UnitTests.Observability
{
    [TestClass]
    public class UT_BlockExecutionTracing
    {
        private ActivityListener _listener = null!;
        private List<Activity> _capturedActivities = null!;

        [TestInitialize]
        public void Setup()
        {
            _capturedActivities = new List<Activity>();
            _listener = new ActivityListener
            {
                ShouldListenTo = source => source.Name == BlockExecutionTracing.ActivitySourceName,
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
                ActivityStarted = activity => _capturedActivities.Add(activity)
            };
            ActivitySource.AddActivityListener(_listener);
        }

        [TestCleanup]
        public void Cleanup()
        {
            _listener.Dispose();
        }

        #region ActivitySource Tests

        [TestMethod]
        public void ActivitySourceName_IsCorrect()
        {
            Assert.AreEqual("Neo.BlockExecution", BlockExecutionTracing.ActivitySourceName);
        }

        [TestMethod]
        public void ActivitySource_IsNotNull()
        {
            Assert.IsNotNull(BlockExecutionTracing.ActivitySource);
        }

        #endregion

        #region Block Execution Span Tests

        [TestMethod]
        public void StartBlockExecutionSpan_CreatesActivity()
        {
            using var activity = BlockExecutionTracing.StartBlockExecutionSpan(12345, "0xabcdef", 100);

            Assert.IsNotNull(activity);
            Assert.AreEqual("block.execute", activity.OperationName);
            Assert.AreEqual(ActivityKind.Internal, activity.Kind);
        }

        [TestMethod]
        public void StartBlockExecutionSpan_SetsCorrectTags()
        {
            using var activity = BlockExecutionTracing.StartBlockExecutionSpan(12345, "0xabcdef", 100);

            Assert.IsNotNull(activity);
            Assert.AreEqual(12345u, activity.GetTagItem("neo.block.index"));
            Assert.AreEqual("0xabcdef", activity.GetTagItem("neo.block.hash"));
            Assert.AreEqual(100, activity.GetTagItem("neo.block.tx_count"));
        }

        [TestMethod]
        public void StartBlockExecutionSpan_WithNullHash_DoesNotSetHashTag()
        {
            using var activity = BlockExecutionTracing.StartBlockExecutionSpan(12345, null, 100);

            Assert.IsNotNull(activity);
            Assert.IsNull(activity.GetTagItem("neo.block.hash"));
        }

        [TestMethod]
        public void StartBlockPersistSpan_CreatesActivity()
        {
            using var activity = BlockExecutionTracing.StartBlockPersistSpan(12345, "0xabcdef");

            Assert.IsNotNull(activity);
            Assert.AreEqual("block.persist", activity.OperationName);
            Assert.AreEqual(ActivityKind.Internal, activity.Kind);
        }

        [TestMethod]
        public void StartBlockValidationSpan_CreatesActivity()
        {
            using var activity = BlockExecutionTracing.StartBlockValidationSpan(12345, "0xabcdef");

            Assert.IsNotNull(activity);
            Assert.AreEqual("block.validate", activity.OperationName);
            Assert.AreEqual(ActivityKind.Internal, activity.Kind);
        }

        #endregion

        #region Transaction Execution Span Tests

        [TestMethod]
        public void StartTransactionExecutionSpan_CreatesActivity()
        {
            using var activity = BlockExecutionTracing.StartTransactionExecutionSpan("0x1234", 5, 12345);

            Assert.IsNotNull(activity);
            Assert.AreEqual("tx.execute", activity.OperationName);
            Assert.AreEqual(ActivityKind.Internal, activity.Kind);
        }

        [TestMethod]
        public void StartTransactionExecutionSpan_SetsCorrectTags()
        {
            using var activity = BlockExecutionTracing.StartTransactionExecutionSpan("0x1234", 5, 12345);

            Assert.IsNotNull(activity);
            Assert.AreEqual("0x1234", activity.GetTagItem("neo.tx.hash"));
            Assert.AreEqual(5, activity.GetTagItem("neo.tx.index"));
            Assert.AreEqual(12345u, activity.GetTagItem("neo.block.index"));
        }

        [TestMethod]
        public void StartTransactionVerificationSpan_CreatesActivity()
        {
            using var activity = BlockExecutionTracing.StartTransactionVerificationSpan("0x1234");

            Assert.IsNotNull(activity);
            Assert.AreEqual("tx.verify", activity.OperationName);
            Assert.AreEqual("0x1234", activity.GetTagItem("neo.tx.hash"));
        }

        [TestMethod]
        public void StartContractInvocationSpan_CreatesActivity()
        {
            using var activity = BlockExecutionTracing.StartContractInvocationSpan("0x1234", "0xcontract", "transfer");

            Assert.IsNotNull(activity);
            Assert.AreEqual("contract.invoke", activity.OperationName);
            Assert.AreEqual("0x1234", activity.GetTagItem("neo.tx.hash"));
            Assert.AreEqual("0xcontract", activity.GetTagItem("neo.contract.hash"));
            Assert.AreEqual("transfer", activity.GetTagItem("neo.contract.method"));
        }

        [TestMethod]
        public void StartContractInvocationSpan_WithoutMethod_DoesNotSetMethodTag()
        {
            using var activity = BlockExecutionTracing.StartContractInvocationSpan("0x1234", "0xcontract");

            Assert.IsNotNull(activity);
            Assert.IsNull(activity.GetTagItem("neo.contract.method"));
        }

        #endregion

        #region Parallel Execution Span Tests

        [TestMethod]
        public void StartParallelBatchSpan_CreatesActivity()
        {
            using var activity = BlockExecutionTracing.StartParallelBatchSpan(12345, 2, 25);

            Assert.IsNotNull(activity);
            Assert.AreEqual("block.parallel_batch", activity.OperationName);
            Assert.AreEqual(ActivityKind.Internal, activity.Kind);
        }

        [TestMethod]
        public void StartParallelBatchSpan_SetsCorrectTags()
        {
            using var activity = BlockExecutionTracing.StartParallelBatchSpan(12345, 2, 25);

            Assert.IsNotNull(activity);
            Assert.AreEqual(12345u, activity.GetTagItem("neo.block.index"));
            Assert.AreEqual(2, activity.GetTagItem("neo.execution.batch_index"));
            Assert.AreEqual(25, activity.GetTagItem("neo.execution.batch_tx_count"));
            Assert.AreEqual("parallel", activity.GetTagItem("neo.execution.mode"));
        }

        [TestMethod]
        public void StartDependencyAnalysisSpan_CreatesActivity()
        {
            using var activity = BlockExecutionTracing.StartDependencyAnalysisSpan(12345, 100);

            Assert.IsNotNull(activity);
            Assert.AreEqual("block.dependency_analysis", activity.OperationName);
            Assert.AreEqual(12345u, activity.GetTagItem("neo.block.index"));
            Assert.AreEqual(100, activity.GetTagItem("neo.execution.tx_count"));
        }

        #endregion

        #region Recording Method Tests

        [TestMethod]
        public void RecordTransactionResult_HALT_SetsOkStatus()
        {
            using var activity = BlockExecutionTracing.StartTransactionExecutionSpan("0x1234", 0, 12345);
            BlockExecutionTracing.RecordTransactionResult(activity, "HALT", 1000000, 3);

            Assert.AreEqual(ActivityStatusCode.Ok, activity?.Status);
            Assert.AreEqual("HALT", activity?.GetTagItem("neo.tx.vm_state"));
            Assert.AreEqual(1000000L, activity?.GetTagItem("neo.tx.gas_consumed"));
            Assert.AreEqual(3, activity?.GetTagItem("neo.tx.notification_count"));
        }

        [TestMethod]
        public void RecordTransactionResult_FAULT_SetsErrorStatus()
        {
            using var activity = BlockExecutionTracing.StartTransactionExecutionSpan("0x1234", 0, 12345);
            BlockExecutionTracing.RecordTransactionResult(activity, "FAULT", 500000, 0);

            Assert.AreEqual(ActivityStatusCode.Error, activity?.Status);
            Assert.AreEqual("FAULT", activity?.GetTagItem("neo.tx.vm_state"));
        }

        [TestMethod]
        public void RecordTransactionResult_WithNullActivity_DoesNotThrow()
        {
            // Should not throw
            BlockExecutionTracing.RecordTransactionResult(null, "HALT", 1000000, 0);
        }

        [TestMethod]
        public void RecordBlockExecutionComplete_SetsTagsAndEvent()
        {
            using var activity = BlockExecutionTracing.StartBlockExecutionSpan(12345, "0xabcdef", 100);
            BlockExecutionTracing.RecordBlockExecutionComplete(activity, 98, 2, 50000000, 1500.5);

            Assert.AreEqual(ActivityStatusCode.Ok, activity?.Status);
            Assert.AreEqual(98, activity?.GetTagItem("neo.block.success_count"));
            Assert.AreEqual(2, activity?.GetTagItem("neo.block.fault_count"));
            Assert.AreEqual(50000000L, activity?.GetTagItem("neo.block.total_gas"));
            Assert.AreEqual(1500.5, activity?.GetTagItem("neo.block.execution_time_ms"));

            var events = activity?.Events.ToList();
            Assert.IsNotNull(events);
            Assert.AreEqual(1, events.Count);
            Assert.AreEqual("block.execution_complete", events[0].Name);
        }

        [TestMethod]
        public void RecordBlockExecutionComplete_NoFaults_SetsOkStatus()
        {
            using var activity = BlockExecutionTracing.StartBlockExecutionSpan(12345, "0xabcdef", 100);
            BlockExecutionTracing.RecordBlockExecutionComplete(activity, 100, 0, 50000000, 1500.5);

            Assert.AreEqual(ActivityStatusCode.Ok, activity?.Status);
        }

        [TestMethod]
        public void RecordParallelExecutionStats_SetsTags()
        {
            using var activity = BlockExecutionTracing.StartBlockExecutionSpan(12345, "0xabcdef", 100);
            BlockExecutionTracing.RecordParallelExecutionStats(activity, 5, 8, 3);

            Assert.AreEqual(5, activity?.GetTagItem("neo.execution.batch_count"));
            Assert.AreEqual(8, activity?.GetTagItem("neo.execution.max_parallelism"));
            Assert.AreEqual(3, activity?.GetTagItem("neo.execution.conflict_count"));
        }

        [TestMethod]
        public void RecordDependencyAnalysisResult_SetsTags()
        {
            using var activity = BlockExecutionTracing.StartDependencyAnalysisSpan(12345, 100);
            BlockExecutionTracing.RecordDependencyAnalysisResult(activity, 80, 20, 5);

            Assert.AreEqual(ActivityStatusCode.Ok, activity?.Status);
            Assert.AreEqual(80, activity?.GetTagItem("neo.execution.independent_count"));
            Assert.AreEqual(20, activity?.GetTagItem("neo.execution.dependent_count"));
            Assert.AreEqual(5, activity?.GetTagItem("neo.execution.group_count"));
        }

        [TestMethod]
        public void RecordContractInvocationResult_Success_SetsOkStatus()
        {
            using var activity = BlockExecutionTracing.StartContractInvocationSpan("0x1234", "0xcontract", "transfer");
            BlockExecutionTracing.RecordContractInvocationResult(activity, true, 500000, "Boolean");

            Assert.AreEqual(ActivityStatusCode.Ok, activity?.Status);
            Assert.AreEqual(true, activity?.GetTagItem("neo.contract.success"));
            Assert.AreEqual(500000L, activity?.GetTagItem("neo.contract.gas_consumed"));
            Assert.AreEqual("Boolean", activity?.GetTagItem("neo.contract.return_type"));
        }

        [TestMethod]
        public void RecordContractInvocationResult_Failure_SetsErrorStatus()
        {
            using var activity = BlockExecutionTracing.StartContractInvocationSpan("0x1234", "0xcontract", "transfer");
            BlockExecutionTracing.RecordContractInvocationResult(activity, false, 500000);

            Assert.AreEqual(ActivityStatusCode.Error, activity?.Status);
            Assert.AreEqual(false, activity?.GetTagItem("neo.contract.success"));
        }

        [TestMethod]
        public void RecordException_SetsErrorStatusAndEvent()
        {
            using var activity = BlockExecutionTracing.StartBlockExecutionSpan(12345, "0xabcdef", 100);
            var exception = new InvalidOperationException("Test error");
            BlockExecutionTracing.RecordException(activity, exception);

            Assert.AreEqual(ActivityStatusCode.Error, activity?.Status);
            var events = activity?.Events.ToList();
            Assert.IsNotNull(events);
            Assert.AreEqual(1, events.Count);
            Assert.AreEqual("exception", events[0].Name);
        }

        [TestMethod]
        public void RecordException_WithNullActivity_DoesNotThrow()
        {
            var exception = new InvalidOperationException("Test");
            // Should not throw
            BlockExecutionTracing.RecordException(null, exception);
        }

        [TestMethod]
        public void RecordSuccess_SetsOkStatus()
        {
            using var activity = BlockExecutionTracing.StartBlockExecutionSpan(12345, "0xabcdef", 100);
            BlockExecutionTracing.RecordSuccess(activity);

            Assert.AreEqual(ActivityStatusCode.Ok, activity?.Status);
        }

        [TestMethod]
        public void RecordSuccess_WithNullActivity_DoesNotThrow()
        {
            // Should not throw
            BlockExecutionTracing.RecordSuccess(null);
        }

        #endregion

        #region Span Link Tests

        [TestMethod]
        public void CreateTransactionLink_ReturnsValidLink()
        {
            var link = BlockExecutionTracing.CreateTransactionLink("0x1234", 12345);

            Assert.IsNotNull(link.Attributes);
            Assert.AreEqual("0x1234", link.Attributes["neo.tx.hash"]);
            Assert.AreEqual(12345u, link.Attributes["neo.block.index"]);
        }

        [TestMethod]
        public void CreateBlockLink_ReturnsValidLink()
        {
            var link = BlockExecutionTracing.CreateBlockLink("0xabcdef", 12345);

            Assert.IsNotNull(link.Attributes);
            Assert.AreEqual("0xabcdef", link.Attributes["neo.block.hash"]);
            Assert.AreEqual(12345u, link.Attributes["neo.block.index"]);
        }

        #endregion
    }
}
