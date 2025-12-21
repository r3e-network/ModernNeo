// Copyright (C) 2015-2025 The Neo Project.
//
// OpenTelemetryMetricsProvider.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Threading;

namespace Neo.Observability.Metrics
{
    /// <summary>
    /// OpenTelemetry-optimized metrics provider with enhanced configuration options.
    /// Uses System.Diagnostics.Metrics which is natively supported by OpenTelemetry.
    /// </summary>
    public sealed class OpenTelemetryMetricsProvider : IMetricsProvider, IDisposable
    {
        private readonly Meter _meter;
        private readonly ConcurrentDictionary<string, OTelCounter> _counters = new();
        private readonly ConcurrentDictionary<string, OTelGauge> _gauges = new();
        private readonly ConcurrentDictionary<string, OTelHistogram> _histograms = new();
        private readonly string _serviceName;
        private readonly string? _serviceVersion;
        private bool _disposed;

        /// <summary>
        /// Gets the meter used by this provider.
        /// </summary>
        public Meter Meter => _meter;

        /// <summary>
        /// Gets the service name.
        /// </summary>
        public string ServiceName => _serviceName;

        /// <summary>
        /// Gets the service version.
        /// </summary>
        public string? ServiceVersion => _serviceVersion;

        /// <summary>
        /// Initializes a new instance of the OpenTelemetryMetricsProvider class.
        /// </summary>
        /// <param name="serviceName">The name of the service for metrics.</param>
        /// <param name="serviceVersion">The version of the service.</param>
        public OpenTelemetryMetricsProvider(string serviceName = "Neo.Blockchain", string? serviceVersion = null)
        {
            _serviceName = serviceName;
            _serviceVersion = serviceVersion;
            _meter = new Meter(serviceName, serviceVersion);
        }

        /// <inheritdoc/>
        public ICounter CreateCounter(string name, string description)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _counters.GetOrAdd(name, n =>
            {
                var counter = _meter.CreateCounter<long>(n, description: description);
                return new OTelCounter(n, counter);
            });
        }

