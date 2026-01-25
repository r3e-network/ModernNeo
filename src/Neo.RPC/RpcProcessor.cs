// Copyright (C) 2015-2025 The Neo Project.
//
// RpcProcessor.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Neo.Json;
using System.Collections.Concurrent;

namespace Neo.RPC
{
    /// <summary>
    /// Processes JSON-RPC requests by routing them to registered method handlers.
    /// </summary>
    public class RpcProcessor
    {
        private readonly ConcurrentDictionary<string, IRpcMethod> _methods = new();
        private readonly ILogger<RpcProcessor> _logger;

        /// <summary>
        /// Gets the number of registered methods.
        /// </summary>
        public int MethodCount => _methods.Count;

        /// <summary>
        /// Gets the names of all registered methods.
        /// </summary>
        public IEnumerable<string> RegisteredMethods => _methods.Keys;

        /// <summary>
        /// Initializes a new instance of the <see cref="RpcProcessor"/> class.
        /// </summary>
        /// <param name="logger">Optional logger for diagnostics.</param>
        public RpcProcessor(ILogger<RpcProcessor>? logger = null)
        {
            _logger = logger ?? NullLogger<RpcProcessor>.Instance;
        }

        /// <summary>
        /// Registers an RPC method handler.
        /// </summary>
        /// <param name="method">The method handler to register.</param>
        /// <returns>True if the method was registered; false if it already exists.</returns>
        public bool RegisterMethod(IRpcMethod method)
        {
            ArgumentNullException.ThrowIfNull(method);
            return _methods.TryAdd(method.Name, method);
        }

        /// <summary>
        /// Unregisters an RPC method handler.
        /// </summary>
        /// <param name="methodName">The name of the method to unregister.</param>
        /// <returns>True if the method was unregistered; otherwise, false.</returns>
        public bool UnregisterMethod(string methodName)
        {
            return _methods.TryRemove(methodName, out _);
        }

        /// <summary>
        /// Checks if a method is registered.
        /// </summary>
        /// <param name="methodName">The name of the method.</param>
        /// <returns>True if the method is registered; otherwise, false.</returns>
        public bool HasMethod(string methodName)
        {
            return _methods.ContainsKey(methodName);
        }

        /// <summary>
        /// Processes a JSON-RPC request string.
        /// </summary>
        /// <param name="requestJson">The JSON request string.</param>
        /// <returns>The JSON response string.</returns>
        public async Task<string> ProcessAsync(string requestJson)
        {
            try
            {
                var json = JToken.Parse(requestJson);

                if (json is JArray array)
                {
                    // Batch request
                    var responses = new JArray();
                    foreach (var item in array)
                    {
                        if (item is JObject obj)
                        {
                            var response = await ProcessRequestAsync(obj);
                            responses.Add(response.ToJson());
                        }
                    }
                    return responses.ToString();
                }
                else if (json is JObject obj)
                {
                    // Single request
                    var response = await ProcessRequestAsync(obj);
                    return response.ToJson().ToString();
                }
                else
                {
                    return RpcResponse.Failure(null, RpcError.InvalidRequest).ToJson().ToString();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse JSON-RPC request: {Message}", ex.Message);
                return RpcResponse.Failure(null, RpcError.ParseError).ToJson().ToString();
            }
        }

        /// <summary>
        /// Processes a single JSON-RPC request.
        /// </summary>
        /// <param name="request">The request to process.</param>
        /// <returns>The response.</returns>
        public async Task<RpcResponse> ProcessRequestAsync(RpcRequest request)
        {
            if (string.IsNullOrEmpty(request.Method))
            {
                return RpcResponse.Failure(request.Id, RpcError.InvalidRequest);
            }

            if (!_methods.TryGetValue(request.Method, out var method))
            {
                return RpcResponse.Failure(request.Id, RpcError.MethodNotFound);
            }

            try
            {
                var result = await method.ProcessAsync(request.Params);
                return RpcResponse.Success(request.Id, result);
            }
            catch (RpcException ex)
            {
                return RpcResponse.Failure(request.Id, new RpcError
                {
                    Code = ex.Code,
                    Message = ex.Message,
                    Data = ex.Data
                });
            }
            catch (Exception ex)
            {
                return RpcResponse.Failure(request.Id, new RpcError
                {
                    Code = -32603,
                    Message = "Internal error",
                    Data = ex.Message
                });
            }
        }

        private async Task<RpcResponse> ProcessRequestAsync(JObject json)
        {
            var request = RpcRequest.FromJson(json);
            return await ProcessRequestAsync(request);
        }
    }

    /// <summary>
    /// Exception thrown by RPC method handlers.
    /// </summary>
    public class RpcException : Exception
    {
        /// <summary>
        /// Gets the error code.
        /// </summary>
        public int Code { get; }

        /// <summary>
        /// Gets additional error data.
        /// </summary>
        public new JToken? Data { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="RpcException"/> class.
        /// </summary>
        public RpcException(int code, string message, JToken? data = null)
            : base(message)
        {
            Code = code;
            Data = data;
        }
    }
}
