// Copyright (C) 2015-2025 The Neo Project.
//
// NullTracer.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Observability.Tracing.Propagation;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Neo.Observability.Tracing
{
    /// <summary>
    /// A no-op tracer that does nothing. Used as default when no tracing is configured.
    /// All methods are aggressively inlined to eliminate call overhead in hot paths.
    /// </summary>
    public sealed class NullTracer : ITracer
    {
        /// <summary>
        /// Gets the singleton instance of the null tracer.
        /// </summary>
        public static NullTracer Instance { get; } = new();

        private NullTracer() { }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ISpan StartSpan(string name) => NullSpan.Instance;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ISpan StartSpan(string name, SpanKind kind) => NullSpan.Instance;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ISpan StartSpan(string name, SpanKind kind, TraceContext parentContext) => NullSpan.Instance;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ISpan StartSpan(string name, SpanKind kind, IEnumerable<SpanLink> links) => NullSpan.Instance;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ISpan StartSpan(string name, SpanKind kind, TraceContext parentContext, IEnumerable<SpanLink> links) => NullSpan.Instance;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TraceContext GetCurrentContext() => TraceContext.Empty;
    }

    internal sealed class NullSpan : ISpan
    {
        public static NullSpan Instance { get; } = new();
        private NullSpan() { }

        public string Name => string.Empty;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ISpan SetAttribute(string key, string value) => this;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ISpan SetAttribute(string key, long value) => this;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ISpan SetAttribute(string key, bool value) => this;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ISpan RecordException(Exception exception) => this;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ISpan SetStatus(SpanStatus status, string? description = null) => this;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ISpan AddEvent(string name) => this;

        /// <summary>
        /// No-op dispose. In-memory null span requires no cleanup.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose() { }
    }
}
