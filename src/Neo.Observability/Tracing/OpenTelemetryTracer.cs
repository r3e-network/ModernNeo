// Copyright (C) 2015-2025 The Neo Project.
//
// OpenTelemetryTracer.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Neo.Observability.Tracing.Propagation;
using OtelActivityKind = System.Diagnostics.ActivityKind;
using OtelActivityStatusCode = System.Diagnostics.ActivityStatusCode;

namespace Neo.Observability.Tracing
{
    /// <summary>
    /// OpenTelemetry-based tracer implementation using System.Diagnostics.Activity.
    /// </summary>
    public sealed class OpenTelemetryTracer : ITracer
    {
        private readonly ActivitySource _activitySource;

        /// <summary>
        /// Gets the ActivitySource used by this tracer.
        /// </summary>
        public ActivitySource ActivitySource => _activitySource;

        /// <summary>
        /// Initializes a new instance of the OpenTelemetryTracer class.
        /// </summary>
        /// <param name="serviceName">The name of the service for tracing.</param>
        /// <param name="serviceVersion">The version of the service.</param>
        public OpenTelemetryTracer(string serviceName, string? serviceVersion = null)
        {
            _activitySource = new ActivitySource(serviceName, serviceVersion);
        }

        /// <summary>
        /// Initializes a new instance of the OpenTelemetryTracer class with an existing ActivitySource.
        /// </summary>
        /// <param name="activitySource">The ActivitySource to use.</param>
        public OpenTelemetryTracer(ActivitySource activitySource)
        {
            _activitySource = activitySource ?? throw new ArgumentNullException(nameof(activitySource));
        }

        /// <inheritdoc/>
        public ISpan StartSpan(string name)
        {
            return StartSpan(name, SpanKind.Internal);
        }

        /// <inheritdoc/>
        public ISpan StartSpan(string name, SpanKind kind)
        {
            var activity = _activitySource.StartActivity(name, ConvertSpanKind(kind));
            return activity != null
                ? new OpenTelemetrySpan(activity)
                : NullSpan.Instance;
        }

        /// <inheritdoc/>
        public ISpan StartSpan(string name, SpanKind kind, TraceContext parentContext)
        {
            var activity = _activitySource.StartActivity(
                name,
                ConvertSpanKind(kind),
                parentContext.ActivityContext);

            return activity != null
                ? new OpenTelemetrySpan(activity)
                : NullSpan.Instance;
        }

        /// <inheritdoc/>
        public ISpan StartSpan(string name, SpanKind kind, IEnumerable<SpanLink> links)
        {
            var activityLinks = links.Select(l => l.ToActivityLink()).ToList();

            var activity = _activitySource.StartActivity(
                name,
                ConvertSpanKind(kind),
                default(ActivityContext),
                tags: null,
                links: activityLinks);

            return activity != null
                ? new OpenTelemetrySpan(activity)
                : NullSpan.Instance;
        }

        /// <inheritdoc/>
        public ISpan StartSpan(string name, SpanKind kind, TraceContext parentContext, IEnumerable<SpanLink> links)
        {
            var activityLinks = links.Select(l => l.ToActivityLink()).ToList();

            var activity = _activitySource.StartActivity(
                name,
                ConvertSpanKind(kind),
                parentContext.ActivityContext,
                tags: null,
                links: activityLinks);

            return activity != null
                ? new OpenTelemetrySpan(activity)
                : NullSpan.Instance;
        }

        /// <inheritdoc/>
        public TraceContext GetCurrentContext()
        {
            return TraceContext.Current;
        }

        private static OtelActivityKind ConvertSpanKind(SpanKind kind) => kind switch
        {
            SpanKind.Internal => OtelActivityKind.Internal,
            SpanKind.Server => OtelActivityKind.Server,
            SpanKind.Client => OtelActivityKind.Client,
            SpanKind.Producer => OtelActivityKind.Producer,
            SpanKind.Consumer => OtelActivityKind.Consumer,
            _ => OtelActivityKind.Internal
        };
    }

    /// <summary>
    /// OpenTelemetry-based span implementation wrapping System.Diagnostics.Activity.
    /// </summary>
    internal sealed class OpenTelemetrySpan : ISpan
    {
        private readonly Activity _activity;

        public OpenTelemetrySpan(Activity activity)
        {
            _activity = activity ?? throw new ArgumentNullException(nameof(activity));
        }

        /// <inheritdoc/>
        public string Name => _activity.DisplayName;

        /// <inheritdoc/>
        public ISpan SetAttribute(string key, string value)
        {
            _activity.SetTag(key, value);
            return this;
        }

        /// <inheritdoc/>
        public ISpan SetAttribute(string key, long value)
        {
            _activity.SetTag(key, value);
            return this;
        }

        /// <inheritdoc/>
        public ISpan SetAttribute(string key, bool value)
        {
            _activity.SetTag(key, value);
            return this;
        }

        /// <inheritdoc/>
        public ISpan RecordException(Exception exception)
        {
            _activity.SetStatus(OtelActivityStatusCode.Error, exception.Message);
            _activity.AddEvent(new ActivityEvent("exception", tags: new ActivityTagsCollection
            {
                { "exception.type", exception.GetType().FullName },
                { "exception.message", exception.Message },
                { "exception.stacktrace", exception.StackTrace }
            }));
            return this;
        }

        /// <inheritdoc/>
        public ISpan SetStatus(SpanStatus status, string? description = null)
        {
            var otelStatus = status switch
            {
                SpanStatus.Ok => OtelActivityStatusCode.Ok,
                SpanStatus.Error => OtelActivityStatusCode.Error,
                _ => OtelActivityStatusCode.Unset
            };
            _activity.SetStatus(otelStatus, description);
            return this;
        }

        /// <inheritdoc/>
        public ISpan AddEvent(string name)
        {
            _activity.AddEvent(new ActivityEvent(name));
            return this;
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            _activity.Dispose();
        }
    }
}
