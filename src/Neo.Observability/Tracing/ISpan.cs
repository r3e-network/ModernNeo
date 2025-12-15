// Copyright (C) 2015-2025 The Neo Project.
//
// ISpan.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using System;

namespace Neo.Observability.Tracing
{
    /// <summary>
    /// Represents a unit of work or operation being traced.
    /// </summary>
    public interface ISpan : IDisposable
    {
        /// <summary>
        /// Gets the name of the span.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Sets an attribute on the span.
        /// </summary>
        /// <param name="key">The attribute key.</param>
        /// <param name="value">The attribute value.</param>
        /// <returns>The span for method chaining.</returns>
        ISpan SetAttribute(string key, string value);

        /// <summary>
        /// Sets an attribute on the span.
        /// </summary>
        /// <param name="key">The attribute key.</param>
        /// <param name="value">The attribute value.</param>
        /// <returns>The span for method chaining.</returns>
        ISpan SetAttribute(string key, long value);

        /// <summary>
        /// Sets an attribute on the span.
        /// </summary>
        /// <param name="key">The attribute key.</param>
        /// <param name="value">The attribute value.</param>
        /// <returns>The span for method chaining.</returns>
        ISpan SetAttribute(string key, bool value);

        /// <summary>
        /// Records an exception on the span.
        /// </summary>
        /// <param name="exception">The exception to record.</param>
        /// <returns>The span for method chaining.</returns>
        ISpan RecordException(Exception exception);

        /// <summary>
        /// Sets the status of the span.
        /// </summary>
        /// <param name="status">The status to set.</param>
        /// <param name="description">Optional description for the status.</param>
        /// <returns>The span for method chaining.</returns>
        ISpan SetStatus(SpanStatus status, string? description = null);

        /// <summary>
        /// Adds an event to the span.
        /// </summary>
        /// <param name="name">The name of the event.</param>
        /// <returns>The span for method chaining.</returns>
        ISpan AddEvent(string name);
    }

    /// <summary>
    /// Represents the status of a span.
    /// </summary>
    public enum SpanStatus
    {
        /// <summary>
        /// The operation completed successfully.
        /// </summary>
        Ok,

        /// <summary>
        /// The operation encountered an error.
        /// </summary>
        Error,

        /// <summary>
        /// The status is unset.
        /// </summary>
        Unset
    }
}
