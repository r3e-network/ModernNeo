// Copyright (C) 2015-2025 The Neo Project.
//
// ITracer.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using System;
using System.Collections.Generic;
using Neo.Observability.Tracing.Propagation;

namespace Neo.Observability.Tracing
{
    /// <summary>
    /// Provides distributed tracing capabilities for the Neo node.
    /// </summary>
    public interface ITracer
    {
        /// <summary>
        /// Starts a new span with the specified name.
        /// </summary>
        /// <param name="name">The name of the span.</param>
        /// <returns>A span that should be disposed when the operation completes.</returns>
        ISpan StartSpan(string name);

        /// <summary>
        /// Starts a new span with the specified name and kind.
        /// </summary>
        /// <param name="name">The name of the span.</param>
        /// <param name="kind">The kind of span.</param>
        /// <returns>A span that should be disposed when the operation completes.</returns>
        ISpan StartSpan(string name, SpanKind kind);

        /// <summary>
        /// Starts a new span with an explicit parent context.
        /// Used for distributed tracing across service boundaries.
        /// </summary>
        /// <param name="name">The name of the span.</param>
        /// <param name="kind">The kind of span.</param>
        /// <param name="parentContext">The parent trace context.</param>
        /// <returns>A span that should be disposed when the operation completes.</returns>
        ISpan StartSpan(string name, SpanKind kind, TraceContext parentContext);

        /// <summary>
        /// Starts a new span with links to related spans.
        /// Used for correlation without parent-child relationship (e.g., fan-out, batch processing).
        /// </summary>
        /// <param name="name">The name of the span.</param>
        /// <param name="kind">The kind of span.</param>
        /// <param name="links">Links to related spans.</param>
        /// <returns>A span that should be disposed when the operation completes.</returns>
        ISpan StartSpan(string name, SpanKind kind, IEnumerable<SpanLink> links);

        /// <summary>
        /// Starts a new span with an explicit parent context and links.
        /// </summary>
        /// <param name="name">The name of the span.</param>
        /// <param name="kind">The kind of span.</param>
        /// <param name="parentContext">The parent trace context.</param>
        /// <param name="links">Links to related spans.</param>
        /// <returns>A span that should be disposed when the operation completes.</returns>
        ISpan StartSpan(string name, SpanKind kind, TraceContext parentContext, IEnumerable<SpanLink> links);

        /// <summary>
        /// Gets the current trace context from the ambient activity.
        /// </summary>
        /// <returns>The current trace context, or TraceContext.Empty if none.</returns>
        TraceContext GetCurrentContext();
    }

    /// <summary>
    /// Represents the kind of a span.
    /// </summary>
    public enum SpanKind
    {
        /// <summary>
        /// Internal operation within the application.
        /// </summary>
        Internal,

        /// <summary>
        /// Server-side handling of a request.
        /// </summary>
        Server,

        /// <summary>
        /// Client-side request to an external service.
        /// </summary>
        Client,

        /// <summary>
        /// Producer of a message.
        /// </summary>
        Producer,

        /// <summary>
        /// Consumer of a message.
        /// </summary>
        Consumer
    }
}
