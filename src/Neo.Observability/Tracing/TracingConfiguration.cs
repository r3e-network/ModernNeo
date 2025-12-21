// Copyright (C) 2015-2025 The Neo Project.
//
// TracingConfiguration.cs file belongs to the neo project and is free
// software distributed under the MIT software license.

using System;
using System.Collections.Generic;

namespace Neo.Observability.Tracing;

/// <summary>
/// Configuration for distributed tracing.
/// </summary>
public sealed class TracingConfiguration
{
    /// <summary>
    /// Gets or sets whether tracing is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the service name for traces.
    /// </summary>
    public string ServiceName { get; set; } = "neo-node";

    /// <summary>
    /// Gets or sets the service version.
    /// </summary>
    public string? ServiceVersion { get; set; }

    /// <summary>
    /// Gets or sets the service instance ID.
    /// </summary>
    public string? ServiceInstanceId { get; set; }

    /// <summary>
    /// Gets or sets the deployment environment (e.g., "production", "staging", "development").
    /// </summary>
    public string? Environment { get; set; }

    /// <summary>
    /// Gets or sets the sampling configuration.
    /// </summary>
    public SamplingConfiguration Sampling { get; set; } = new();

    /// <summary>
    /// Gets or sets the exporter configuration.
    /// </summary>
    public ExporterConfiguration Exporter { get; set; } = new();

    /// <summary>
    /// Gets or sets additional resource attributes.
    /// </summary>
    public Dictionary<string, string> ResourceAttributes { get; set; } = new();

    /// <summary>
    /// Gets or sets the activity sources to listen to.
    /// </summary>
    public List<string> ActivitySources { get; set; } = new()
    {
        "Neo.RPC",
        "Neo.Grpc",
        "Neo.Network.P2P",
        "Neo.Consensus",
        "Neo.BlockExecution"
    };

    /// <summary>
    /// Creates a default configuration for development.
    /// </summary>
    public static TracingConfiguration Development() => new()
    {
        Enabled = true,
        ServiceName = "neo-node-dev",
        Environment = "development",
        Sampling = new SamplingConfiguration
        {
            Strategy = SamplingStrategy.AlwaysOn
        },
        Exporter = new ExporterConfiguration
        {
            Type = ExporterType.Console
        }
    };

    /// <summary>
    /// Creates a default configuration for production.
    /// </summary>
    public static TracingConfiguration Production() => new()
    {
        Enabled = true,
        ServiceName = "neo-node",
        Environment = "production",
        Sampling = new SamplingConfiguration
        {
            Strategy = SamplingStrategy.ParentBased,
            RootSamplingRatio = 0.1 // 10% sampling for root spans
        },
        Exporter = new ExporterConfiguration
        {
            Type = ExporterType.Otlp,
            Endpoint = "http://localhost:4317"
        }
    };
}

/// <summary>
/// Sampling strategy types.
/// </summary>
public enum SamplingStrategy
{
    /// <summary>
    /// Always sample all traces.
    /// </summary>
    AlwaysOn,

    /// <summary>
    /// Never sample any traces.
    /// </summary>
    AlwaysOff,

    /// <summary>
    /// Sample based on trace ID ratio.
    /// </summary>
    TraceIdRatio,

    /// <summary>
    /// Sample based on parent span decision.
    /// </summary>
    ParentBased,

    /// <summary>
    /// Use custom rule-based sampling.
    /// </summary>
    RuleBased
}

/// <summary>
/// Configuration for trace sampling.
/// </summary>
public sealed class SamplingConfiguration
{
    /// <summary>
    /// Gets or sets the sampling strategy.
    /// </summary>
    public SamplingStrategy Strategy { get; set; } = SamplingStrategy.ParentBased;

    /// <summary>
    /// Gets or sets the sampling ratio for TraceIdRatio strategy (0.0 to 1.0).
    /// </summary>
    public double SamplingRatio { get; set; } = 1.0;

    /// <summary>
    /// Gets or sets the root span sampling ratio for ParentBased strategy.
    /// </summary>
    public double RootSamplingRatio { get; set; } = 1.0;

    /// <summary>
    /// Gets or sets span name prefixes to always sample.
    /// </summary>
    public List<string> AlwaysSamplePrefixes { get; set; } = new()
    {
        "consensus.",  // Always sample consensus operations
        "block.execute" // Always sample block execution
    };

    /// <summary>
    /// Gets or sets span name prefixes to never sample.
    /// </summary>
    public List<string> NeverSamplePrefixes { get; set; } = new()
    {
        "p2p.send.Ping",  // Don't sample ping/pong
        "p2p.send.Pong",
        "p2p.receive.Ping",
        "p2p.receive.Pong"
    };

