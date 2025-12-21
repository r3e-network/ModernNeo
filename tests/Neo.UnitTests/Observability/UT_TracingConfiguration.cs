// Copyright (C) 2015-2025 The Neo Project.
//
// UT_TracingConfiguration.cs file belongs to the neo project and is free
// software distributed under the MIT software license.

#nullable enable

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.Observability.Tracing;
using System.Linq;

namespace Neo.UnitTests.Observability
{
    [TestClass]
    public class UT_TracingConfiguration
    {
        #region TracingConfiguration Tests

        [TestMethod]
        public void TracingConfiguration_DefaultValues_AreCorrect()
        {
            var config = new TracingConfiguration();

            Assert.IsTrue(config.Enabled);
            Assert.AreEqual("neo-node", config.ServiceName);
            Assert.IsNull(config.ServiceVersion);
            Assert.IsNull(config.ServiceInstanceId);
            Assert.IsNull(config.Environment);
            Assert.IsNotNull(config.Sampling);
            Assert.IsNotNull(config.Exporter);
            Assert.IsNotNull(config.ResourceAttributes);
            Assert.IsNotNull(config.ActivitySources);
        }

        [TestMethod]
        public void TracingConfiguration_ActivitySources_ContainsExpectedSources()
        {
            var config = new TracingConfiguration();

            Assert.IsTrue(config.ActivitySources.Contains("Neo.RPC"));
            Assert.IsTrue(config.ActivitySources.Contains("Neo.Grpc"));
            Assert.IsTrue(config.ActivitySources.Contains("Neo.Network.P2P"));
            Assert.IsTrue(config.ActivitySources.Contains("Neo.Consensus"));
            Assert.IsTrue(config.ActivitySources.Contains("Neo.BlockExecution"));
        }

        [TestMethod]
        public void TracingConfiguration_Development_HasCorrectSettings()
        {
            var config = TracingConfiguration.Development();

            Assert.IsTrue(config.Enabled);
            Assert.AreEqual("neo-node-dev", config.ServiceName);
            Assert.AreEqual("development", config.Environment);
            Assert.AreEqual(SamplingStrategy.AlwaysOn, config.Sampling.Strategy);
            Assert.AreEqual(ExporterType.Console, config.Exporter.Type);
        }

        [TestMethod]
        public void TracingConfiguration_Production_HasCorrectSettings()
        {
            var config = TracingConfiguration.Production();

            Assert.IsTrue(config.Enabled);
            Assert.AreEqual("neo-node", config.ServiceName);
            Assert.AreEqual("production", config.Environment);
            Assert.AreEqual(SamplingStrategy.ParentBased, config.Sampling.Strategy);
            Assert.AreEqual(0.1, config.Sampling.RootSamplingRatio);
            Assert.AreEqual(ExporterType.Otlp, config.Exporter.Type);
            Assert.AreEqual("http://localhost:4317", config.Exporter.Endpoint);
        }

        #endregion

        #region SamplingConfiguration Tests

        [TestMethod]
        public void SamplingConfiguration_DefaultValues_AreCorrect()
        {
            var config = new SamplingConfiguration();

            Assert.AreEqual(SamplingStrategy.ParentBased, config.Strategy);
            Assert.AreEqual(1.0, config.SamplingRatio);
            Assert.AreEqual(1.0, config.RootSamplingRatio);
            Assert.IsNotNull(config.AlwaysSamplePrefixes);
            Assert.IsNotNull(config.NeverSamplePrefixes);
            Assert.IsNotNull(config.CustomRules);
        }

        [TestMethod]
        public void SamplingConfiguration_AlwaysSamplePrefixes_ContainsExpectedPrefixes()
        {
            var config = new SamplingConfiguration();

            Assert.IsTrue(config.AlwaysSamplePrefixes.Contains("consensus."));
            Assert.IsTrue(config.AlwaysSamplePrefixes.Contains("block.execute"));
        }

        [TestMethod]
        public void SamplingConfiguration_NeverSamplePrefixes_ContainsExpectedPrefixes()
        {
            var config = new SamplingConfiguration();

            Assert.IsTrue(config.NeverSamplePrefixes.Contains("p2p.send.Ping"));
            Assert.IsTrue(config.NeverSamplePrefixes.Contains("p2p.send.Pong"));
            Assert.IsTrue(config.NeverSamplePrefixes.Contains("p2p.receive.Ping"));
            Assert.IsTrue(config.NeverSamplePrefixes.Contains("p2p.receive.Pong"));
        }

        #endregion

        #region ExporterConfiguration Tests

        [TestMethod]
        public void ExporterConfiguration_DefaultValues_AreCorrect()
        {
            var config = new ExporterConfiguration();

            Assert.AreEqual(ExporterType.Console, config.Type);
            Assert.IsNull(config.Endpoint);
            Assert.AreEqual(OtlpProtocol.Grpc, config.OtlpProtocol);
            Assert.IsNotNull(config.Headers);
            Assert.AreEqual(30000, config.TimeoutMs);
            Assert.AreEqual(5000, config.BatchDelayMs);
            Assert.AreEqual(512, config.MaxBatchSize);
            Assert.AreEqual(2048, config.MaxQueueSize);
        }

