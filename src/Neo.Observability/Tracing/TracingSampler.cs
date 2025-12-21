// Copyright (C) 2015-2025 The Neo Project.
//
// TracingSampler.cs file belongs to the neo project and is free
// software distributed under the MIT software license.

using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Neo.Observability.Tracing;

/// <summary>
/// Sampling decision for a trace.
/// </summary>
public enum SamplingDecision
{
    /// <summary>
    /// Drop the span - do not record or export.
    /// </summary>
    Drop = 0,

    /// <summary>
    /// Record the span but do not export.
    /// </summary>
    RecordOnly = 1,

    /// <summary>
    /// Record and export the span.
    /// </summary>
    RecordAndSample = 2
}

/// <summary>
/// Result of a sampling decision.
/// </summary>
public readonly struct SamplingResult
{
    /// <summary>
    /// The sampling decision.
    /// </summary>
    public SamplingDecision Decision { get; }

    /// <summary>
    /// Creates a new sampling result.
    /// </summary>
    public SamplingResult(SamplingDecision decision)
    {
        Decision = decision;
    }

    /// <summary>
    /// A result indicating the span should be dropped.
    /// </summary>
    public static SamplingResult Drop => new(SamplingDecision.Drop);

    /// <summary>
    /// A result indicating the span should be recorded only.
    /// </summary>
    public static SamplingResult RecordOnly => new(SamplingDecision.RecordOnly);

    /// <summary>
    /// A result indicating the span should be recorded and sampled.
    /// </summary>
    public static SamplingResult RecordAndSample => new(SamplingDecision.RecordAndSample);
}

/// <summary>
/// Parameters for making a sampling decision.
/// </summary>
public readonly struct SamplingParameters
{
    /// <summary>
    /// The parent context, if any.
    /// </summary>
    public ActivityContext ParentContext { get; }

    /// <summary>
    /// The trace ID.
    /// </summary>
    public ActivityTraceId TraceId { get; }

    /// <summary>
    /// The span name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// The span kind.
    /// </summary>
    public ActivityKind Kind { get; }

    /// <summary>
    /// Creates new sampling parameters.
    /// </summary>
    public SamplingParameters(
        ActivityContext parentContext,
        ActivityTraceId traceId,
        string name,
        ActivityKind kind)
    {
        ParentContext = parentContext;
        TraceId = traceId;
        Name = name;
        Kind = kind;
    }
}

/// <summary>
/// Interface for trace samplers.
/// </summary>
public interface ITraceSampler
{
    /// <summary>
    /// Makes a sampling decision for a span.
    /// </summary>
    /// <param name="parameters">The sampling parameters.</param>
    /// <returns>The sampling result.</returns>
    SamplingResult ShouldSample(in SamplingParameters parameters);

    /// <summary>
    /// Gets the sampler description.
    /// </summary>
    string Description { get; }
}

/// <summary>
/// Always samples all traces.
/// </summary>
public sealed class AlwaysOnSampler : ITraceSampler
{
    /// <summary>
    /// Singleton instance.
    /// </summary>
    public static AlwaysOnSampler Instance { get; } = new();

    private AlwaysOnSampler() { }

    /// <inheritdoc/>
    public SamplingResult ShouldSample(in SamplingParameters parameters)
        => SamplingResult.RecordAndSample;

    /// <inheritdoc/>
    public string Description => "AlwaysOnSampler";
}

/// <summary>
/// Never samples any traces.
/// </summary>
public sealed class AlwaysOffSampler : ITraceSampler
{
    /// <summary>
    /// Singleton instance.
    /// </summary>
    public static AlwaysOffSampler Instance { get; } = new();

    private AlwaysOffSampler() { }

    /// <inheritdoc/>
    public SamplingResult ShouldSample(in SamplingParameters parameters)
        => SamplingResult.Drop;

    /// <inheritdoc/>
    public string Description => "AlwaysOffSampler";
}

/// <summary>
/// Samples traces based on a probability ratio.
/// </summary>
public sealed class TraceIdRatioBasedSampler : ITraceSampler
{
    private readonly double _probability;
    private readonly long _idUpperBound;

    /// <summary>
    /// Creates a new ratio-based sampler.
    /// </summary>
    /// <param name="probability">The sampling probability (0.0 to 1.0).</param>
    public TraceIdRatioBasedSampler(double probability)
    {
        if (probability < 0.0 || probability > 1.0)
            throw new ArgumentOutOfRangeException(nameof(probability), "Probability must be between 0.0 and 1.0");

        _probability = probability;

        // Calculate the upper bound for trace ID comparison
        // TraceId is 16 bytes, we use the first 8 bytes as a long
        _idUpperBound = probability >= 1.0
            ? long.MaxValue
            : (long)(probability * long.MaxValue);
    }

    /// <summary>
    /// Gets the sampling probability.
    /// </summary>
    public double Probability => _probability;

    /// <inheritdoc/>
    public SamplingResult ShouldSample(in SamplingParameters parameters)
    {
        if (_probability >= 1.0)
            return SamplingResult.RecordAndSample;

        if (_probability <= 0.0)
            return SamplingResult.Drop;

        // Use trace ID for deterministic sampling
        var traceIdBytes = parameters.TraceId.ToHexString();
        if (traceIdBytes.Length >= 16)
        {
            // Parse first 8 bytes as long for comparison
            if (long.TryParse(traceIdBytes.AsSpan(0, 16), System.Globalization.NumberStyles.HexNumber, null, out var value))
            {
                // Make it positive by taking absolute value
                value = Math.Abs(value);
                return value < _idUpperBound
                    ? SamplingResult.RecordAndSample
                    : SamplingResult.Drop;
            }
        }

        // Fallback to random sampling if trace ID parsing fails
        return Random.Shared.NextDouble() < _probability
            ? SamplingResult.RecordAndSample
            : SamplingResult.Drop;
    }