    /// <summary>
    /// Gets or sets custom sampling rules (prefix -> ratio).
    /// </summary>
    public Dictionary<string, double> CustomRules { get; set; } = new();
}

/// <summary>
/// Exporter types.
/// </summary>
public enum ExporterType
{
    /// <summary>
    /// No exporter (traces are dropped).
    /// </summary>
    None,

    /// <summary>
    /// Console exporter for debugging.
    /// </summary>
    Console,

    /// <summary>
    /// OTLP (OpenTelemetry Protocol) exporter.
    /// </summary>
    Otlp,

    /// <summary>
    /// Zipkin exporter.
    /// </summary>
    Zipkin,

    /// <summary>
    /// Jaeger exporter.
    /// </summary>
    Jaeger
}

/// <summary>
/// OTLP protocol types.
/// </summary>
public enum OtlpProtocol
{
    /// <summary>
    /// gRPC protocol.
    /// </summary>
    Grpc,

    /// <summary>
    /// HTTP/protobuf protocol.
    /// </summary>
    HttpProtobuf
}

/// <summary>
/// Configuration for trace exporters.
/// </summary>
public sealed class ExporterConfiguration
{
    /// <summary>
    /// Gets or sets the exporter type.
    /// </summary>
    public ExporterType Type { get; set; } = ExporterType.Console;

    /// <summary>
    /// Gets or sets the exporter endpoint URL.
    /// </summary>
    public string? Endpoint { get; set; }

    /// <summary>
    /// Gets or sets the OTLP protocol (for OTLP exporter).
    /// </summary>
    public OtlpProtocol OtlpProtocol { get; set; } = OtlpProtocol.Grpc;

    /// <summary>
    /// Gets or sets additional headers for the exporter.
    /// </summary>
    public Dictionary<string, string> Headers { get; set; } = new();

    /// <summary>
    /// Gets or sets the export timeout in milliseconds.
    /// </summary>
    public int TimeoutMs { get; set; } = 30000;

    /// <summary>
    /// Gets or sets the batch export delay in milliseconds.
    /// </summary>
    public int BatchDelayMs { get; set; } = 5000;

    /// <summary>
    /// Gets or sets the maximum batch size.
    /// </summary>
    public int MaxBatchSize { get; set; } = 512;

    /// <summary>
    /// Gets or sets the maximum queue size.
    /// </summary>
    public int MaxQueueSize { get; set; } = 2048;
}

/// <summary>
/// Factory for creating samplers from configuration.
/// </summary>
public static class SamplerFactory
{
    /// <summary>
    /// Creates a sampler from configuration.
    /// </summary>
    /// <param name="config">The sampling configuration.</param>
    /// <returns>The configured sampler.</returns>
    public static ITraceSampler Create(SamplingConfiguration config)
    {
        var baseSampler = config.Strategy switch
        {
            SamplingStrategy.AlwaysOn => (ITraceSampler)AlwaysOnSampler.Instance,
            SamplingStrategy.AlwaysOff => AlwaysOffSampler.Instance,
            SamplingStrategy.TraceIdRatio => new TraceIdRatioBasedSampler(config.SamplingRatio),
            SamplingStrategy.ParentBased => new ParentBasedSampler(
                new TraceIdRatioBasedSampler(config.RootSamplingRatio)),
            SamplingStrategy.RuleBased => CreateRuleBasedSampler(config),
            _ => AlwaysOnSampler.Instance
        };

        // Wrap with rule-based sampler if there are always/never sample prefixes
        if (config.AlwaysSamplePrefixes.Count > 0 || config.NeverSamplePrefixes.Count > 0)
        {
            var ruleSampler = new RuleBasedSampler(baseSampler);

            foreach (var prefix in config.AlwaysSamplePrefixes)
            {
                ruleSampler.AlwaysSamplePrefix(prefix);
            }

            foreach (var prefix in config.NeverSamplePrefixes)
            {
                ruleSampler.NeverSamplePrefix(prefix);
            }

            foreach (var (prefix, ratio) in config.CustomRules)
            {
                ruleSampler.SamplePrefixAtRate(prefix, ratio);
            }

            return ruleSampler;
        }

        return baseSampler;
    }

    private static ITraceSampler CreateRuleBasedSampler(SamplingConfiguration config)
    {
        var defaultSampler = new TraceIdRatioBasedSampler(config.SamplingRatio);
        var ruleSampler = new RuleBasedSampler(defaultSampler);

        foreach (var (prefix, ratio) in config.CustomRules)
        {
            ruleSampler.SamplePrefixAtRate(prefix, ratio);
        }

        return ruleSampler;
    }
}