        #endregion

        #region SamplerFactory Tests

        [TestMethod]
        public void SamplerFactory_AlwaysOn_CreatesAlwaysOnSampler()
        {
            var config = new SamplingConfiguration
            {
                Strategy = SamplingStrategy.AlwaysOn,
                AlwaysSamplePrefixes = new(),
                NeverSamplePrefixes = new()
            };

            var sampler = SamplerFactory.Create(config);

            Assert.IsInstanceOfType<AlwaysOnSampler>(sampler);
        }

        [TestMethod]
        public void SamplerFactory_AlwaysOff_CreatesAlwaysOffSampler()
        {
            var config = new SamplingConfiguration
            {
                Strategy = SamplingStrategy.AlwaysOff,
                AlwaysSamplePrefixes = new(),
                NeverSamplePrefixes = new()
            };

            var sampler = SamplerFactory.Create(config);

            Assert.IsInstanceOfType<AlwaysOffSampler>(sampler);
        }

        [TestMethod]
        public void SamplerFactory_TraceIdRatio_CreatesRatioSampler()
        {
            var config = new SamplingConfiguration
            {
                Strategy = SamplingStrategy.TraceIdRatio,
                SamplingRatio = 0.5,
                AlwaysSamplePrefixes = new(),
                NeverSamplePrefixes = new()
            };

            var sampler = SamplerFactory.Create(config);

            Assert.IsInstanceOfType<TraceIdRatioBasedSampler>(sampler);
            Assert.AreEqual(0.5, ((TraceIdRatioBasedSampler)sampler).Probability);
        }

        [TestMethod]
        public void SamplerFactory_ParentBased_CreatesParentBasedSampler()
        {
            var config = new SamplingConfiguration
            {
                Strategy = SamplingStrategy.ParentBased,
                RootSamplingRatio = 0.25,
                AlwaysSamplePrefixes = new(),
                NeverSamplePrefixes = new()
            };

            var sampler = SamplerFactory.Create(config);

            Assert.IsInstanceOfType<ParentBasedSampler>(sampler);
        }

        [TestMethod]
        public void SamplerFactory_WithPrefixes_CreatesRuleBasedSampler()
        {
            var config = new SamplingConfiguration
            {
                Strategy = SamplingStrategy.AlwaysOn,
                AlwaysSamplePrefixes = new() { "important." },
                NeverSamplePrefixes = new() { "noise." }
            };

            var sampler = SamplerFactory.Create(config);

            Assert.IsInstanceOfType<RuleBasedSampler>(sampler);
        }

        [TestMethod]
        public void SamplerFactory_RuleBased_CreatesRuleBasedSampler()
        {
            var config = new SamplingConfiguration
            {
                Strategy = SamplingStrategy.RuleBased,
                SamplingRatio = 0.5,
                CustomRules = new() { { "custom.", 0.75 } },
                AlwaysSamplePrefixes = new(),
                NeverSamplePrefixes = new()
            };

            var sampler = SamplerFactory.Create(config);

            Assert.IsInstanceOfType<RuleBasedSampler>(sampler);
        }

        [TestMethod]
        public void SamplerFactory_DefaultConfig_CreatesWorkingSampler()
        {
            var config = new SamplingConfiguration();
            var sampler = SamplerFactory.Create(config);

            Assert.IsNotNull(sampler);
            // Should be RuleBasedSampler because default config has prefixes
            Assert.IsInstanceOfType<RuleBasedSampler>(sampler);
        }

        #endregion

        #region Enum Tests

        [TestMethod]
        public void SamplingStrategy_HasExpectedValues()
        {
            Assert.AreEqual(0, (int)SamplingStrategy.AlwaysOn);
            Assert.AreEqual(1, (int)SamplingStrategy.AlwaysOff);
            Assert.AreEqual(2, (int)SamplingStrategy.TraceIdRatio);
            Assert.AreEqual(3, (int)SamplingStrategy.ParentBased);
            Assert.AreEqual(4, (int)SamplingStrategy.RuleBased);
        }

        [TestMethod]
        public void ExporterType_HasExpectedValues()
        {
            Assert.AreEqual(0, (int)ExporterType.None);
            Assert.AreEqual(1, (int)ExporterType.Console);
            Assert.AreEqual(2, (int)ExporterType.Otlp);
            Assert.AreEqual(3, (int)ExporterType.Zipkin);
            Assert.AreEqual(4, (int)ExporterType.Jaeger);
        }

        [TestMethod]
        public void OtlpProtocol_HasExpectedValues()
        {
            Assert.AreEqual(0, (int)OtlpProtocol.Grpc);
            Assert.AreEqual(1, (int)OtlpProtocol.HttpProtobuf);
        }

        #endregion
    }
}
