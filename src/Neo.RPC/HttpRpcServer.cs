// Copyright (C) 2015-2025 The Neo Project.
//
// HttpRpcServer.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Json;
using System.Collections.Concurrent;
using System.Net;
using System.Text;

namespace Neo.RPC
{
    /// <summary>
    /// HTTP-based JSON-RPC server implementation.
    /// Provides a lightweight, high-performance RPC endpoint.
    /// </summary>
    public sealed class HttpRpcServer : IRpcServer
    {
        private readonly HttpListener _listener;
        private readonly ConcurrentDictionary<string, IRpcMethod> _methods = new();
        private readonly RpcProcessor _processor;
        private readonly HttpRpcServerOptions _options;
        private readonly CancellationTokenSource _cts = new();
        private Task? _listenerTask;
        private bool _disposed;

        /// <inheritdoc/>
        public bool IsRunning { get; private set; }

        /// <inheritdoc/>
        public string Endpoint => _options.ListenAddress;

        /// <summary>
        /// Event raised when a request is received.
        /// </summary>
        public event EventHandler<RpcRequestEventArgs>? RequestReceived;

        /// <summary>
        /// Event raised when an error occurs.
        /// </summary>
        public event EventHandler<RpcErrorEventArgs>? ErrorOccurred;

        /// <summary>
        /// Creates a new HTTP RPC server.
        /// </summary>
        public HttpRpcServer(HttpRpcServerOptions? options = null)
        {
            _options = options ?? new HttpRpcServerOptions();
            _listener = new HttpListener();
            _listener.Prefixes.Add(_options.ListenAddress);
            _processor = new RpcProcessor();

            if (_options.EnableCors)
            {
                // CORS will be handled in request processing
            }
        }

        /// <inheritdoc/>
        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (IsRunning)
                return;

            _listener.Start();
            IsRunning = true;

            _listenerTask = Task.Run(() => ListenAsync(_cts.Token), cancellationToken);
        }

        /// <inheritdoc/>
        public async Task StopAsync(CancellationToken cancellationToken = default)
        {
            if (!IsRunning)
                return;

            await _cts.CancelAsync();
            _listener.Stop();
            IsRunning = false;

            if (_listenerTask != null)
            {
                try
                {
                    await _listenerTask.WaitAsync(cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    // Expected
                }
            }
        }

        /// <inheritdoc/>
        public void RegisterMethod(IRpcMethod method)
        {
            ArgumentNullException.ThrowIfNull(method);
            _methods[method.Name] = method;
            _processor.RegisterMethod(method);
        }

        /// <inheritdoc/>
        public bool UnregisterMethod(string methodName)
        {
            _processor.UnregisterMethod(methodName);
            return _methods.TryRemove(methodName, out _);
        }

        /// <summary>
        /// Gets all registered method names.
        /// </summary>
        public IEnumerable<string> GetRegisteredMethods() => _methods.Keys;

        private async Task ListenAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested && _listener.IsListening)
            {
                try
                {
                    var context = await _listener.GetContextAsync().WaitAsync(cancellationToken);
                    _ = ProcessRequestAsync(context, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (HttpListenerException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    ErrorOccurred?.Invoke(this, new RpcErrorEventArgs(ex));
                }
            }
        }

        private async Task ProcessRequestAsync(HttpListenerContext context, CancellationToken cancellationToken)
        {
            var request = context.Request;
            var response = context.Response;
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            if (_options.RequestTimeout > TimeSpan.Zero)
                timeoutCts.CancelAfter(_options.RequestTimeout);

            try
            {
                // Handle CORS preflight
                if (_options.EnableCors)
                {
                    response.Headers.Add("Access-Control-Allow-Origin", _options.CorsOrigin);
                    response.Headers.Add("Access-Control-Allow-Methods", "POST, OPTIONS");
                    response.Headers.Add("Access-Control-Allow-Headers", "Content-Type");

                    if (request.HttpMethod == "OPTIONS")
                    {
                        response.StatusCode = 204;
                        response.Close();
                        return;
                    }
                }

                // Only accept POST
                if (request.HttpMethod != "POST")
                {
                    await SendErrorAsync(response, new RpcError { Code = -32600, Message = "Only POST method is allowed" }, null);
                    return;
                }

                var bodyResult = await ReadRequestBodyAsync(request, timeoutCts.Token);
                if (bodyResult.TooLarge)
                {
                    await SendErrorAsync(response, new RpcError { Code = -32600, Message = "Request payload too large" }, null);
                    return;
                }

                var body = bodyResult.Body ?? string.Empty;

                RequestReceived?.Invoke(this, new RpcRequestEventArgs(body, request.RemoteEndPoint));

                // Process using RpcProcessor
                var responseJson = await _processor.ProcessAsync(body).WaitAsync(timeoutCts.Token);

                // Send response
                await SendJsonResponseAsync(response, responseJson);
            }
            catch (OperationCanceledException)
            {
                await SendErrorAsync(response, new RpcError { Code = -32603, Message = "Request timed out" }, null);
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, new RpcErrorEventArgs(ex));
                await SendErrorAsync(response, RpcError.InternalError, null);
            }
            finally
            {
                response.Close();
            }
        }

