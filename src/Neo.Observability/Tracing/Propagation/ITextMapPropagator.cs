// Copyright (C) 2015-2025 The Neo Project.
//
// ITextMapPropagator.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using System;
using System.Collections.Generic;

namespace Neo.Observability.Tracing.Propagation
{
    /// <summary>
    /// Interface for extracting and injecting trace context from/to carriers.
    /// </summary>
    public interface ITextMapPropagator
    {
        /// <summary>
        /// Gets the header names used by this propagator.
        /// </summary>
        IReadOnlyCollection<string> Fields { get; }

        /// <summary>
        /// Extracts trace context from a carrier using the provided getter.
        /// </summary>
        /// <typeparam name="T">The carrier type.</typeparam>
        /// <param name="carrier">The carrier to extract from.</param>
        /// <param name="getter">Function to get header values from the carrier.</param>
        /// <returns>The extracted trace context, or TraceContext.Empty if extraction fails.</returns>
        TraceContext Extract<T>(T carrier, Func<T, string, string?> getter);

        /// <summary>
        /// Injects trace context into a carrier using the provided setter.
        /// </summary>
        /// <typeparam name="T">The carrier type.</typeparam>
        /// <param name="context">The trace context to inject.</param>
        /// <param name="carrier">The carrier to inject into.</param>
        /// <param name="setter">Action to set header values on the carrier.</param>
        void Inject<T>(TraceContext context, T carrier, Action<T, string, string> setter);
    }

    /// <summary>
    /// Delegate for getting a value from a carrier by key.
    /// </summary>
    /// <typeparam name="T">The carrier type.</typeparam>
    /// <param name="carrier">The carrier.</param>
    /// <param name="key">The key to look up.</param>
    /// <returns>The value, or null if not found.</returns>
    public delegate string? CarrierGetter<in T>(T carrier, string key);

    /// <summary>
    /// Delegate for setting a value on a carrier.
    /// </summary>
    /// <typeparam name="T">The carrier type.</typeparam>
    /// <param name="carrier">The carrier.</param>
    /// <param name="key">The key to set.</param>
    /// <param name="value">The value to set.</param>
    public delegate void CarrierSetter<in T>(T carrier, string key, string value);
}
