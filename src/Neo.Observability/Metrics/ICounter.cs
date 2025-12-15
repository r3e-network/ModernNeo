// Copyright (C) 2015-2025 The Neo Project.
//
// ICounter.cs file belongs to the neo project and is free
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
    /// Represents a monotonically increasing counter metric.
    /// </summary>
    public interface ICounter
    {
        /// <summary>
        /// Gets the name of the counter.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Increments the counter by 1.
        /// </summary>
        void Increment();

        /// <summary>
        /// Increments the counter by the specified value.
        /// </summary>
        /// <param name="value">The value to increment by. Must be non-negative.</param>
        void Increment(long value);

        /// <summary>
        /// Gets the current value of the counter.
        /// </summary>
        long Value { get; }
    }
}
