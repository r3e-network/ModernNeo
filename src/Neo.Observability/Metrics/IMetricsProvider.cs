// Copyright (C) 2015-2025 The Neo Project.
//
// IMetricsProvider.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

namespace Neo.Observability.Metrics
{
    /// <summary>
    /// Provides metrics collection capabilities for the Neo node.
    /// </summary>
    public interface IMetricsProvider
    {
        /// <summary>
        /// Creates or retrieves a counter metric.
        /// </summary>
        /// <param name="name">The name of the counter.</param>
        /// <param name="description">A description of what the counter measures.</param>
        /// <returns>A counter instance.</returns>
        ICounter CreateCounter(string name, string description);

        /// <summary>
        /// Creates or retrieves a gauge metric.
        /// </summary>
        /// <param name="name">The name of the gauge.</param>
        /// <param name="description">A description of what the gauge measures.</param>
        /// <returns>A gauge instance.</returns>
        IGauge CreateGauge(string name, string description);

        /// <summary>
        /// Creates or retrieves a histogram metric.
        /// </summary>
        /// <param name="name">The name of the histogram.</param>
        /// <param name="description">A description of what the histogram measures.</param>
        /// <param name="buckets">Optional bucket boundaries for the histogram.</param>
        /// <returns>A histogram instance.</returns>
        IHistogram CreateHistogram(string name, string description, double[]? buckets = null);
    }
}
