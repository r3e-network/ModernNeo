// Copyright (C) 2015-2025 The Neo Project.
//
// RpcTracingTests.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

#nullable enable

using System.Collections.Specialized;
using System.Diagnostics;

namespace Neo.RPC.Tests
{
    [TestClass]
    public class RpcTracingTests
    {
        private ActivityListener _listener = null!;
        private List<Activity> _capturedActivities = null!;

        [TestInitialize]
        public void Setup()
        {
            _capturedActivities = new List<Activity>();
            _listener = new ActivityListener
            {
                ShouldListenTo = source => source.Name == RpcTracing.ActivitySourceName,
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
                ActivityStarted = activity => _capturedActivities.Add(activity)
            };
            ActivitySource.AddActivityListener(_listener);
        }

        [TestCleanup]
        public void Cleanup()
        {
            _listener.Dispose();
        }

        #region ActivitySource Tests

        [TestMethod]
        public void ActivitySourceName_IsCorrect()
        {
            Assert.AreEqual("Neo.RPC", RpcTracing.ActivitySourceName);
        }

        [TestMethod]
        public void ActivitySource_IsNotNull()
        {
            Assert.IsNotNull(RpcTracing.ActivitySource);
        }

        #endregion

        #region StartServerSpan Tests

        [TestMethod]
        public void StartServerSpan_CreatesActivity()
        {
            using var activity = RpcTracing.StartServerSpan("getblockcount");

            Assert.IsNotNull(activity);
            Assert.AreEqual("rpc.getblockcount", activity.OperationName);
            Assert.AreEqual(ActivityKind.Server, activity.Kind);
        }

        [TestMethod]
        public void StartServerSpan_SetsRpcTags()
        {
            using var activity = RpcTracing.StartServerSpan("getblock");

            Assert.IsNotNull(activity);
            Assert.AreEqual("jsonrpc", activity.GetTagItem("rpc.system"));
            Assert.AreEqual("getblock", activity.GetTagItem("rpc.method"));
            Assert.AreEqual("neo", activity.GetTagItem("rpc.service"));
        }

        [TestMethod]
        public void StartServerSpan_WithHeaders_ExtractsTraceContext()
        {
            var headers = new NameValueCollection
            {
                ["traceparent"] = "00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01"
            };

            using var activity = RpcTracing.StartServerSpan("test", headers);

            Assert.IsNotNull(activity);
            // The activity should have the parent trace ID
            Assert.AreEqual("0af7651916cd43dd8448eb211c80319c", activity.TraceId.ToHexString());
        }

        [TestMethod]
        public void StartServerSpan_WithInvalidHeaders_CreatesNewTrace()
        {
            var headers = new NameValueCollection
            {
                ["traceparent"] = "invalid-format"
            };

            using var activity = RpcTracing.StartServerSpan("test", headers);

            Assert.IsNotNull(activity);
            // Should create a new trace, not fail
            Assert.AreNotEqual(default(ActivityTraceId), activity.TraceId);
        }

        [TestMethod]
        public void StartServerSpan_WithNullHeaders_CreatesNewTrace()
        {
            using var activity = RpcTracing.StartServerSpan("test", null);

            Assert.IsNotNull(activity);
            Assert.AreNotEqual(default(ActivityTraceId), activity.TraceId);
        }

        #endregion

        #region RecordRequestAttributes Tests

        [TestMethod]
        public void RecordRequestAttributes_SetsRequestId()
        {
            using var activity = RpcTracing.StartServerSpan("test");
            RpcTracing.RecordRequestAttributes(activity, 123, null);

            Assert.AreEqual("123", activity?.GetTagItem("rpc.request.id"));
        }

        [TestMethod]
        public void RecordRequestAttributes_SetsPeerIp()
        {
            using var activity = RpcTracing.StartServerSpan("test");
            RpcTracing.RecordRequestAttributes(activity, null, "192.168.1.100");

            Assert.AreEqual("192.168.1.100", activity?.GetTagItem("net.peer.ip"));
        }

        [TestMethod]
        public void RecordRequestAttributes_WithNullActivity_DoesNotThrow()
        {
            // Should not throw
            RpcTracing.RecordRequestAttributes(null, 123, "192.168.1.100");
        }

        [TestMethod]
        public void RecordRequestAttributes_WithNullValues_DoesNotSetTags()
        {
            using var activity = RpcTracing.StartServerSpan("test");
            RpcTracing.RecordRequestAttributes(activity, null, null);

            Assert.IsNull(activity?.GetTagItem("rpc.request.id"));
            Assert.IsNull(activity?.GetTagItem("net.peer.ip"));
        }

        #endregion

        #region RecordSuccess Tests

        [TestMethod]
        public void RecordSuccess_SetsOkStatus()
        {
            using var activity = RpcTracing.StartServerSpan("test");
            RpcTracing.RecordSuccess(activity);

            Assert.AreEqual(ActivityStatusCode.Ok, activity?.Status);
            Assert.AreEqual("success", activity?.GetTagItem("rpc.response.status"));
        }

        [TestMethod]
        public void RecordSuccess_WithNullActivity_DoesNotThrow()
        {
            // Should not throw
            RpcTracing.RecordSuccess(null);
        }

        #endregion

        #region RecordError Tests

        [TestMethod]
        public void RecordError_SetsErrorStatus()
        {
            using var activity = RpcTracing.StartServerSpan("test");
            RpcTracing.RecordError(activity, -32601, "Method not found");

            Assert.AreEqual(ActivityStatusCode.Error, activity?.Status);
            Assert.AreEqual("error", activity?.GetTagItem("rpc.response.status"));
            Assert.AreEqual(-32601, activity?.GetTagItem("rpc.error.code"));
            Assert.AreEqual("Method not found", activity?.GetTagItem("rpc.error.message"));
        }

