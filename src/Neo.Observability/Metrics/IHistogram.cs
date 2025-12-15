// Copyright (C) 2015-2025 The Neo Project.
//
// IHistogram.cs file belongs to the neo project and is free
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
    /// Represents a histogram metric for measuring distributions.
    /// </summary>
    public interface IHistogram
    {
        /// <summary>
        /// Gets the name of the histogram.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Records an observation in the histogram.
        /// </summary>
        /// <param name="value">The value to observe.</param>
        void Observe(double value);

        /// <summary>
        /// Gets the total count of observations.
        /// </summary>
        long Count { get; }

        /// <summary>
        /// Gets the sum of all observed values.
        /// </summary>
        double Sum { get; }
    }
}
