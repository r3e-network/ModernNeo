// Copyright (C) 2015-2025 The Neo Project.
//
// UT_TracingSampler.cs file belongs to the neo project and is free
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
using System.Diagnostics;

namespace Neo.UnitTests.Observability
{
    [TestClass]
    public class UT_TracingSampler
    {
        #region AlwaysOnSampler Tests

        [TestMethod]
        public void AlwaysOnSampler_Instance_IsNotNull()
        {
            Assert.IsNotNull(AlwaysOnSampler.Instance);
        }

        [TestMethod]
        public void AlwaysOnSampler_ShouldSample_ReturnsRecordAndSample()
        {
            var sampler = AlwaysOnSampler.Instance;
            var parameters = CreateSamplingParameters("test.span");

            var result = sampler.ShouldSample(parameters);

            Assert.AreEqual(SamplingDecision.RecordAndSample, result.Decision);
        }

        [TestMethod]
        public void AlwaysOnSampler_Description_IsCorrect()
        {
            Assert.AreEqual("AlwaysOnSampler", AlwaysOnSampler.Instance.Description);
        }

        #endregion

        #region AlwaysOffSampler Tests

        [TestMethod]
        public void AlwaysOffSampler_Instance_IsNotNull()
        {
            Assert.IsNotNull(AlwaysOffSampler.Instance);
        }

        [TestMethod]
        public void AlwaysOffSampler_ShouldSample_ReturnsDrop()
        {
            var sampler = AlwaysOffSampler.Instance;
            var parameters = CreateSamplingParameters("test.span");

            var result = sampler.ShouldSample(parameters);

            Assert.AreEqual(SamplingDecision.Drop, result.Decision);
        }

        [TestMethod]
        public void AlwaysOffSampler_Description_IsCorrect()
        {
            Assert.AreEqual("AlwaysOffSampler", AlwaysOffSampler.Instance.Description);
        }

        #endregion

        #region TraceIdRatioBasedSampler Tests

        [TestMethod]
        public void TraceIdRatioBasedSampler_Probability1_AlwaysSamples()
        {
            var sampler = new TraceIdRatioBasedSampler(1.0);

            for (int i = 0; i < 100; i++)
            {
                var parameters = CreateSamplingParameters($"test.span.{i}");
                var result = sampler.ShouldSample(parameters);
                Assert.AreEqual(SamplingDecision.RecordAndSample, result.Decision);
            }
        }

        [TestMethod]
        public void TraceIdRatioBasedSampler_Probability0_NeverSamples()
        {
            var sampler = new TraceIdRatioBasedSampler(0.0);

            for (int i = 0; i < 100; i++)
            {
                var parameters = CreateSamplingParameters($"test.span.{i}");
                var result = sampler.ShouldSample(parameters);
                Assert.AreEqual(SamplingDecision.Drop, result.Decision);
            }
        }