        [TestMethod]
        public void RecordError_WithNullMessage_DoesNotSetMessageTag()
        {
            using var activity = RpcTracing.StartServerSpan("test");
            RpcTracing.RecordError(activity, -32600, null);

            Assert.AreEqual(-32600, activity?.GetTagItem("rpc.error.code"));
            Assert.IsNull(activity?.GetTagItem("rpc.error.message"));
        }

        [TestMethod]
        public void RecordError_WithNullActivity_DoesNotThrow()
        {
            // Should not throw
            RpcTracing.RecordError(null, -32600, "Error");
        }

        #endregion

        #region RecordException Tests

        [TestMethod]
        public void RecordException_SetsErrorStatusAndEvent()
        {
            using var activity = RpcTracing.StartServerSpan("test");
            var exception = new InvalidOperationException("Test exception");
            RpcTracing.RecordException(activity, exception);

            Assert.AreEqual(ActivityStatusCode.Error, activity?.Status);
            Assert.AreEqual("Test exception", activity?.StatusDescription);

            // Check that an exception event was added
            var events = activity?.Events.ToList();
            Assert.IsNotNull(events);
            Assert.AreEqual(1, events.Count);
            Assert.AreEqual("exception", events[0].Name);
        }

        [TestMethod]
        public void RecordException_WithNullActivity_DoesNotThrow()
        {
            var exception = new InvalidOperationException("Test");
            // Should not throw
            RpcTracing.RecordException(null, exception);
        }

        #endregion

        #region ProcessWithTracingAsync Tests

        [TestMethod]
        public async Task ProcessWithTracingAsync_CreatesSpanForRequest()
        {
            var processor = new RpcProcessor();
            processor.RegisterMethod(new TracingTestRpcMethod("getblockcount", result: "100"));

            var request = @"{""jsonrpc"":""2.0"",""id"":1,""method"":""getblockcount"",""params"":[]}";
            var response = await processor.ProcessWithTracingAsync(request);

            Assert.IsTrue(response.Contains("\"result\""));
            Assert.IsTrue(_capturedActivities.Any(a => a.OperationName == "rpc.getblockcount"));
        }

        [TestMethod]
        public async Task ProcessWithTracingAsync_WithHeaders_PropagatesContext()
        {
            var processor = new RpcProcessor();
            processor.RegisterMethod(new TracingTestRpcMethod("test", result: "ok"));

            var headers = new NameValueCollection
            {
                ["traceparent"] = "00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01"
            };

            var request = @"{""jsonrpc"":""2.0"",""id"":1,""method"":""test"",""params"":[]}";
            await processor.ProcessWithTracingAsync(request, headers);

            var activity = _capturedActivities.FirstOrDefault(a => a.OperationName == "rpc.test");
            Assert.IsNotNull(activity);
            Assert.AreEqual("0af7651916cd43dd8448eb211c80319c", activity.TraceId.ToHexString());
        }

        [TestMethod]
        public async Task ProcessWithTracingAsync_WithRemoteAddress_RecordsAttribute()
        {
            var processor = new RpcProcessor();
            processor.RegisterMethod(new TracingTestRpcMethod("test", result: "ok"));

            var request = @"{""jsonrpc"":""2.0"",""id"":1,""method"":""test"",""params"":[]}";
            await processor.ProcessWithTracingAsync(request, null, "10.0.0.1");

            var activity = _capturedActivities.FirstOrDefault(a => a.OperationName == "rpc.test");
            Assert.IsNotNull(activity);
            Assert.AreEqual("10.0.0.1", activity.GetTagItem("net.peer.ip"));
        }

        [TestMethod]
        public async Task ProcessWithTracingAsync_ErrorResponse_RecordsError()
        {
            var processor = new RpcProcessor();
            // No method registered, will return error

            var request = @"{""jsonrpc"":""2.0"",""id"":1,""method"":""nonexistent"",""params"":[]}";
            var response = await processor.ProcessWithTracingAsync(request);

            Assert.IsTrue(response.Contains("\"error\""));
            // Activity should still be created
            Assert.IsTrue(_capturedActivities.Any(a => a.OperationName == "rpc.nonexistent"));
        }

        [TestMethod]
        public async Task ProcessWithTracingAsync_InvalidJson_UsesUnknownMethod()
        {
            var processor = new RpcProcessor();

            var request = "not valid json";
            await processor.ProcessWithTracingAsync(request);

            // Should use "unknown" as method name when parsing fails
            Assert.IsTrue(_capturedActivities.Any(a => a.OperationName == "rpc.unknown"));
        }

        #endregion
    }

    /// <summary>
    /// Test implementation of IRpcMethod for tracing tests.
    /// </summary>
    internal class TracingTestRpcMethod : IRpcMethod
    {
        private readonly string? _result;
        private readonly bool _throwsException;

        public string Name { get; }

        public TracingTestRpcMethod(string name, string? result = null, bool throwsException = false)
        {
            Name = name;
            _result = result;
            _throwsException = throwsException;
        }

        public Task<Neo.Json.JToken?> ProcessAsync(Neo.Json.JArray? parameters)
        {
            if (_throwsException)
                throw new InvalidOperationException("Test exception");

            return Task.FromResult<Neo.Json.JToken?>(_result);
        }
    }
}
