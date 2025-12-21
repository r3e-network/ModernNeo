// Copyright (C) 2015-2025 The Neo Project.
//
// RpcRequest.cs file belongs to the neo project and is free
// software distributed under the MIT software license.

using Neo.Json;

namespace Neo.RPC;

/// <summary>
/// Represents a JSON-RPC 2.0 request.
/// </summary>
public class RpcRequest
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
    /// Gets or sets the method name to invoke.
    /// </summary>
    public string Method { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the method parameters.
    /// </summary>
    public JArray? Params { get; set; }

    /// <summary>
    /// Creates an RpcRequest from a JSON object.
    /// </summary>
    public static RpcRequest FromJson(JObject json)
    {
        return new RpcRequest
        {
            JsonRpc = json["jsonrpc"]?.AsString() ?? "2.0",
            Id = json["id"]?.AsString(),
            Method = json["method"]?.AsString() ?? string.Empty,
            Params = json["params"] as JArray
        };
    }

    /// <summary>
    /// Converts this request to a JSON object.
    /// </summary>
    public JObject ToJson()
    {
        var json = new JObject
        {
            ["jsonrpc"] = JsonRpc,
            ["method"] = Method
        };

        if (Id != null)
            json["id"] = Id.ToString();

        if (Params != null)
            json["params"] = Params;

        return json;
    }
}
