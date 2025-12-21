// Copyright (C) 2015-2025 The Neo Project.
//
// UT_ConsensusMetrics.cs file belongs to the neo project and is free
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
    public class UT_ConsensusMetrics
    {
        [TestMethod]
        public void TestCreateConsensusMetrics()
        {
            using var provider = new OpenTelemetryMetricsProvider("Neo.Test");
            var metrics = new ConsensusMetrics(provider);

            Assert.IsNotNull(metrics);
            Assert.IsNotNull(metrics.ConsensusRoundsTotal);
            Assert.IsNotNull(metrics.ConsensusRoundsSuccessful);
            Assert.IsNotNull(metrics.ConsensusRoundsFailed);
            Assert.IsNotNull(metrics.CurrentView);
            Assert.IsNotNull(metrics.ValidatorCount);
        }

        [TestMethod]
        public void TestCreateConsensusMetricsNullProviderThrows()
        {
            Assert.ThrowsExactly<ArgumentNullException>(() => new ConsensusMetrics(null!));
        }

        [TestMethod]
        public void TestRecordRoundStart()
        {
            using var provider = new OpenTelemetryMetricsProvider("Neo.Test");
            var metrics = new ConsensusMetrics(provider);

            metrics.RecordRoundStart(view: 0, primaryIndex: 2, validatorCount: 7);

            Assert.AreEqual(1, metrics.ConsensusRoundsTotal.Value);
            Assert.AreEqual(0.0, metrics.CurrentView.Value);
            Assert.AreEqual(2.0, metrics.CurrentPrimaryIndex.Value);
            Assert.AreEqual(7.0, metrics.ValidatorCount.Value);
        }

        [TestMethod]
        public void TestRecordRoundSuccess()
        {
            using var provider = new OpenTelemetryMetricsProvider("Neo.Test");
            var metrics = new ConsensusMetrics(provider);

            metrics.RecordRoundStart(view: 0, primaryIndex: 0, validatorCount: 7);
            metrics.RecordRoundSuccess(durationMs: 5000.0);

            Assert.AreEqual(1, metrics.ConsensusRoundsSuccessful.Value);
            Assert.AreEqual(1, metrics.ConsensusRoundDuration.Count);
            Assert.AreEqual(5.0, metrics.ConsensusRoundDuration.Sum, 0.001); // 5000ms = 5s
        }

        [TestMethod]
        public void TestRecordRoundFailure()
        {
            using var provider = new OpenTelemetryMetricsProvider("Neo.Test");
            var metrics = new ConsensusMetrics(provider);

            metrics.RecordRoundStart(view: 0, primaryIndex: 0, validatorCount: 7);
            metrics.RecordRoundFailure();

            Assert.AreEqual(1, metrics.ConsensusRoundsFailed.Value);
        }

        [TestMethod]
        public void TestRecordPrepareRequest()
        {
            using var provider = new OpenTelemetryMetricsProvider("Neo.Test");
            var metrics = new ConsensusMetrics(provider);

            // Sent
            metrics.RecordPrepareRequest(sent: true);
            Assert.AreEqual(1, metrics.BlocksProposed.Value);

            // Received with latency
            metrics.RecordPrepareRequest(sent: false, latencyMs: 100.0);
        }

        [TestMethod]
        public void TestRecordPrepareResponse()
        {
            using var provider = new OpenTelemetryMetricsProvider("Neo.Test");
            var metrics = new ConsensusMetrics(provider);

            metrics.RecordPrepareResponse(sent: true);
            metrics.RecordPrepareResponse(sent: false);
            metrics.RecordPrepareResponse(sent: false);

            // PrepareResponse counters are internal, just verify no exceptions
        }

        [TestMethod]
        public void TestRecordCommit()
        {
            using var provider = new OpenTelemetryMetricsProvider("Neo.Test");
            var metrics = new ConsensusMetrics(provider);

            metrics.RecordCommit(sent: true, latencyMs: 500.0);
            metrics.RecordCommit(sent: false, latencyMs: 450.0);

            // Commit counters are internal, just verify no exceptions
        }

        [TestMethod]
        public void TestRecordChangeView()
        {
            using var provider = new OpenTelemetryMetricsProvider("Neo.Test");
            var metrics = new ConsensusMetrics(provider);

            metrics.RecordChangeView(sent: true);
            metrics.RecordChangeView(sent: false);

            Assert.AreEqual(2, metrics.ViewChanges.Value);
        }

        [TestMethod]
        public void TestRecordRecoveryMessage()
        {
            using var provider = new OpenTelemetryMetricsProvider("Neo.Test");
            var metrics = new ConsensusMetrics(provider);

            metrics.RecordRecoveryMessage(sent: true);
            metrics.RecordRecoveryMessage(sent: false);

            // Recovery message counters are internal, just verify no exceptions
        }

        [TestMethod]
        public void TestRecordBlockCommitted()
        {
            using var provider = new OpenTelemetryMetricsProvider("Neo.Test");
            var metrics = new ConsensusMetrics(provider);

            metrics.RecordBlockCommitted(finalizationTimeMs: 3000.0);
            metrics.RecordBlockCommitted(finalizationTimeMs: 2500.0);

            Assert.AreEqual(2, metrics.BlocksCommitted.Value);
        }

        [TestMethod]
        public void TestRecordInvalidMessage()
        {
            using var provider = new OpenTelemetryMetricsProvider("Neo.Test");
            var metrics = new ConsensusMetrics(provider);

            metrics.RecordInvalidMessage();
            metrics.RecordInvalidMessage();
            metrics.RecordInvalidMessage();

            Assert.AreEqual(3, metrics.InvalidMessages.Value);
        }

        [TestMethod]
        public void TestRecordTimeout()
        {
            using var provider = new OpenTelemetryMetricsProvider("Neo.Test");
            var metrics = new ConsensusMetrics(provider);

            metrics.RecordTimeout();
            metrics.RecordTimeout();

            Assert.AreEqual(2, metrics.Timeouts.Value);
        }

        [TestMethod]
        public void TestUpdateActiveValidators()
        {
            using var provider = new OpenTelemetryMetricsProvider("Neo.Test");
            var metrics = new ConsensusMetrics(provider);

            metrics.UpdateActiveValidators(count: 5);
            Assert.AreEqual(5.0, metrics.ActiveValidators.Value);

            metrics.UpdateActiveValidators(count: 7);
            Assert.AreEqual(7.0, metrics.ActiveValidators.Value);
        }

        [TestMethod]
        public void TestSetMyValidatorIndex()
        {
            using var provider = new OpenTelemetryMetricsProvider("Neo.Test");
            var metrics = new ConsensusMetrics(provider);

            // Not a validator
            metrics.SetMyValidatorIndex(-1);

            // Became a validator
            metrics.SetMyValidatorIndex(3);
        }

        [TestMethod]
        public void TestFullConsensusRoundScenario()
        {
            using var provider = new OpenTelemetryMetricsProvider("Neo.Test");
            var metrics = new ConsensusMetrics(provider);

            // Round starts
            metrics.RecordRoundStart(view: 0, primaryIndex: 0, validatorCount: 7);
            metrics.SetMyValidatorIndex(0);
            metrics.UpdateActiveValidators(7);

            // Primary sends PrepareRequest
            metrics.RecordPrepareRequest(sent: true);

            // Receive PrepareResponses from other validators
            for (int i = 0; i < 4; i++)
            {
                metrics.RecordPrepareResponse(sent: false);
            }

            // Send our PrepareResponse
            metrics.RecordPrepareResponse(sent: true);

            // Receive Commits
            for (int i = 0; i < 4; i++)
            {
                metrics.RecordCommit(sent: false, latencyMs: 100.0 + i * 10);
            }

            // Send our Commit
            metrics.RecordCommit(sent: true, latencyMs: 150.0);

            // Block committed
            metrics.RecordBlockCommitted(finalizationTimeMs: 2000.0);

            // Round success
            metrics.RecordRoundSuccess(durationMs: 3000.0);

            // Verify final state
            Assert.AreEqual(1, metrics.ConsensusRoundsTotal.Value);
            Assert.AreEqual(1, metrics.ConsensusRoundsSuccessful.Value);
            Assert.AreEqual(0, metrics.ConsensusRoundsFailed.Value);
            Assert.AreEqual(1, metrics.BlocksProposed.Value);
            Assert.AreEqual(1, metrics.BlocksCommitted.Value);
        }

        [TestMethod]
        public void TestViewChangeScenario()
        {
            using var provider = new OpenTelemetryMetricsProvider("Neo.Test");
            var metrics = new ConsensusMetrics(provider);

            // First round starts
            metrics.RecordRoundStart(view: 0, primaryIndex: 0, validatorCount: 7);

            // Timeout occurs
            metrics.RecordTimeout();

            // View change
            metrics.RecordChangeView(sent: true);

            // Round fails
            metrics.RecordRoundFailure();

            // New round with view 1
            metrics.RecordRoundStart(view: 1, primaryIndex: 1, validatorCount: 7);

            Assert.AreEqual(2, metrics.ConsensusRoundsTotal.Value);
            Assert.AreEqual(1, metrics.ConsensusRoundsFailed.Value);
            Assert.AreEqual(1, metrics.Timeouts.Value);
            Assert.AreEqual(1, metrics.ViewChanges.Value);
            Assert.AreEqual(1.0, metrics.CurrentView.Value);
        }

        [TestMethod]
        public void TestAllMetricsInitialized()
        {
            using var provider = new OpenTelemetryMetricsProvider("Neo.Test");
            var metrics = new ConsensusMetrics(provider);

            // Round metrics
            Assert.IsNotNull(metrics.ConsensusRoundsTotal);
            Assert.IsNotNull(metrics.ConsensusRoundsSuccessful);
            Assert.IsNotNull(metrics.ConsensusRoundsFailed);
            Assert.IsNotNull(metrics.ConsensusRoundDuration);
            Assert.IsNotNull(metrics.CurrentView);
            Assert.IsNotNull(metrics.CurrentPrimaryIndex);

            // Validator metrics
            Assert.IsNotNull(metrics.ValidatorCount);
            Assert.IsNotNull(metrics.ActiveValidators);
            Assert.IsNotNull(metrics.BlocksProposed);
            Assert.IsNotNull(metrics.BlocksCommitted);

            // Error metrics
            Assert.IsNotNull(metrics.InvalidMessages);
            Assert.IsNotNull(metrics.Timeouts);
            Assert.IsNotNull(metrics.ViewChanges);
        }
    }
}