    /// <inheritdoc/>
    public string Description => $"TraceIdRatioBasedSampler({_probability:F4})";
}

/// <summary>
/// Samples based on parent span's sampling decision.
/// </summary>
public sealed class ParentBasedSampler : ITraceSampler
{
    private readonly ITraceSampler _rootSampler;
    private readonly ITraceSampler? _remoteParentSampled;
    private readonly ITraceSampler? _remoteParentNotSampled;
    private readonly ITraceSampler? _localParentSampled;
    private readonly ITraceSampler? _localParentNotSampled;

    /// <summary>
    /// Creates a new parent-based sampler.
    /// </summary>
    /// <param name="rootSampler">Sampler to use for root spans (no parent).</param>
    /// <param name="remoteParentSampled">Sampler for remote sampled parent. Defaults to AlwaysOn.</param>
    /// <param name="remoteParentNotSampled">Sampler for remote not-sampled parent. Defaults to AlwaysOff.</param>
    /// <param name="localParentSampled">Sampler for local sampled parent. Defaults to AlwaysOn.</param>
    /// <param name="localParentNotSampled">Sampler for local not-sampled parent. Defaults to AlwaysOff.</param>
    public ParentBasedSampler(
        ITraceSampler rootSampler,
        ITraceSampler? remoteParentSampled = null,
        ITraceSampler? remoteParentNotSampled = null,
        ITraceSampler? localParentSampled = null,
        ITraceSampler? localParentNotSampled = null)
    {
        _rootSampler = rootSampler ?? throw new ArgumentNullException(nameof(rootSampler));
        _remoteParentSampled = remoteParentSampled ?? AlwaysOnSampler.Instance;
        _remoteParentNotSampled = remoteParentNotSampled ?? AlwaysOffSampler.Instance;
        _localParentSampled = localParentSampled ?? AlwaysOnSampler.Instance;
        _localParentNotSampled = localParentNotSampled ?? AlwaysOffSampler.Instance;
    }

    /// <inheritdoc/>
    public SamplingResult ShouldSample(in SamplingParameters parameters)
    {
        var parentContext = parameters.ParentContext;

        // No parent - use root sampler
        if (parentContext.TraceId == default)
        {
            return _rootSampler.ShouldSample(parameters);
        }

        // Has parent - check if sampled
        var parentSampled = (parentContext.TraceFlags & ActivityTraceFlags.Recorded) != 0;

        if (parentContext.IsRemote)
        {
            return parentSampled
                ? _remoteParentSampled!.ShouldSample(parameters)
                : _remoteParentNotSampled!.ShouldSample(parameters);
        }
        else
        {
            return parentSampled
                ? _localParentSampled!.ShouldSample(parameters)
                : _localParentNotSampled!.ShouldSample(parameters);
        }
    }

    /// <inheritdoc/>
    public string Description => $"ParentBasedSampler(root={_rootSampler.Description})";
}

/// <summary>
/// Samples based on span name patterns.
/// </summary>
public sealed class RuleBasedSampler : ITraceSampler
{
    private readonly List<(Func<string, bool> Predicate, ITraceSampler Sampler)> _rules;
    private readonly ITraceSampler _defaultSampler;

    /// <summary>
    /// Creates a new rule-based sampler.
    /// </summary>
    /// <param name="defaultSampler">Default sampler when no rules match.</param>
    public RuleBasedSampler(ITraceSampler defaultSampler)
    {
        _defaultSampler = defaultSampler ?? throw new ArgumentNullException(nameof(defaultSampler));
        _rules = new List<(Func<string, bool>, ITraceSampler)>();
    }

    /// <summary>
    /// Adds a sampling rule.
    /// </summary>
    /// <param name="predicate">Predicate to match span names.</param>
    /// <param name="sampler">Sampler to use when predicate matches.</param>
    /// <returns>This sampler for chaining.</returns>
    public RuleBasedSampler AddRule(Func<string, bool> predicate, ITraceSampler sampler)
    {
        _rules.Add((predicate, sampler));
        return this;
    }

    /// <summary>
    /// Adds a rule to always sample spans matching a prefix.
    /// </summary>
    /// <param name="prefix">The span name prefix.</param>
    /// <returns>This sampler for chaining.</returns>
    public RuleBasedSampler AlwaysSamplePrefix(string prefix)
        => AddRule(name => name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase), AlwaysOnSampler.Instance);

    /// <summary>
    /// Adds a rule to never sample spans matching a prefix.
    /// </summary>
    /// <param name="prefix">The span name prefix.</param>
    /// <returns>This sampler for chaining.</returns>
    public RuleBasedSampler NeverSamplePrefix(string prefix)
        => AddRule(name => name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase), AlwaysOffSampler.Instance);

    /// <summary>
    /// Adds a rule to sample spans matching a prefix at a given rate.
    /// </summary>
    /// <param name="prefix">The span name prefix.</param>
    /// <param name="probability">The sampling probability.</param>
    /// <returns>This sampler for chaining.</returns>
    public RuleBasedSampler SamplePrefixAtRate(string prefix, double probability)
        => AddRule(name => name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase), new TraceIdRatioBasedSampler(probability));

    /// <inheritdoc/>
    public SamplingResult ShouldSample(in SamplingParameters parameters)
    {
        foreach (var (predicate, sampler) in _rules)
        {
            if (predicate(parameters.Name))
            {
                return sampler.ShouldSample(parameters);
            }
        }

        return _defaultSampler.ShouldSample(parameters);
    }

    /// <inheritdoc/>
    public string Description => $"RuleBasedSampler({_rules.Count} rules, default={_defaultSampler.Description})";
}
