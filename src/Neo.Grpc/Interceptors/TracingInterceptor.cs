// Copyright (C) 2015-2025 The Neo Project.
//
// TracingInterceptor.cs file belongs to the neo project and is free
// software distributed under the MIT software license.

using Grpc.Core;
using Grpc.Core.Interceptors;
using System.Diagnostics;

namespace Neo.Grpc.Interceptors;

/// <summary>
/// gRPC server interceptor that adds distributed tracing to all RPC calls.
/// </summary>
public class TracingInterceptor : Interceptor
{
    /// <summary>
    /// The ActivitySource name for gRPC tracing.
    /// </summary>
    public const string ActivitySourceName = "Neo.Grpc";

    private static readonly ActivitySource s_activitySource = new(ActivitySourceName, "1.0.0");

    /// <summary>
    /// Gets the ActivitySource for gRPC tracing.
    /// </summary>
    public static ActivitySource ActivitySource => s_activitySource;

    /// <inheritdoc/>
    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request,
        ServerCallContext context,
        UnaryServerMethod<TRequest, TResponse> continuation)
    {
        var parentContext = ExtractTraceContext(context.RequestHeaders);

        using var activity = s_activitySource.StartActivity(
            $"grpc.{context.Method}",
            ActivityKind.Server,
            parentContext);

        if (activity != null)
        {
            SetCommonTags(activity, context);
        }

        try
        {
            var response = await continuation(request, context);

            activity?.SetStatus(ActivityStatusCode.Ok);
            activity?.SetTag("grpc.status_code", (int)context.Status.StatusCode);

            return response;
        }
        catch (RpcException ex)
        {
            RecordRpcException(activity, ex);
            throw;
        }
        catch (Exception ex)
        {
            RecordException(activity, ex);
            throw;
        }
    }

    /// <inheritdoc/>
    public override async Task ServerStreamingServerHandler<TRequest, TResponse>(
        TRequest request,
        IServerStreamWriter<TResponse> responseStream,
        ServerCallContext context,
        ServerStreamingServerMethod<TRequest, TResponse> continuation)
    {
        var parentContext = ExtractTraceContext(context.RequestHeaders);

        using var activity = s_activitySource.StartActivity(
            $"grpc.{context.Method}",
            ActivityKind.Server,
            parentContext);

        if (activity != null)
        {
            SetCommonTags(activity, context);
            activity.SetTag("grpc.stream_type", "server_streaming");
        }

        try
        {
            await continuation(request, responseStream, context);
            activity?.SetStatus(ActivityStatusCode.Ok);
        }
        catch (RpcException ex)
        {
            RecordRpcException(activity, ex);
            throw;
        }
        catch (Exception ex)
        {
            RecordException(activity, ex);
            throw;
        }
    }

    /// <inheritdoc/>
    public override async Task<TResponse> ClientStreamingServerHandler<TRequest, TResponse>(
        IAsyncStreamReader<TRequest> requestStream,
        ServerCallContext context,
        ClientStreamingServerMethod<TRequest, TResponse> continuation)
    {
        var parentContext = ExtractTraceContext(context.RequestHeaders);

        using var activity = s_activitySource.StartActivity(
            $"grpc.{context.Method}",
            ActivityKind.Server,
            parentContext);

        if (activity != null)
        {
            SetCommonTags(activity, context);
            activity.SetTag("grpc.stream_type", "client_streaming");
        }

        try
        {
            var response = await continuation(requestStream, context);
            activity?.SetStatus(ActivityStatusCode.Ok);
            return response;
        }
        catch (RpcException ex)
        {
            RecordRpcException(activity, ex);
            throw;
        }
        catch (Exception ex)
        {
            RecordException(activity, ex);
            throw;
        }
    }

    /// <inheritdoc/>
    public override async Task DuplexStreamingServerHandler<TRequest, TResponse>(
        IAsyncStreamReader<TRequest> requestStream,
        IServerStreamWriter<TResponse> responseStream,
        ServerCallContext context,
        DuplexStreamingServerMethod<TRequest, TResponse> continuation)
    {
        var parentContext = ExtractTraceContext(context.RequestHeaders);

        using var activity = s_activitySource.StartActivity(
            $"grpc.{context.Method}",
            ActivityKind.Server,
            parentContext);

        if (activity != null)
        {
            SetCommonTags(activity, context);
            activity.SetTag("grpc.stream_type", "duplex_streaming");
        }

        try
        {
            await continuation(requestStream, responseStream, context);
            activity?.SetStatus(ActivityStatusCode.Ok);
        }
        catch (RpcException ex)
        {
            RecordRpcException(activity, ex);
            throw;
        }
        catch (Exception ex)
        {
            RecordException(activity, ex);
            throw;
        }
    }

    private static ActivityContext ExtractTraceContext(Metadata headers)
    {
        var traceparent = headers.GetValue("traceparent");
        if (string.IsNullOrEmpty(traceparent))
            return default;

        var tracestate = headers.GetValue("tracestate");

        if (Observability.Tracing.Propagation.W3CTraceContextPropagator.TryParseTraceParent(
            traceparent, out var traceId, out var spanId, out var flags))
        {
            if (Observability.Tracing.Propagation.TraceContext.TryParse(
                traceId, spanId, flags, tracestate, out var context))
            {
                return context.ActivityContext;
            }
        }

        return default;
    }

    private static void SetCommonTags(Activity activity, ServerCallContext context)
    {
        activity.SetTag("rpc.system", "grpc");
        activity.SetTag("rpc.service", GetServiceName(context.Method));
        activity.SetTag("rpc.method", GetMethodName(context.Method));
        activity.SetTag("net.peer.ip", context.Peer);

        if (context.Deadline != DateTime.MaxValue)
        {
            activity.SetTag("grpc.deadline", context.Deadline.ToString("O"));
        }
    }

    private static void RecordRpcException(Activity? activity, RpcException ex)
    {
        if (activity == null) return;

        activity.SetStatus(ActivityStatusCode.Error, ex.Status.Detail);
        activity.SetTag("grpc.status_code", (int)ex.StatusCode);
        activity.SetTag("grpc.error.detail", ex.Status.Detail);

        activity.AddEvent(new ActivityEvent("grpc.error", tags: new ActivityTagsCollection
        {
            { "grpc.status_code", (int)ex.StatusCode },
            { "grpc.status_detail", ex.Status.Detail }
        }));
    }

    private static void RecordException(Activity? activity, Exception ex)
    {
        if (activity == null) return;

        activity.SetStatus(ActivityStatusCode.Error, ex.Message);
        activity.SetTag("grpc.status_code", (int)StatusCode.Internal);

        activity.AddEvent(new ActivityEvent("exception", tags: new ActivityTagsCollection
        {
            { "exception.type", ex.GetType().FullName },
            { "exception.message", ex.Message },
            { "exception.stacktrace", ex.StackTrace }
        }));
    }

    private static string GetServiceName(string fullMethod)
    {
        // Format: /package.Service/Method
        var parts = fullMethod.Split('/');
        if (parts.Length >= 2)
        {
            var servicePart = parts[1];
            var lastDot = servicePart.LastIndexOf('.');
            return lastDot >= 0 ? servicePart[(lastDot + 1)..] : servicePart;
        }
        return "unknown";
    }

    private static string GetMethodName(string fullMethod)
    {
        // Format: /package.Service/Method
        var parts = fullMethod.Split('/');
        return parts.Length >= 3 ? parts[2] : "unknown";
    }
}

/// <summary>
/// Extension methods for Metadata to simplify header access.
/// </summary>
internal static class MetadataExtensions
{
    /// <summary>
    /// Gets a header value by key, or null if not found.
    /// </summary>
    public static string? GetValue(this Metadata metadata, string key)
    {
        foreach (var entry in metadata)
        {
            if (string.Equals(entry.Key, key, StringComparison.OrdinalIgnoreCase))
            {
                return entry.Value;
            }
        }
        return null;
    }
}