        private readonly struct RequestBodyResult
        {
            public RequestBodyResult(string? body, bool tooLarge)
            {
                Body = body;
                TooLarge = tooLarge;
            }

            public string? Body { get; }
            public bool TooLarge { get; }
        }

        private async Task<RequestBodyResult> ReadRequestBodyAsync(HttpListenerRequest request, CancellationToken cancellationToken)
        {
            if (request.ContentLength64 > 0 && request.ContentLength64 > _options.MaxRequestSize)
                return new RequestBodyResult(null, tooLarge: true);

            using var ms = new MemoryStream();
            var buffer = new byte[8192];
            long total = 0;

            while (true)
            {
                var read = await request.InputStream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);
                if (read == 0)
                    break;

                total += read;
                if (total > _options.MaxRequestSize)
                    return new RequestBodyResult(null, tooLarge: true);

                ms.Write(buffer, 0, read);
            }

            var body = Encoding.UTF8.GetString(ms.GetBuffer(), 0, (int)total);
            return new RequestBodyResult(body, tooLarge: false);
        }

        private static async Task SendJsonResponseAsync(HttpListenerResponse response, string json)
        {
            response.ContentType = "application/json";
            response.StatusCode = 200;

            var buffer = Encoding.UTF8.GetBytes(json);
            response.ContentLength64 = buffer.Length;
            await response.OutputStream.WriteAsync(buffer);
        }

        private static async Task SendErrorAsync(HttpListenerResponse response, RpcError error, object? id)
        {
            var rpcResponse = RpcResponse.Failure(id, error);
            await SendJsonResponseAsync(response, rpcResponse.ToJson().ToString());
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _cts.Cancel();
            _listener.Close();
            _cts.Dispose();
        }
    }

    /// <summary>
    /// Configuration options for HTTP RPC server.
    /// </summary>
    public sealed class HttpRpcServerOptions
    {
        /// <summary>
        /// Listen address. Default: "http://localhost:10332/".
        /// </summary>
        public string ListenAddress { get; set; } = "http://localhost:10332/";

        /// <summary>
        /// Enable CORS. Default: true.
        /// </summary>
        public bool EnableCors { get; set; } = true;

        /// <summary>
        /// CORS origin. Default: "*".
        /// </summary>
        public string CorsOrigin { get; set; } = "*";

        /// <summary>
        /// Maximum request size in bytes. Default: 1MB.
        /// </summary>
        public int MaxRequestSize { get; set; } = 1024 * 1024;

        /// <summary>
        /// Request timeout. Default: 30 seconds.
        /// </summary>
        public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(30);
    }

    /// <summary>
    /// Event args for RPC request.
    /// </summary>
    public sealed class RpcRequestEventArgs : EventArgs
    {
        public string RequestBody { get; }
        public EndPoint? RemoteEndPoint { get; }

        public RpcRequestEventArgs(string requestBody, EndPoint? remoteEndPoint)
        {
            RequestBody = requestBody;
            RemoteEndPoint = remoteEndPoint;
        }
    }

    /// <summary>
    /// Event args for RPC error.
    /// </summary>
    public sealed class RpcErrorEventArgs : EventArgs
    {
        public Exception Exception { get; }

        public RpcErrorEventArgs(Exception exception)
        {
            Exception = exception;
        }
    }
}
