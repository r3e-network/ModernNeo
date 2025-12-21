// Copyright (C) 2015-2025 The Neo Project.
//
// TraceContext.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using System;
using System.Diagnostics;

namespace Neo.Observability.Tracing.Propagation
{
    /// <summary>
    /// Represents a trace context for distributed tracing propagation.
    /// Wraps System.Diagnostics.ActivityContext for OpenTelemetry compatibility.
    /// </summary>
    public readonly struct TraceContext : IEquatable<TraceContext>
    {
        /// <summary>
        /// Gets an empty/invalid trace context.
        /// </summary>
        public static readonly TraceContext Empty = default;

        private readonly ActivityContext _context;

        /// <summary>
        /// Gets the trace ID as a hex string.
        /// </summary>
        public string TraceId => _context.TraceId.ToHexString();

        /// <summary>
        /// Gets the span ID as a hex string.
        /// </summary>
        public string SpanId => _context.SpanId.ToHexString();

        /// <summary>
        /// Gets the trace flags.
        /// </summary>
        public byte TraceFlags => (byte)_context.TraceFlags;

        /// <summary>
        /// Gets the trace state string.
        /// </summary>
        public string? TraceState => _context.TraceState;

        /// <summary>
        /// Gets whether this context is valid (has non-zero trace and span IDs).
        /// </summary>
        public bool IsValid => _context.TraceId != default && _context.SpanId != default;

        /// <summary>
        /// Gets whether this trace is sampled.
        /// </summary>
        public bool IsSampled => (_context.TraceFlags & ActivityTraceFlags.Recorded) != 0;

        /// <summary>
        /// Gets the underlying ActivityContext.
        /// </summary>
        public ActivityContext ActivityContext => _context;

        /// <summary>
        /// Initializes a new TraceContext from an ActivityContext.
        /// </summary>
        /// <param name="context">The ActivityContext to wrap.</param>
        public TraceContext(ActivityContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Initializes a new TraceContext from trace and span IDs.
        /// </summary>
        /// <param name="traceId">The trace ID as a 32-character hex string.</param>
        /// <param name="spanId">The span ID as a 16-character hex string.</param>
        /// <param name="traceFlags">The trace flags (default: sampled).</param>
        /// <param name="traceState">Optional trace state.</param>
        public TraceContext(string traceId, string spanId, byte traceFlags = 1, string? traceState = null)
        {
            if (string.IsNullOrEmpty(traceId) || traceId.Length != 32)
                throw new ArgumentException("Invalid trace ID format (must be 32 hex characters)", nameof(traceId));

            if (string.IsNullOrEmpty(spanId) || spanId.Length != 16)
                throw new ArgumentException("Invalid span ID format (must be 16 hex characters)", nameof(spanId));

            try
            {
                var activityTraceId = ActivityTraceId.CreateFromString(traceId.AsSpan());
                var activitySpanId = ActivitySpanId.CreateFromString(spanId.AsSpan());

                _context = new ActivityContext(
                    activityTraceId,
                    activitySpanId,
                    (ActivityTraceFlags)traceFlags,
                    traceState,
                    isRemote: true);
            }
            catch (Exception ex) when (ex is FormatException or ArgumentException)
            {
                throw new ArgumentException("Invalid trace or span ID format", ex);
            }
        }

        /// <summary>
        /// Creates a TraceContext from the current Activity.
        /// </summary>
        /// <returns>The current trace context, or Empty if no activity is active.</returns>
        public static TraceContext Current
        {
            get
            {
                var activity = Activity.Current;
                return activity != null
                    ? new TraceContext(activity.Context)
                    : Empty;
            }
        }

        /// <summary>
        /// Tries to parse a TraceContext from trace and span ID strings.
        /// </summary>
        /// <param name="traceId">The trace ID.</param>
        /// <param name="spanId">The span ID.</param>
        /// <param name="traceFlags">The trace flags.</param>
        /// <param name="traceState">The trace state.</param>
        /// <param name="context">The parsed context.</param>
        /// <returns>True if parsing succeeded.</returns>
        public static bool TryParse(string? traceId, string? spanId, byte traceFlags, string? traceState, out TraceContext context)
        {
            context = Empty;

            if (string.IsNullOrEmpty(traceId) || traceId.Length != 32)
                return false;

            if (string.IsNullOrEmpty(spanId) || spanId.Length != 16)
                return false;

            try
            {
                var activityTraceId = ActivityTraceId.CreateFromString(traceId.AsSpan());
                var activitySpanId = ActivitySpanId.CreateFromString(spanId.AsSpan());

                context = new TraceContext(new ActivityContext(
                    activityTraceId,
                    activitySpanId,
                    (ActivityTraceFlags)traceFlags,
                    traceState,
                    isRemote: true));

                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <inheritdoc/>
        public bool Equals(TraceContext other) =>
            _context.TraceId == other._context.TraceId &&
            _context.SpanId == other._context.SpanId;

        /// <inheritdoc/>
        public override bool Equals(object? obj) =>
            obj is TraceContext other && Equals(other);

        /// <inheritdoc/>
        public override int GetHashCode() =>
            HashCode.Combine(_context.TraceId, _context.SpanId);

        /// <inheritdoc/>
        public override string ToString() =>
            IsValid ? $"TraceId={TraceId}, SpanId={SpanId}, Sampled={IsSampled}" : "Empty";

        /// <summary>
        /// Equality operator.
        /// </summary>
        public static bool operator ==(TraceContext left, TraceContext right) => left.Equals(right);

        /// <summary>
        /// Inequality operator.
        /// </summary>
        public static bool operator !=(TraceContext left, TraceContext right) => !left.Equals(right);
    }
}