        /// <summary>
        /// Creates a counter with tags support.
        /// </summary>
        /// <param name="name">The name of the counter.</param>
        /// <param name="description">A description of what the counter measures.</param>
        /// <param name="unit">The unit of measurement.</param>
        /// <returns>A counter instance.</returns>
        public ICounter CreateCounter(string name, string description, string? unit)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _counters.GetOrAdd(name, n =>
            {
                var counter = _meter.CreateCounter<long>(n, unit: unit, description: description);
                return new OTelCounter(n, counter);
            });
        }

        /// <inheritdoc/>
        public IGauge CreateGauge(string name, string description)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _gauges.GetOrAdd(name, n => new OTelGauge(n, _meter, description));
        }

        /// <summary>
        /// Creates a gauge with unit support.
        /// </summary>
        /// <param name="name">The name of the gauge.</param>
        /// <param name="description">A description of what the gauge measures.</param>
        /// <param name="unit">The unit of measurement.</param>
        /// <returns>A gauge instance.</returns>
        public IGauge CreateGauge(string name, string description, string? unit)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _gauges.GetOrAdd(name, n => new OTelGauge(n, _meter, description, unit));
        }

        /// <inheritdoc/>
        public IHistogram CreateHistogram(string name, string description, double[]? buckets = null)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _histograms.GetOrAdd(name, n =>
            {
                var histogram = _meter.CreateHistogram<double>(n, description: description);
                return new OTelHistogram(n, histogram, buckets);
            });
        }

        /// <summary>
        /// Creates a histogram with unit support.
        /// </summary>
        /// <param name="name">The name of the histogram.</param>
        /// <param name="description">A description of what the histogram measures.</param>
        /// <param name="unit">The unit of measurement.</param>
        /// <param name="buckets">Optional bucket boundaries for the histogram.</param>
        /// <returns>A histogram instance.</returns>
        public IHistogram CreateHistogram(string name, string description, string? unit, double[]? buckets = null)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _histograms.GetOrAdd(name, n =>
            {
                var histogram = _meter.CreateHistogram<double>(n, unit: unit, description: description);
                return new OTelHistogram(n, histogram, buckets);
            });
        }

        /// <summary>
        /// Creates an observable counter that reports values from a callback.
        /// </summary>
        /// <param name="name">The name of the counter.</param>
        /// <param name="observeValue">Callback to observe the current value.</param>
        /// <param name="description">A description of what the counter measures.</param>
        /// <param name="unit">The unit of measurement.</param>
        public void CreateObservableCounter(string name, Func<long> observeValue, string? description = null, string? unit = null)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _meter.CreateObservableCounter(name, observeValue, unit: unit, description: description);
        }

        /// <summary>
        /// Creates an observable gauge that reports values from a callback.
        /// </summary>
        /// <param name="name">The name of the gauge.</param>
        /// <param name="observeValue">Callback to observe the current value.</param>
        /// <param name="description">A description of what the gauge measures.</param>
        /// <param name="unit">The unit of measurement.</param>
        public void CreateObservableGauge(string name, Func<double> observeValue, string? description = null, string? unit = null)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _meter.CreateObservableGauge(name, observeValue, unit: unit, description: description);
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
    /// OpenTelemetry-compatible counter implementation with tags support.
    /// </summary>
    internal sealed class OTelCounter : ICounter
    {
        private readonly Counter<long> _counter;
        private long _value;

        public string Name { get; }
        public long Value => Interlocked.Read(ref _value);

        public OTelCounter(string name, Counter<long> counter)
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

        /// <summary>
        /// Increments the counter with tags.
        /// </summary>
        /// <param name="value">The value to increment by.</param>
        /// <param name="tags">Tags to associate with this measurement.</param>
        public void Increment(long value, params KeyValuePair<string, object?>[] tags)
        {
            if (value < 0) throw new ArgumentOutOfRangeException(nameof(value), "Value must be non-negative");
            Interlocked.Add(ref _value, value);
            _counter.Add(value, tags);
        }
    }

    /// <summary>
    /// OpenTelemetry-compatible gauge implementation.
    /// </summary>
    internal sealed class OTelGauge : IGauge
    {
        private double _value;

        public string Name { get; }
        public double Value => Volatile.Read(ref _value);

        public OTelGauge(string name, Meter meter, string description, string? unit = null)
        {
            Name = name;
            meter.CreateObservableGauge(name, () => Volatile.Read(ref _value), unit: unit, description: description);
        }

        public void Set(double value)
        {
            Volatile.Write(ref _value, value);
        }

        public void Increment()
        {
            double currentValue, newValue;
            do
            {
                currentValue = Volatile.Read(ref _value);
                newValue = currentValue + 1;
            } while (Interlocked.CompareExchange(ref _value, newValue, currentValue) != currentValue);
        }

        public void Decrement()
        {
            double currentValue, newValue;
            do
            {
                currentValue = Volatile.Read(ref _value);
                newValue = currentValue - 1;
            } while (Interlocked.CompareExchange(ref _value, newValue, currentValue) != currentValue);
        }
    }

    /// <summary>
    /// OpenTelemetry-compatible histogram implementation with bucket support.
    /// </summary>
    internal sealed class OTelHistogram : IHistogram
    {
        private readonly Histogram<double> _histogram;
        private readonly double[]? _buckets;
        private long _count;
        private double _sum;
        private double _min = double.MaxValue;
        private double _max = double.MinValue;

        public string Name { get; }
        public long Count => Interlocked.Read(ref _count);
        public double Sum => Volatile.Read(ref _sum);

        /// <summary>
        /// Gets the minimum observed value.
        /// </summary>
        public double Min => Volatile.Read(ref _min);

        /// <summary>
        /// Gets the maximum observed value.
        /// </summary>
        public double Max => Volatile.Read(ref _max);

        /// <summary>
        /// Gets the configured bucket boundaries.
        /// </summary>
        public double[]? Buckets => _buckets;

        public OTelHistogram(string name, Histogram<double> histogram, double[]? buckets = null)
        {
            Name = name;
            _histogram = histogram;
            _buckets = buckets;
        }

        public void Observe(double value)
        {
            Interlocked.Increment(ref _count);

            // Thread-safe sum update
            double currentSum, newSum;
            do
            {
                currentSum = Volatile.Read(ref _sum);
                newSum = currentSum + value;
            } while (Interlocked.CompareExchange(ref _sum, newSum, currentSum) != currentSum);

            // Thread-safe min update
            double currentMin;
            do
            {
                currentMin = Volatile.Read(ref _min);
                if (value >= currentMin) break;
            } while (Interlocked.CompareExchange(ref _min, value, currentMin) != currentMin);

            // Thread-safe max update
            double currentMax;
            do
            {
                currentMax = Volatile.Read(ref _max);
                if (value <= currentMax) break;
            } while (Interlocked.CompareExchange(ref _max, value, currentMax) != currentMax);

            _histogram.Record(value);
        }

        /// <summary>
        /// Records an observation with tags.
        /// </summary>
        /// <param name="value">The value to observe.</param>
        /// <param name="tags">Tags to associate with this measurement.</param>
        public void Observe(double value, params KeyValuePair<string, object?>[] tags)
        {
            Interlocked.Increment(ref _count);

            double currentSum, newSum;
            do
            {
                currentSum = Volatile.Read(ref _sum);
                newSum = currentSum + value;
            } while (Interlocked.CompareExchange(ref _sum, newSum, currentSum) != currentSum);

            _histogram.Record(value, tags);
        }
    }
}
