// Copyright (C) 2015-2025 The Neo Project.
//
// RpcResponse.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Json;

namespace Neo.RPC
{
    /// <summary>
    /// Represents a JSON-RPC 2.0 response.
    /// </summary>
    public class RpcResponse
    {
        /// <summary>
        /// Gets or sets the JSON-RPC version. Must be "2.0".
        /// </summary>
        public string JsonRpc { get; set; } = "2.0";

        /// <summary>
        /// Gets or sets the request identifier.
        /// </summary>
        public object? Id { get; set; }

        /// <summary>
        /// Gets or sets the result of the method invocation.
        /// </summary>
        public JToken? Result { get; set; }

        /// <summary>
        /// Gets or sets the error if the method invocation failed.
        /// </summary>
        public RpcError? Error { get; set; }

        /// <summary>
        /// Creates a successful response.
        /// </summary>
        public static RpcResponse Success(object? id, JToken? result)
        {
            return new RpcResponse
            {
                Id = id,
                Result = result
            };
        }

        /// <summary>
        /// Creates an error response.
        /// </summary>
        public static RpcResponse Failure(object? id, RpcError error)
        {
            return new RpcResponse
            {
                Id = id,
                Error = error
            };
        }

        /// <summary>
        /// Converts this response to a JSON object.
        /// </summary>
        public JObject ToJson()
        {
            var json = new JObject
            {
                ["jsonrpc"] = JsonRpc
            };

            if (Id != null)
                json["id"] = Id.ToString();

            if (Error != null)
                json["error"] = Error.ToJson();
            else
                json["result"] = Result ?? JToken.Null;

            return json;
        }
    }

    /// <summary>
    /// Represents a JSON-RPC 2.0 error.
    /// </summary>
    public class RpcError
    {
        /// <summary>
        /// Gets or sets the error code.
        /// </summary>
        public int Code { get; set; }

        /// <summary>
        /// Gets or sets the error message.
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets additional error data.
        /// </summary>
        public JToken? Data { get; set; }

        /// <summary>
        /// Converts this error to a JSON object.
        /// </summary>
        public JObject ToJson()
        {
            var json = new JObject
            {
                ["code"] = Code,
                ["message"] = Message
            };

            if (Data != null)
                json["data"] = Data;

            return json;
        }

        // Standard JSON-RPC error codes
        public static RpcError ParseError => new() { Code = -32700, Message = "Parse error" };
        public static RpcError InvalidRequest => new() { Code = -32600, Message = "Invalid Request" };
        public static RpcError MethodNotFound => new() { Code = -32601, Message = "Method not found" };
        public static RpcError InvalidParams => new() { Code = -32602, Message = "Invalid params" };
        public static RpcError InternalError => new() { Code = -32603, Message = "Internal error" };
    }
}
