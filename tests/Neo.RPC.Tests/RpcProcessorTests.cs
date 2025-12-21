// Copyright (C) 2015-2025 The Neo Project.
//
// RpcProcessorTests.cs file belongs to the neo project and is free
// software distributed under the MIT software license.

using Neo.Json;

namespace Neo.RPC.Tests;

[TestClass]
public class RpcProcessorTests
{
    private RpcProcessor _processor = null!;

    [TestInitialize]
    public void Setup()
    {
        _processor = new RpcProcessor();
    }

    [TestMethod]
    public void RegisterMethod_NewMethod_ReturnsTrue()
    {
        var method = new TestRpcMethod("test");
        var result = _processor.RegisterMethod(method);
        Assert.IsTrue(result);
        Assert.AreEqual(1, _processor.MethodCount);
    }

    [TestMethod]
    public void RegisterMethod_DuplicateMethod_ReturnsFalse()
    {
        var method1 = new TestRpcMethod("test");
        var method2 = new TestRpcMethod("test");
        _processor.RegisterMethod(method1);
        var result = _processor.RegisterMethod(method2);
        Assert.IsFalse(result);
        Assert.AreEqual(1, _processor.MethodCount);
    }

    [TestMethod]
    public void UnregisterMethod_ExistingMethod_ReturnsTrue()
    {
        var method = new TestRpcMethod("test");
        _processor.RegisterMethod(method);
        var result = _processor.UnregisterMethod("test");
        Assert.IsTrue(result);
        Assert.AreEqual(0, _processor.MethodCount);
    }

    [TestMethod]
    public void UnregisterMethod_NonExistingMethod_ReturnsFalse()
    {
        var result = _processor.UnregisterMethod("nonexistent");
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void HasMethod_RegisteredMethod_ReturnsTrue()
    {
        var method = new TestRpcMethod("test");
        _processor.RegisterMethod(method);
        Assert.IsTrue(_processor.HasMethod("test"));
    }

    [TestMethod]
    public void HasMethod_UnregisteredMethod_ReturnsFalse()
    {
        Assert.IsFalse(_processor.HasMethod("nonexistent"));
    }

    [TestMethod]
    public async Task ProcessAsync_ValidRequest_ReturnsSuccessResponse()
    {
        var method = new TestRpcMethod("echo", result: "hello");
        _processor.RegisterMethod(method);

        var request = @"{""jsonrpc"":""2.0"",""id"":1,""method"":""echo"",""params"":[]}";
        var response = await _processor.ProcessAsync(request);

        Assert.IsTrue(response.Contains("\"result\""));
        Assert.IsTrue(response.Contains("hello"));
    }

    [TestMethod]
    public async Task ProcessAsync_MethodNotFound_ReturnsError()
    {
        var request = @"{""jsonrpc"":""2.0"",""id"":1,""method"":""nonexistent"",""params"":[]}";
        var response = await _processor.ProcessAsync(request);

        Assert.IsTrue(response.Contains("\"error\""));
        Assert.IsTrue(response.Contains("-32601"));
    }

    [TestMethod]
    public async Task ProcessAsync_InvalidJson_ReturnsParseError()
    {
        var request = "invalid json";
        var response = await _processor.ProcessAsync(request);

        Assert.IsTrue(response.Contains("\"error\""));
        Assert.IsTrue(response.Contains("-32700"));
    }

    [TestMethod]
    public async Task ProcessAsync_BatchRequest_ReturnsMultipleResponses()
    {
        var method = new TestRpcMethod("test", result: "ok");
        _processor.RegisterMethod(method);

        var request = @"[{""jsonrpc"":""2.0"",""id"":1,""method"":""test""},{""jsonrpc"":""2.0"",""id"":2,""method"":""test""}]";
        var response = await _processor.ProcessAsync(request);

        var json = JToken.Parse(response) as JArray;
        Assert.IsNotNull(json);
        Assert.AreEqual(2, json.Count);
    }

    [TestMethod]
    public async Task ProcessRequestAsync_MethodThrowsRpcException_ReturnsCustomError()
    {
        var method = new TestRpcMethod("error", throwsRpcException: true);
        _processor.RegisterMethod(method);

        var request = new RpcRequest { Id = 1, Method = "error" };
        var response = await _processor.ProcessRequestAsync(request);

        Assert.IsNotNull(response.Error);
        Assert.AreEqual(-100, response.Error.Code);
    }

    [TestMethod]
    public async Task ProcessRequestAsync_MethodThrowsException_ReturnsInternalError()
    {
        var method = new TestRpcMethod("crash", throwsException: true);
        _processor.RegisterMethod(method);

        var request = new RpcRequest { Id = 1, Method = "crash" };
        var response = await _processor.ProcessRequestAsync(request);

        Assert.IsNotNull(response.Error);
        Assert.AreEqual(-32603, response.Error.Code);
    }

    [TestMethod]
    public async Task ProcessRequestAsync_EmptyMethod_ReturnsInvalidRequest()
    {
        var request = new RpcRequest { Id = 1, Method = "" };
        var response = await _processor.ProcessRequestAsync(request);

        Assert.IsNotNull(response.Error);
        Assert.AreEqual(-32600, response.Error.Code);
    }

    [TestMethod]
    public void RegisteredMethods_ReturnsAllMethodNames()
    {
        _processor.RegisterMethod(new TestRpcMethod("method1"));
        _processor.RegisterMethod(new TestRpcMethod("method2"));
        _processor.RegisterMethod(new TestRpcMethod("method3"));

        var methods = _processor.RegisteredMethods.ToList();
        Assert.AreEqual(3, methods.Count);
        Assert.IsTrue(methods.Contains("method1"));
        Assert.IsTrue(methods.Contains("method2"));
        Assert.IsTrue(methods.Contains("method3"));
    }
}

/// <summary>
/// Test implementation of IRpcMethod.
/// </summary>
internal class TestRpcMethod : IRpcMethod
{
    private readonly string? _result;
    private readonly bool _throwsRpcException;
    private readonly bool _throwsException;

    public string Name { get; }

    public TestRpcMethod(string name, string? result = null, bool throwsRpcException = false, bool throwsException = false)
    {
        Name = name;
        _result = result;
        _throwsRpcException = throwsRpcException;
        _throwsException = throwsException;
    }

    public Task<JToken?> ProcessAsync(JArray? parameters)
    {
        if (_throwsRpcException)
            throw new RpcException(-100, "Custom RPC error");

        if (_throwsException)
            throw new InvalidOperationException("Test exception");

        return Task.FromResult<JToken?>(_result);
    }
}
