// Copyright (C) 2015-2025 The Neo Project.
//
// PerformanceBaseline.cs file belongs to the neo project and is free
// software distributed under the MIT software license.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Neo.PerformanceTests;

/// <summary>
/// Represents a performance baseline for regression testing.
/// </summary>
public class PerformanceBaseline
{
    /// <summary>
    /// Version of the baseline format.
    /// </summary>
    public int Version { get; set; } = 1;

    /// <summary>
    /// Timestamp when the baseline was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Git commit hash when the baseline was created.
    /// </summary>
    public string? CommitHash { get; set; }

    /// <summary>
    /// Machine information where the baseline was created.
    /// </summary>
    public MachineInfo? Machine { get; set; }

    /// <summary>
    /// Collection of benchmark results.
    /// </summary>
    public Dictionary<string, BenchmarkResult> Benchmarks { get; set; } = new();

    /// <summary>
    /// Loads a baseline from a JSON file.
    /// </summary>
    public static PerformanceBaseline? Load(string filePath)
    {
        if (!File.Exists(filePath))
            return null;

        var json = File.ReadAllText(filePath);
        return JsonSerializer.Deserialize<PerformanceBaseline>(json, GetJsonOptions());
    }

    /// <summary>
    /// Saves the baseline to a JSON file.
    /// </summary>
    public void Save(string filePath)
    {
        var json = JsonSerializer.Serialize(this, GetJsonOptions());
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);
        File.WriteAllText(filePath, json);
    }

    private static JsonSerializerOptions GetJsonOptions() => new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
}

/// <summary>
/// Machine information for baseline context.
/// </summary>
public class MachineInfo
{
    public string? ProcessorName { get; set; }
    public int ProcessorCount { get; set; }
    public string? OsDescription { get; set; }
    public string? RuntimeVersion { get; set; }

    public static MachineInfo Current => new()
    {
        ProcessorCount = Environment.ProcessorCount,
        OsDescription = System.Runtime.InteropServices.RuntimeInformation.OSDescription,
        RuntimeVersion = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription
    };
}

/// <summary>
/// Result of a single benchmark.
/// </summary>
public class BenchmarkResult
{
    /// <summary>
    /// Mean execution time in nanoseconds.
    /// </summary>
    public double MeanNs { get; set; }

    /// <summary>
    /// Standard deviation in nanoseconds.
    /// </summary>
    public double StdDevNs { get; set; }

    /// <summary>
    /// Memory allocated per operation in bytes.
    /// </summary>
    public long AllocatedBytes { get; set; }

    /// <summary>
    /// Number of iterations used to compute the result.
    /// </summary>
    public int Iterations { get; set; }

    /// <summary>
    /// Timestamp when this benchmark was run.
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Result of comparing current performance against baseline.
/// </summary>
public class RegressionResult
{
    public string BenchmarkName { get; set; } = string.Empty;
    public double BaselineMeanNs { get; set; }
    public double CurrentMeanNs { get; set; }
    public double PercentageChange { get; set; }
    public bool IsRegression { get; set; }
    public string? Message { get; set; }
}

/// <summary>
/// Compares performance results against baselines.
/// </summary>
public class PerformanceComparer
{
    /// <summary>
    /// Threshold for detecting regression (default: 10%).
    /// </summary>
    public double RegressionThreshold { get; set; } = 0.10;

    /// <summary>
    /// Compares current results against baseline.
    /// </summary>
    public IReadOnlyList<RegressionResult> Compare(
        PerformanceBaseline baseline,
        Dictionary<string, BenchmarkResult> currentResults)
    {
        var results = new List<RegressionResult>();

        foreach (var (name, current) in currentResults)
        {
            if (!baseline.Benchmarks.TryGetValue(name, out var baselineResult))
            {
                results.Add(new RegressionResult
                {
                    BenchmarkName = name,
                    CurrentMeanNs = current.MeanNs,
                    Message = "New benchmark (no baseline)"
                });
                continue;
            }

            var percentageChange = (current.MeanNs - baselineResult.MeanNs) / baselineResult.MeanNs;
            var isRegression = percentageChange > RegressionThreshold;

            results.Add(new RegressionResult
            {
                BenchmarkName = name,
                BaselineMeanNs = baselineResult.MeanNs,
                CurrentMeanNs = current.MeanNs,
                PercentageChange = percentageChange,
                IsRegression = isRegression,
                Message = isRegression
                    ? $"REGRESSION: {percentageChange:P1} slower than baseline"
                    : $"OK: {percentageChange:P1} change from baseline"
            });
        }

        // Check for missing benchmarks
        foreach (var name in baseline.Benchmarks.Keys)
        {
            if (!currentResults.ContainsKey(name))
            {
                results.Add(new RegressionResult
                {
                    BenchmarkName = name,
                    BaselineMeanNs = baseline.Benchmarks[name].MeanNs,
                    Message = "Benchmark removed from current run"
                });
            }
        }

        return results;
    }

    /// <summary>
    /// Checks if any regressions were detected.
    /// </summary>
    public bool HasRegressions(IReadOnlyList<RegressionResult> results)
    {
        return results.Any(r => r.IsRegression);
    }
}
