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

using System;

namespace Neo.Observability.Tracing
{
    /// <summary>
    /// A no-op tracer that does nothing. Used as default when no tracing is configured.
    /// </summary>
    public sealed class NullTracer : ITracer
    {
        /// <summary>
        /// Gets the singleton instance of the null tracer.
        /// </summary>
        public static NullTracer Instance { get; } = new();

        private NullTracer() { }

        /// <inheritdoc/>
        public ISpan StartSpan(string name) => NullSpan.Instance;

        /// <inheritdoc/>
        public ISpan StartSpan(string name, SpanKind kind) => NullSpan.Instance;
    }

    internal sealed class NullSpan : ISpan
    {
        public static NullSpan Instance { get; } = new();
        private NullSpan() { }

        public string Name => string.Empty;

        public ISpan SetAttribute(string key, string value) => this;
        public ISpan SetAttribute(string key, long value) => this;
        public ISpan SetAttribute(string key, bool value) => this;
        public ISpan RecordException(Exception exception) => this;
        public ISpan SetStatus(SpanStatus status, string? description = null) => this;
        public ISpan AddEvent(string name) => this;

        public void Dispose() { }
    }
}
