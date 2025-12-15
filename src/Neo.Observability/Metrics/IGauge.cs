// Copyright (C) 2015-2025 The Neo Project.
//
// IGauge.cs file belongs to the neo project and is free
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
    /// Represents a gauge metric that can increase or decrease.
    /// </summary>
    public interface IGauge
    {
        /// <summary>
        /// Gets the name of the gauge.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Sets the gauge to the specified value.
        /// </summary>
        /// <param name="value">The value to set.</param>
        void Set(double value);

        /// <summary>
        /// Increments the gauge by 1.
        /// </summary>
        void Increment();

        /// <summary>
        /// Decrements the gauge by 1.
        /// </summary>
        void Decrement();

        /// <summary>
        /// Gets the current value of the gauge.
        /// </summary>
        double Value { get; }
    }
}
