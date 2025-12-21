// Copyright (C) 2015-2025 The Neo Project.
//
// W3CTraceContextPropagator.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Neo.Observability.Tracing.Propagation
{
    /// <summary>
    /// W3C Trace Context propagator implementing the traceparent and tracestate headers.
    /// See: https://www.w3.org/TR/trace-context/
    /// </summary>
    public sealed partial class W3CTraceContextPropagator : ITextMapPropagator
    {
        /// <summary>
        /// The traceparent header name.
        /// </summary>
        public const string TraceParentHeader = "traceparent";

        /// <summary>
        /// The tracestate header name.
        /// </summary>
        public const string TraceStateHeader = "tracestate";

        /// <summary>
        /// Gets the singleton instance of the W3C propagator.
        /// </summary>
        public static W3CTraceContextPropagator Instance { get; } = new();

        private static readonly string[] s_fields = [TraceParentHeader, TraceStateHeader];

        // traceparent format: version-traceid-spanid-traceflags
        // Example: 00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01
        [GeneratedRegex(@"^([0-9a-f]{2})-([0-9a-f]{32})-([0-9a-f]{16})-([0-9a-f]{2})$", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
        private static partial Regex TraceParentRegex();

        /// <inheritdoc/>
        public IReadOnlyCollection<string> Fields => s_fields;

        /// <inheritdoc/>
        public TraceContext Extract<T>(T carrier, Func<T, string, string?> getter)
        {
            ArgumentNullException.ThrowIfNull(carrier);
            ArgumentNullException.ThrowIfNull(getter);

            var traceparent = getter(carrier, TraceParentHeader);
            if (string.IsNullOrEmpty(traceparent))
                return TraceContext.Empty;

            if (!TryParseTraceParent(traceparent, out var traceId, out var spanId, out var traceFlags))
                return TraceContext.Empty;

            var tracestate = getter(carrier, TraceStateHeader);

            if (TraceContext.TryParse(traceId, spanId, traceFlags, tracestate, out var context))
                return context;

            return TraceContext.Empty;
        }

        /// <inheritdoc/>
        public void Inject<T>(TraceContext context, T carrier, Action<T, string, string> setter)
        {
            ArgumentNullException.ThrowIfNull(carrier);
            ArgumentNullException.ThrowIfNull(setter);

            if (!context.IsValid)
                return;

            // Format: version-traceid-spanid-traceflags
            var traceparent = $"00-{context.TraceId}-{context.SpanId}-{context.TraceFlags:x2}";
            setter(carrier, TraceParentHeader, traceparent);

            if (!string.IsNullOrEmpty(context.TraceState))
            {
                setter(carrier, TraceStateHeader, context.TraceState!);
            }
        }

        /// <summary>
        /// Tries to parse a traceparent header value.
        /// </summary>
        /// <param name="traceparent">The traceparent header value.</param>
        /// <param name="traceId">The extracted trace ID.</param>
        /// <param name="spanId">The extracted span ID.</param>
        /// <param name="traceFlags">The extracted trace flags.</param>
        /// <returns>True if parsing succeeded.</returns>
        public static bool TryParseTraceParent(string traceparent, out string traceId, out string spanId, out byte traceFlags)
        {
            traceId = string.Empty;
            spanId = string.Empty;
            traceFlags = 0;

            if (string.IsNullOrEmpty(traceparent))
                return false;

            var match = TraceParentRegex().Match(traceparent);
            if (!match.Success)
                return false;

            var version = match.Groups[1].Value;

            // Version 00 is the only supported version
            // Future versions should be forward-compatible
            if (version != "00" && version[0] == 'f' && version[1] == 'f')
                return false; // Invalid version ff

            traceId = match.Groups[2].Value;
            spanId = match.Groups[3].Value;

            // Validate trace ID is not all zeros
            if (traceId == "00000000000000000000000000000000")
                return false;

            // Validate span ID is not all zeros
            if (spanId == "0000000000000000")
                return false;

            if (!byte.TryParse(match.Groups[4].Value, System.Globalization.NumberStyles.HexNumber, null, out traceFlags))
                return false;

            return true;
        }

        /// <summary>
        /// Creates a traceparent header value from a trace context.
        /// </summary>
        /// <param name="context">The trace context.</param>
        /// <returns>The traceparent header value, or null if context is invalid.</returns>
        public static string? FormatTraceParent(TraceContext context)
        {
            if (!context.IsValid)
                return null;

            return $"00-{context.TraceId}-{context.SpanId}-{context.TraceFlags:x2}";
        }
    }
}
