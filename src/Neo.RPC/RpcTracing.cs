// Copyright (C) 2015-2025 The Neo Project.
//
// RpcTracing.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Observability.Tracing;
using Neo.Observability.Tracing.Propagation;
using System.Collections.Specialized;
using System.Diagnostics;

namespace Neo.RPC
{
    /// <summary>
    /// Provides distributed tracing support for RPC requests.
    /// </summary>
    public static class RpcTracing
    {
        /// <summary>
        /// The ActivitySource name for RPC tracing.
        /// </summary>
        public const string ActivitySourceName = "Neo.RPC";

        private static readonly ActivitySource s_activitySource = new(ActivitySourceName, "1.0.0");

        /// <summary>
        /// Gets the ActivitySource for RPC tracing.
        /// </summary>
        public static ActivitySource ActivitySource => s_activitySource;

        /// <summary>
        /// Starts a server span for an incoming RPC request.
        /// </summary>
        /// <param name="methodName">The RPC method name.</param>
        /// <param name="headers">Optional HTTP headers for context extraction.</param>
        /// <returns>An Activity representing the span, or null if not sampled.</returns>
        public static Activity? StartServerSpan(string methodName, NameValueCollection? headers = null)
        {
            ActivityContext parentContext = default;

            // Extract trace context from headers if available
            if (headers != null)
            {
                var traceparent = headers["traceparent"];
                if (!string.IsNullOrEmpty(traceparent))
                {
                    var tracestate = headers["tracestate"];
                    if (W3CTraceContextPropagator.TryParseTraceParent(traceparent, out var traceId, out var spanId, out var flags))
                    {
                        if (TraceContext.TryParse(traceId, spanId, flags, tracestate, out var context))
                        {
                            parentContext = context.ActivityContext;
                        }
                    }
                }
            }

            var activity = s_activitySource.StartActivity(
                $"rpc.{methodName}",
                ActivityKind.Server,
                parentContext);

            if (activity != null)
            {
                activity.SetTag("rpc.system", "jsonrpc");
                activity.SetTag("rpc.method", methodName);
                activity.SetTag("rpc.service", "neo");
            }

            return activity;
        }

        /// <summary>
        /// Records RPC request attributes on a span.
        /// </summary>
        /// <param name="activity">The activity to record on.</param>
        /// <param name="requestId">The JSON-RPC request ID.</param>
        /// <param name="remoteAddress">The remote client address.</param>
        public static void RecordRequestAttributes(Activity? activity, object? requestId, string? remoteAddress)
        {
            if (activity == null) return;

            if (requestId != null)
            {
                activity.SetTag("rpc.request.id", requestId.ToString());
            }

            if (!string.IsNullOrEmpty(remoteAddress))
            {
                activity.SetTag("net.peer.ip", remoteAddress);
            }
        }

        /// <summary>
        /// Records a successful RPC response.
        /// </summary>
        /// <param name="activity">The activity to record on.</param>
        public static void RecordSuccess(Activity? activity)
        {
            if (activity == null) return;

            activity.SetStatus(ActivityStatusCode.Ok);
            activity.SetTag("rpc.response.status", "success");
        }

        /// <summary>
        /// Records an RPC error response.
        /// </summary>
        /// <param name="activity">The activity to record on.</param>
        /// <param name="errorCode">The error code.</param>
        /// <param name="errorMessage">The error message.</param>
        public static void RecordError(Activity? activity, int errorCode, string? errorMessage)
        {
            if (activity == null) return;

            activity.SetStatus(ActivityStatusCode.Error, errorMessage);
            activity.SetTag("rpc.response.status", "error");
            activity.SetTag("rpc.error.code", errorCode);

            if (!string.IsNullOrEmpty(errorMessage))
            {
                activity.SetTag("rpc.error.message", errorMessage);
            }
        }

        /// <summary>
        /// Records an exception on the span.
        /// </summary>
        /// <param name="activity">The activity to record on.</param>
        /// <param name="exception">The exception that occurred.</param>
        public static void RecordException(Activity? activity, Exception exception)
        {
            if (activity == null) return;

            activity.SetStatus(ActivityStatusCode.Error, exception.Message);
            activity.AddEvent(new ActivityEvent("exception", tags: new ActivityTagsCollection
            {
                { "exception.type", exception.GetType().FullName },
                { "exception.message", exception.Message },
                { "exception.stacktrace", exception.StackTrace }
            }));
        }
    }

    /// <summary>
    /// Extension methods for integrating tracing with RpcProcessor.
    /// </summary>
    public static class RpcProcessorTracingExtensions
    {
        /// <summary>
        /// Processes a JSON-RPC request with distributed tracing.
        /// </summary>
        /// <param name="processor">The RPC processor.</param>
        /// <param name="requestJson">The JSON request string.</param>
        /// <param name="headers">Optional HTTP headers for context extraction.</param>
        /// <param name="remoteAddress">Optional remote client address.</param>
        /// <returns>The JSON response string.</returns>
        public static async Task<string> ProcessWithTracingAsync(
            this RpcProcessor processor,
            string requestJson,
            NameValueCollection? headers = null,
            string? remoteAddress = null)
        {
            // Try to extract method name from request for span naming
            var methodName = TryExtractMethodName(requestJson) ?? "unknown";

            using var activity = RpcTracing.StartServerSpan(methodName, headers);
            RpcTracing.RecordRequestAttributes(activity, null, remoteAddress);

            try
            {
                var response = await processor.ProcessAsync(requestJson);

                // Check if response indicates an error
                if (response.Contains("\"error\""))
                {
                    RpcTracing.RecordError(activity, -1, "RPC error in response");
                }
                else
                {
                    RpcTracing.RecordSuccess(activity);
                }

                return response;
            }
            catch (Exception ex)
            {
                RpcTracing.RecordException(activity, ex);
                throw;
            }
        }

        private static string? TryExtractMethodName(string requestJson)
        {
            try
            {
                // Simple extraction without full JSON parsing for performance
                var methodIndex = requestJson.IndexOf("\"method\"", StringComparison.Ordinal);
                if (methodIndex < 0) return null;

                var colonIndex = requestJson.IndexOf(':', methodIndex);
                if (colonIndex < 0) return null;

                var quoteStart = requestJson.IndexOf('"', colonIndex);
                if (quoteStart < 0) return null;

                var quoteEnd = requestJson.IndexOf('"', quoteStart + 1);
                if (quoteEnd < 0) return null;

                return requestJson.Substring(quoteStart + 1, quoteEnd - quoteStart - 1);
            }
            catch
            {
                return null;
            }
        }
    }
}
