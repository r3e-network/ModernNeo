// Copyright (C) 2015-2025 The Neo Project.
//
// DiagnosticsMetricsProvider.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using System;
using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using System.Threading;

namespace Neo.Observability.Metrics
{
    /// <summary>
    /// A metrics provider implementation using System.Diagnostics.Metrics.
    /// Compatible with OpenTelemetry and Prometheus exporters.
    /// </summary>
    public sealed class DiagnosticsMetricsProvider : IMetricsProvider, IDisposable
    {
        private readonly Meter _meter;
        private readonly ConcurrentDictionary<string, DiagnosticsCounter> _counters = new();
        private readonly ConcurrentDictionary<string, DiagnosticsGauge> _gauges = new();
        private readonly ConcurrentDictionary<string, DiagnosticsHistogram> _histograms = new();
        private bool _disposed;

        /// <summary>
        /// Gets the meter name used by this provider.
        /// </summary>
        public string MeterName => _meter.Name;

        /// <summary>
        /// Initializes a new instance of the <see cref="DiagnosticsMetricsProvider"/> class.
        /// </summary>
        /// <param name="meterName">The name of the meter. Defaults to "Neo".</param>
        /// <param name="version">The version of the meter. Defaults to "1.0.0".</param>
        public DiagnosticsMetricsProvider(string meterName = "Neo", string? version = "1.0.0")
        {
            _meter = new Meter(meterName, version);
        }

        /// <inheritdoc/>
        public ICounter CreateCounter(string name, string description)
        {
            return _counters.GetOrAdd(name, n =>
            {
                var counter = _meter.CreateCounter<long>(n, description: description);
                return new DiagnosticsCounter(n, counter);
            });
        }

        /// <inheritdoc/>
        public IGauge CreateGauge(string name, string description)
        {
            return _gauges.GetOrAdd(name, n =>
            {
                var gauge = new DiagnosticsGauge(n, _meter, description);
                return gauge;
            });
        }

        /// <inheritdoc/>
        public IHistogram CreateHistogram(string name, string description, double[]? buckets = null)
        {
            return _histograms.GetOrAdd(name, n =>
            {
                var histogram = _meter.CreateHistogram<double>(n, description: description);
                return new DiagnosticsHistogram(n, histogram);
            });
        }

        /// <summary>
        /// Disposes the meter and all associated instruments.
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _meter.Dispose();
        }
    }

    /// <summary>
    /// Counter implementation using System.Diagnostics.Metrics.
    /// </summary>
    internal sealed class DiagnosticsCounter : ICounter
    {
        private readonly Counter<long> _counter;
        private long _value;

        public string Name { get; }
        public long Value => Interlocked.Read(ref _value);

        public DiagnosticsCounter(string name, Counter<long> counter)
        {
            Name = name;
            _counter = counter;
        }

        public void Increment()
        {
            Interlocked.Increment(ref _value);
            _counter.Add(1);
        }

        public void Increment(long value)
        {
            if (value < 0) throw new ArgumentOutOfRangeException(nameof(value), "Value must be non-negative");
            Interlocked.Add(ref _value, value);
            _counter.Add(value);
        }
    }

    /// <summary>
    /// Gauge implementation using System.Diagnostics.Metrics.
    /// </summary>
    internal sealed class DiagnosticsGauge : IGauge
    {
        private double _value;

        public string Name { get; }
        public double Value => Volatile.Read(ref _value);

        public DiagnosticsGauge(string name, Meter meter, string description)
        {
            Name = name;
            // ObservableGauge reports the current value when observed
            meter.CreateObservableGauge(name, () => Volatile.Read(ref _value), description: description);
        }

        public void Set(double value)
        {
            Volatile.Write(ref _value, value);
        }

        public void Increment()
        {
            Interlocked.Exchange(ref _value, _value + 1);
        }

        public void Decrement()
        {
            Interlocked.Exchange(ref _value, _value - 1);
        }
    }

    /// <summary>
    /// Histogram implementation using System.Diagnostics.Metrics.
    /// </summary>
    internal sealed class DiagnosticsHistogram : IHistogram
    {
        private readonly Histogram<double> _histogram;
        private long _count;
        private double _sum;

        public string Name { get; }
        public long Count => Interlocked.Read(ref _count);
        public double Sum => Volatile.Read(ref _sum);

        public DiagnosticsHistogram(string name, Histogram<double> histogram)
        {
            Name = name;
            _histogram = histogram;
        }

        public void Observe(double value)
        {
            Interlocked.Increment(ref _count);
            // Thread-safe sum update using compare-exchange pattern
            double currentSum, newSum;
            do
            {
                currentSum = Volatile.Read(ref _sum);
                newSum = currentSum + value;
            } while (Interlocked.CompareExchange(ref _sum, newSum, currentSum) != currentSum);

            _histogram.Record(value);
        }
    }
}