        [TestMethod]
        public void TraceIdRatioBasedSampler_InvalidProbability_Throws()
        {
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new TraceIdRatioBasedSampler(-0.1));
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new TraceIdRatioBasedSampler(1.1));
        }

        [TestMethod]
        public void TraceIdRatioBasedSampler_Probability_IsStored()
        {
            var sampler = new TraceIdRatioBasedSampler(0.5);
            Assert.AreEqual(0.5, sampler.Probability);
        }

        [TestMethod]
        public void TraceIdRatioBasedSampler_Description_ContainsProbability()
        {
            var sampler = new TraceIdRatioBasedSampler(0.25);
            Assert.IsTrue(sampler.Description.Contains("0.25"));
        }

        [TestMethod]
        public void TraceIdRatioBasedSampler_IsDeterministic_ForSameTraceId()
        {
            var sampler = new TraceIdRatioBasedSampler(0.5);
            var traceId = ActivityTraceId.CreateRandom();
            var parameters1 = new SamplingParameters(default, traceId, "test", ActivityKind.Internal);
            var parameters2 = new SamplingParameters(default, traceId, "test", ActivityKind.Internal);

            var result1 = sampler.ShouldSample(parameters1);
            var result2 = sampler.ShouldSample(parameters2);

            Assert.AreEqual(result1.Decision, result2.Decision);
        }

        #endregion

        #region ParentBasedSampler Tests

        [TestMethod]
        public void ParentBasedSampler_NoParent_UsesRootSampler()
        {
            var rootSampler = AlwaysOnSampler.Instance;
            var sampler = new ParentBasedSampler(rootSampler);
            var parameters = CreateSamplingParameters("test.span");

            var result = sampler.ShouldSample(parameters);

            Assert.AreEqual(SamplingDecision.RecordAndSample, result.Decision);
        }

        [TestMethod]
        public void ParentBasedSampler_NullRootSampler_Throws()
        {
            Assert.ThrowsExactly<ArgumentNullException>(() => new ParentBasedSampler(null!));
        }

        [TestMethod]
        public void ParentBasedSampler_LocalSampledParent_Samples()
        {
            var sampler = new ParentBasedSampler(AlwaysOffSampler.Instance);
            var parentContext = new ActivityContext(
                ActivityTraceId.CreateRandom(),
                ActivitySpanId.CreateRandom(),
                ActivityTraceFlags.Recorded,
                isRemote: false);
            var parameters = new SamplingParameters(parentContext, parentContext.TraceId, "test", ActivityKind.Internal);

            var result = sampler.ShouldSample(parameters);

            Assert.AreEqual(SamplingDecision.RecordAndSample, result.Decision);
        }

        [TestMethod]
        public void ParentBasedSampler_LocalNotSampledParent_Drops()
        {
            var sampler = new ParentBasedSampler(AlwaysOnSampler.Instance);
            var parentContext = new ActivityContext(
                ActivityTraceId.CreateRandom(),
                ActivitySpanId.CreateRandom(),
                ActivityTraceFlags.None,
                isRemote: false);
            var parameters = new SamplingParameters(parentContext, parentContext.TraceId, "test", ActivityKind.Internal);

            var result = sampler.ShouldSample(parameters);

            Assert.AreEqual(SamplingDecision.Drop, result.Decision);
        }

        [TestMethod]
        public void ParentBasedSampler_Description_ContainsRootSampler()
        {
            var sampler = new ParentBasedSampler(AlwaysOnSampler.Instance);
            Assert.IsTrue(sampler.Description.Contains("AlwaysOnSampler"));
        }

        #endregion

        #region RuleBasedSampler Tests

        [TestMethod]
        public void RuleBasedSampler_NoMatchingRule_UsesDefault()
        {
            var sampler = new RuleBasedSampler(AlwaysOnSampler.Instance);
            var parameters = CreateSamplingParameters("test.span");

            var result = sampler.ShouldSample(parameters);

            Assert.AreEqual(SamplingDecision.RecordAndSample, result.Decision);
        }

        [TestMethod]
        public void RuleBasedSampler_MatchingRule_UsesRuleSampler()
        {
            var sampler = new RuleBasedSampler(AlwaysOnSampler.Instance)
                .AddRule(name => name.StartsWith("drop."), AlwaysOffSampler.Instance);

            var dropParams = CreateSamplingParameters("drop.this");
            var keepParams = CreateSamplingParameters("keep.this");

            Assert.AreEqual(SamplingDecision.Drop, sampler.ShouldSample(dropParams).Decision);
            Assert.AreEqual(SamplingDecision.RecordAndSample, sampler.ShouldSample(keepParams).Decision);
        }

        [TestMethod]
        public void RuleBasedSampler_AlwaysSamplePrefix_Works()
        {
            var sampler = new RuleBasedSampler(AlwaysOffSampler.Instance)
                .AlwaysSamplePrefix("important.");

            var importantParams = CreateSamplingParameters("important.operation");
            var normalParams = CreateSamplingParameters("normal.operation");

            Assert.AreEqual(SamplingDecision.RecordAndSample, sampler.ShouldSample(importantParams).Decision);
            Assert.AreEqual(SamplingDecision.Drop, sampler.ShouldSample(normalParams).Decision);
        }

        [TestMethod]
        public void RuleBasedSampler_NeverSamplePrefix_Works()
        {
            var sampler = new RuleBasedSampler(AlwaysOnSampler.Instance)
                .NeverSamplePrefix("noise.");

            var noiseParams = CreateSamplingParameters("noise.ping");
            var signalParams = CreateSamplingParameters("signal.data");

            Assert.AreEqual(SamplingDecision.Drop, sampler.ShouldSample(noiseParams).Decision);
            Assert.AreEqual(SamplingDecision.RecordAndSample, sampler.ShouldSample(signalParams).Decision);
        }

        [TestMethod]
        public void RuleBasedSampler_SamplePrefixAtRate_Works()
        {
            var sampler = new RuleBasedSampler(AlwaysOffSampler.Instance)
                .SamplePrefixAtRate("sample.", 1.0);

            var sampleParams = CreateSamplingParameters("sample.this");

            Assert.AreEqual(SamplingDecision.RecordAndSample, sampler.ShouldSample(sampleParams).Decision);
        }

        [TestMethod]
        public void RuleBasedSampler_FirstMatchingRuleWins()
        {
            var sampler = new RuleBasedSampler(AlwaysOffSampler.Instance)
                .AddRule(name => name.StartsWith("test."), AlwaysOnSampler.Instance)
                .AddRule(name => name.StartsWith("test.drop."), AlwaysOffSampler.Instance);

            // First rule matches, so it should sample even though second rule would drop
            var parameters = CreateSamplingParameters("test.drop.this");

            Assert.AreEqual(SamplingDecision.RecordAndSample, sampler.ShouldSample(parameters).Decision);
        }

        [TestMethod]
        public void RuleBasedSampler_Description_ContainsRuleCount()
        {
            var sampler = new RuleBasedSampler(AlwaysOnSampler.Instance)
                .AlwaysSamplePrefix("a.")
                .NeverSamplePrefix("b.");

            Assert.IsTrue(sampler.Description.Contains("2 rules"));
        }

        #endregion

        #region SamplingResult Tests

        [TestMethod]
        public void SamplingResult_Drop_HasCorrectDecision()
        {
            var result = SamplingResult.Drop;
            Assert.AreEqual(SamplingDecision.Drop, result.Decision);
        }

        [TestMethod]
        public void SamplingResult_RecordOnly_HasCorrectDecision()
        {
            var result = SamplingResult.RecordOnly;
            Assert.AreEqual(SamplingDecision.RecordOnly, result.Decision);
        }

        [TestMethod]
        public void SamplingResult_RecordAndSample_HasCorrectDecision()
        {
            var result = SamplingResult.RecordAndSample;
            Assert.AreEqual(SamplingDecision.RecordAndSample, result.Decision);
        }

        #endregion

        #region Helper Methods

        private static SamplingParameters CreateSamplingParameters(string name)
        {
            return new SamplingParameters(
                default,
                ActivityTraceId.CreateRandom(),
                name,
                ActivityKind.Internal);
        }

        #endregion
    }
}
