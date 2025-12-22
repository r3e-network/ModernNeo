// Copyright (C) 2015-2025 The Neo Project.
//
// UT_ConsensusTracing.cs file belongs to the neo project and is free
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
    public class UT_ConsensusTracing
    {
        private ActivityListener _listener = null!;
        private List<Activity> _capturedActivities = null!;

        [TestInitialize]
        public void Setup()
        {
            _capturedActivities = new List<Activity>();
            _listener = new ActivityListener
            {
                ShouldListenTo = source => source.Name == ConsensusTracing.ActivitySourceName,
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
            Assert.AreEqual("Neo.Consensus", ConsensusTracing.ActivitySourceName);
        }

        [TestMethod]
        public void ActivitySource_IsNotNull()
        {
            Assert.IsNotNull(ConsensusTracing.ActivitySource);
        }

        #endregion

        #region Consensus Round Span Tests

        [TestMethod]
        public void StartConsensusRoundSpan_CreatesActivity()
        {
            using var activity = ConsensusTracing.StartConsensusRoundSpan(12345, 0, 3);

            Assert.IsNotNull(activity);
            Assert.AreEqual("consensus.round", activity.OperationName);
            Assert.AreEqual(ActivityKind.Internal, activity.Kind);
        }

        [TestMethod]
        public void StartConsensusRoundSpan_SetsCorrectTags()
        {
            using var activity = ConsensusTracing.StartConsensusRoundSpan(12345, 2, 5);

            Assert.IsNotNull(activity);
            Assert.AreEqual(12345u, activity.GetTagItem("neo.consensus.block_index"));
            Assert.AreEqual((byte)2, activity.GetTagItem("neo.consensus.view_number"));
            Assert.AreEqual(5, activity.GetTagItem("neo.consensus.validator_index"));
        }

        [TestMethod]
        public void StartPrepareRequestSpan_CreatesActivity()
        {
            using var activity = ConsensusTracing.StartPrepareRequestSpan(1000, 0, 50);

            Assert.IsNotNull(activity);
            Assert.AreEqual("consensus.prepare_request", activity.OperationName);
            Assert.AreEqual(ActivityKind.Producer, activity.Kind);
        }

        [TestMethod]
        public void StartPrepareRequestSpan_SetsCorrectTags()
        {
            using var activity = ConsensusTracing.StartPrepareRequestSpan(1000, 1, 100);

            Assert.IsNotNull(activity);
            Assert.AreEqual(1000u, activity.GetTagItem("neo.consensus.block_index"));
            Assert.AreEqual((byte)1, activity.GetTagItem("neo.consensus.view_number"));
            Assert.AreEqual(100, activity.GetTagItem("neo.consensus.tx_count"));
            Assert.AreEqual("PrepareRequest", activity.GetTagItem("neo.consensus.message_type"));
        }

        [TestMethod]
        public void StartPrepareResponseSpan_CreatesActivity()
        {
            using var activity = ConsensusTracing.StartPrepareResponseSpan(1000, 0, 2);

            Assert.IsNotNull(activity);
            Assert.AreEqual("consensus.prepare_response", activity.OperationName);
            Assert.AreEqual(ActivityKind.Consumer, activity.Kind);
            Assert.AreEqual("PrepareResponse", activity.GetTagItem("neo.consensus.message_type"));
        }

        [TestMethod]
        public void StartCommitSpan_CreatesActivity()
        {
            using var activity = ConsensusTracing.StartCommitSpan(1000, 0, 4);

            Assert.IsNotNull(activity);
            Assert.AreEqual("consensus.commit", activity.OperationName);
            Assert.AreEqual(ActivityKind.Internal, activity.Kind);
            Assert.AreEqual("Commit", activity.GetTagItem("neo.consensus.message_type"));
        }

        [TestMethod]
        public void StartViewChangeSpan_CreatesActivity()
        {
            using var activity = ConsensusTracing.StartViewChangeSpan(1000, 0, 1, "Timeout");

            Assert.IsNotNull(activity);
            Assert.AreEqual("consensus.view_change", activity.OperationName);
            Assert.AreEqual((byte)0, activity.GetTagItem("neo.consensus.old_view"));
            Assert.AreEqual((byte)1, activity.GetTagItem("neo.consensus.new_view"));
            Assert.AreEqual("Timeout", activity.GetTagItem("neo.consensus.view_change_reason"));
        }

        [TestMethod]
        public void StartViewChangeSpan_WithoutReason_DoesNotSetReasonTag()
        {
            using var activity = ConsensusTracing.StartViewChangeSpan(1000, 0, 1);

            Assert.IsNotNull(activity);
            Assert.IsNull(activity.GetTagItem("neo.consensus.view_change_reason"));
        }

        #endregion

        #region Phase Tracking Tests

        [TestMethod]
        public void RecordPhaseTransition_SetsTagsAndEvent()
        {
            using var activity = ConsensusTracing.StartConsensusRoundSpan(1000, 0, 0);
            ConsensusTracing.RecordPhaseTransition(activity, "Initial", "Primary");

            Assert.AreEqual("Primary", activity?.GetTagItem("neo.consensus.phase"));
            var events = activity?.Events.ToList();
            Assert.IsNotNull(events);
            Assert.AreEqual(1, events.Count);
            Assert.AreEqual("phase.transition", events[0].Name);
        }

        [TestMethod]
        public void RecordPhaseTransition_WithNullActivity_DoesNotThrow()
        {
            // Should not throw
            ConsensusTracing.RecordPhaseTransition(null, "Initial", "Primary");
        }

        [TestMethod]
        public void RecordSignatureProgress_SetsTags()
        {
            using var activity = ConsensusTracing.StartConsensusRoundSpan(1000, 0, 0);
            ConsensusTracing.RecordSignatureProgress(activity, 5, 3, 5);

            Assert.AreEqual(5, activity?.GetTagItem("neo.consensus.prepare_count"));
            Assert.AreEqual(3, activity?.GetTagItem("neo.consensus.commit_count"));
            Assert.AreEqual(5, activity?.GetTagItem("neo.consensus.required_count"));
        }

        #endregion

        #region Block Correlation Tests

        [TestMethod]
        public void AddBlockCorrelation_SetsTagsAndEvent()
        {
            using var activity = ConsensusTracing.StartConsensusRoundSpan(1000, 0, 0);
            var blockHash = "0xabcdef1234567890";
            ConsensusTracing.AddBlockCorrelation(activity, blockHash, 1000);

            Assert.AreEqual(blockHash, activity?.GetTagItem("neo.block.hash"));
            Assert.AreEqual(1000u, activity?.GetTagItem("neo.block.index"));
            var events = activity?.Events.ToList();
            Assert.IsNotNull(events);
            Assert.AreEqual(1, events.Count);
            Assert.AreEqual("block.proposed", events[0].Name);
        }

        [TestMethod]
        public void AddBlockCorrelation_WithEmptyHash_DoesNotSetTags()
        {
            using var activity = ConsensusTracing.StartConsensusRoundSpan(1000, 0, 0);
            ConsensusTracing.AddBlockCorrelation(activity, "", 1000);

            Assert.IsNull(activity?.GetTagItem("neo.block.hash"));
        }

        [TestMethod]
        public void RecordConsensusComplete_SetsStatusAndTags()
        {
            using var activity = ConsensusTracing.StartConsensusRoundSpan(1000, 0, 0);
            ConsensusTracing.RecordConsensusComplete(activity, "0xabcdef", 50, 1500.5);

            Assert.AreEqual(ActivityStatusCode.Ok, activity?.Status);
            Assert.AreEqual("0xabcdef", activity?.GetTagItem("neo.block.hash"));
            Assert.AreEqual(50, activity?.GetTagItem("neo.consensus.tx_count"));
            Assert.AreEqual(1500.5, activity?.GetTagItem("neo.consensus.duration_ms"));
        }

        #endregion

        #region Error Recording Tests

        [TestMethod]
        public void RecordConsensusFailure_SetsErrorStatus()
        {
            using var activity = ConsensusTracing.StartConsensusRoundSpan(1000, 0, 0);
            ConsensusTracing.RecordConsensusFailure(activity, "View change timeout");

            Assert.AreEqual(ActivityStatusCode.Error, activity?.Status);
            Assert.AreEqual("View change timeout", activity?.GetTagItem("neo.consensus.failure_reason"));
        }

        [TestMethod]
        public void RecordException_SetsErrorStatusAndEvent()
        {
            using var activity = ConsensusTracing.StartConsensusRoundSpan(1000, 0, 0);
            var exception = new InvalidOperationException("Test error");
            ConsensusTracing.RecordException(activity, exception);

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
            ConsensusTracing.RecordException(null, exception);
        }

        #endregion
    }
}
