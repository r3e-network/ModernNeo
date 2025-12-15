// Copyright (C) 2015-2025 The Neo Project.
//
// NullMetricsProvider.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using System.Threading;

namespace Neo.Observability.Metrics
{
    /// <summary>
    /// A no-op metrics provider that does nothing. Used as default when no metrics are configured.
    /// </summary>
    public sealed class NullMetricsProvider : IMetricsProvider
    {
        /// <summary>
        /// Gets the singleton instance of the null metrics provider.
        /// </summary>
        public static NullMetricsProvider Instance { get; } = new();

        private NullMetricsProvider() { }

        /// <inheritdoc/>
        public ICounter CreateCounter(string name, string description) => NullCounter.Instance;

        /// <inheritdoc/>
        public IGauge CreateGauge(string name, string description) => NullGauge.Instance;

        /// <inheritdoc/>
        public IHistogram CreateHistogram(string name, string description, double[]? buckets = null) => NullHistogram.Instance;
    }

    internal sealed class NullCounter : ICounter
    {
        public static NullCounter Instance { get; } = new();
        private NullCounter() { }

        public string Name => string.Empty;
        public long Value => 0;
        public void Increment() { }
        public void Increment(long value) { }
    }

    internal sealed class NullGauge : IGauge
    {
        public static NullGauge Instance { get; } = new();
        private NullGauge() { }

        public string Name => string.Empty;
        public double Value => 0;
        public void Set(double value) { }
        public void Increment() { }
        public void Decrement() { }
    }

    internal sealed class NullHistogram : IHistogram
    {
        public static NullHistogram Instance { get; } = new();
        private NullHistogram() { }

        public string Name => string.Empty;
        public long Count => 0;
        public double Sum => 0;
        public void Observe(double value) { }
    }
}
