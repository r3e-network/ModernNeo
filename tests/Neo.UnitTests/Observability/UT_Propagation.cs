// Copyright (C) 2015-2025 The Neo Project.
//
// UT_Propagation.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

#nullable enable

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.Observability.Tracing;
using Neo.Observability.Tracing.Propagation;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Neo.UnitTests.Observability
{
    [TestClass]
    public class UT_Propagation
    {
        #region TraceContext Tests

        [TestMethod]
        public void TraceContext_Empty_IsNotValid()
        {
            var context = TraceContext.Empty;
            Assert.IsFalse(context.IsValid);
        }

        [TestMethod]
        public void TraceContext_FromActivityContext_IsValid()
        {
            var traceId = ActivityTraceId.CreateRandom();
            var spanId = ActivitySpanId.CreateRandom();
            var activityContext = new ActivityContext(traceId, spanId, ActivityTraceFlags.Recorded);

            var context = new TraceContext(activityContext);

            Assert.IsTrue(context.IsValid);
            Assert.AreEqual(traceId.ToHexString(), context.TraceId);
            Assert.AreEqual(spanId.ToHexString(), context.SpanId);
            Assert.IsTrue(context.IsSampled);
        }

        [TestMethod]
        public void TraceContext_FromStrings_IsValid()
        {
            var traceId = "0af7651916cd43dd8448eb211c80319c";
            var spanId = "b7ad6b7169203331";

            var context = new TraceContext(traceId, spanId, 1);

            Assert.IsTrue(context.IsValid);
            Assert.AreEqual(traceId, context.TraceId);
            Assert.AreEqual(spanId, context.SpanId);
            Assert.IsTrue(context.IsSampled);
        }

        [TestMethod]
        public void TraceContext_FromStrings_InvalidTraceId_Throws()
        {
            Assert.ThrowsExactly<ArgumentException>(() =>
                new TraceContext("invalid", "b7ad6b7169203331"));
        }

        [TestMethod]
        public void TraceContext_FromStrings_InvalidSpanId_Throws()
        {
            Assert.ThrowsExactly<ArgumentException>(() =>
                new TraceContext("0af7651916cd43dd8448eb211c80319c", "invalid"));
        }

        [TestMethod]
        public void TraceContext_TryParse_ValidInput_ReturnsTrue()
        {
            var traceId = "0af7651916cd43dd8448eb211c80319c";
            var spanId = "b7ad6b7169203331";

            var result = TraceContext.TryParse(traceId, spanId, 1, null, out var context);

            Assert.IsTrue(result);
            Assert.IsTrue(context.IsValid);
            Assert.AreEqual(traceId, context.TraceId);
        }

        [TestMethod]
        public void TraceContext_TryParse_InvalidInput_ReturnsFalse()
        {
            var result = TraceContext.TryParse("invalid", "invalid", 0, null, out var context);

            Assert.IsFalse(result);
            Assert.IsFalse(context.IsValid);
        }

        [TestMethod]
        public void TraceContext_TryParse_NullInput_ReturnsFalse()
        {
            var result = TraceContext.TryParse(null, null, 0, null, out var context);

            Assert.IsFalse(result);
            Assert.IsFalse(context.IsValid);
        }

        [TestMethod]
        public void TraceContext_Equality_SameContext_AreEqual()
        {
            var traceId = "0af7651916cd43dd8448eb211c80319c";
            var spanId = "b7ad6b7169203331";

            var context1 = new TraceContext(traceId, spanId);
            var context2 = new TraceContext(traceId, spanId);

            Assert.AreEqual(context1, context2);
            Assert.IsTrue(context1 == context2);
            Assert.IsFalse(context1 != context2);
        }

        [TestMethod]
        public void TraceContext_Equality_DifferentContext_AreNotEqual()
        {
            var context1 = new TraceContext("0af7651916cd43dd8448eb211c80319c", "b7ad6b7169203331");
            var context2 = new TraceContext("1af7651916cd43dd8448eb211c80319c", "c7ad6b7169203331");

            Assert.AreNotEqual(context1, context2);
            Assert.IsFalse(context1 == context2);
            Assert.IsTrue(context1 != context2);
        }

        [TestMethod]
        public void TraceContext_Current_WithActivity_ReturnsContext()
        {
            using var activitySource = new ActivitySource("Test.Current");
            using var listener = new ActivityListener
            {
                ShouldListenTo = source => source.Name == "Test.Current",
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData
            };
            ActivitySource.AddActivityListener(listener);

            using var activity = activitySource.StartActivity("TestOp");
            Assert.IsNotNull(activity);

            var context = TraceContext.Current;
            Assert.IsTrue(context.IsValid);
            Assert.AreEqual(activity.TraceId.ToHexString(), context.TraceId);
        }

        [TestMethod]
        public void TraceContext_Current_WithoutActivity_ReturnsEmpty()
        {
            // Ensure no activity is current
            Activity.Current = null;

            var context = TraceContext.Current;
            Assert.IsFalse(context.IsValid);
        }

        #endregion

        #region W3CTraceContextPropagator Tests

        [TestMethod]
        public void W3CPropagator_Fields_ContainsExpectedHeaders()
        {
            var propagator = W3CTraceContextPropagator.Instance;

            Assert.IsTrue(propagator.Fields.Contains("traceparent"));
            Assert.IsTrue(propagator.Fields.Contains("tracestate"));
        }

        [TestMethod]
        public void W3CPropagator_Extract_ValidTraceparent_ReturnsContext()
        {
            var propagator = W3CTraceContextPropagator.Instance;
            var headers = new Dictionary<string, string>
            {
                ["traceparent"] = "00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01"
            };

            var context = propagator.Extract(headers, (h, key) => h.TryGetValue(key, out var value) ? value : null);

            Assert.IsTrue(context.IsValid);
            Assert.AreEqual("0af7651916cd43dd8448eb211c80319c", context.TraceId);
            Assert.AreEqual("b7ad6b7169203331", context.SpanId);
            Assert.IsTrue(context.IsSampled);
        }

        [TestMethod]
        public void W3CPropagator_Extract_WithTracestate_ReturnsContextWithState()
        {
            var propagator = W3CTraceContextPropagator.Instance;
            var headers = new Dictionary<string, string>
            {
                ["traceparent"] = "00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01",
                ["tracestate"] = "neo=test123"
            };

            var context = propagator.Extract(headers, (h, key) => h.TryGetValue(key, out var value) ? value : null);

            Assert.IsTrue(context.IsValid);
            Assert.AreEqual("neo=test123", context.TraceState);
        }

        [TestMethod]
        public void W3CPropagator_Extract_InvalidTraceparent_ReturnsEmpty()
        {
            var propagator = W3CTraceContextPropagator.Instance;
            var headers = new Dictionary<string, string>
            {
                ["traceparent"] = "invalid-format"
            };

            var context = propagator.Extract(headers, (h, key) => h.TryGetValue(key, out var value) ? value : null);

            Assert.IsFalse(context.IsValid);
        }

        [TestMethod]
        public void W3CPropagator_Extract_MissingTraceparent_ReturnsEmpty()
        {
            var propagator = W3CTraceContextPropagator.Instance;
            var headers = new Dictionary<string, string>();

            var context = propagator.Extract(headers, (h, key) => h.TryGetValue(key, out var value) ? value : null);

            Assert.IsFalse(context.IsValid);
        }

        [TestMethod]
        public void W3CPropagator_Inject_ValidContext_SetsHeaders()
        {
            var propagator = W3CTraceContextPropagator.Instance;
            var context = new TraceContext("0af7651916cd43dd8448eb211c80319c", "b7ad6b7169203331", 1);
            var headers = new Dictionary<string, string>();

            propagator.Inject(context, headers, (h, key, value) => h[key] = value);

            Assert.IsTrue(headers.ContainsKey("traceparent"));
            Assert.AreEqual("00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01", headers["traceparent"]);
        }

        [TestMethod]
        public void W3CPropagator_Inject_WithTracestate_SetsBothHeaders()
        {
            var propagator = W3CTraceContextPropagator.Instance;
            var activityContext = new ActivityContext(
                ActivityTraceId.CreateFromString("0af7651916cd43dd8448eb211c80319c".AsSpan()),
                ActivitySpanId.CreateFromString("b7ad6b7169203331".AsSpan()),
                ActivityTraceFlags.Recorded,
                "neo=test123");
            var context = new TraceContext(activityContext);
            var headers = new Dictionary<string, string>();

            propagator.Inject(context, headers, (h, key, value) => h[key] = value);

            Assert.IsTrue(headers.ContainsKey("traceparent"));
            Assert.IsTrue(headers.ContainsKey("tracestate"));
            Assert.AreEqual("neo=test123", headers["tracestate"]);
        }

        [TestMethod]
        public void W3CPropagator_Inject_EmptyContext_DoesNotSetHeaders()
        {
            var propagator = W3CTraceContextPropagator.Instance;
            var headers = new Dictionary<string, string>();

            propagator.Inject(TraceContext.Empty, headers, (h, key, value) => h[key] = value);

            Assert.IsFalse(headers.ContainsKey("traceparent"));
        }

        [TestMethod]
        public void W3CPropagator_RoundTrip_PreservesContext()
        {
            var propagator = W3CTraceContextPropagator.Instance;
            var original = new TraceContext("0af7651916cd43dd8448eb211c80319c", "b7ad6b7169203331", 1);
            var headers = new Dictionary<string, string>();

            propagator.Inject(original, headers, (h, key, value) => h[key] = value);
            var extracted = propagator.Extract(headers, (h, key) => h.TryGetValue(key, out var value) ? value : null);

            Assert.AreEqual(original.TraceId, extracted.TraceId);
            Assert.AreEqual(original.SpanId, extracted.SpanId);
            Assert.AreEqual(original.IsSampled, extracted.IsSampled);
        }

        [TestMethod]
        public void W3CPropagator_TryParseTraceParent_ValidFormat_ReturnsTrue()
        {
            var result = W3CTraceContextPropagator.TryParseTraceParent(
                "00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01",
                out var traceId, out var spanId, out var flags);

            Assert.IsTrue(result);
            Assert.AreEqual("0af7651916cd43dd8448eb211c80319c", traceId);
            Assert.AreEqual("b7ad6b7169203331", spanId);
            Assert.AreEqual(1, flags);
        }

        [TestMethod]
        public void W3CPropagator_TryParseTraceParent_AllZeroTraceId_ReturnsFalse()
        {
            var result = W3CTraceContextPropagator.TryParseTraceParent(
                "00-00000000000000000000000000000000-b7ad6b7169203331-01",
                out _, out _, out _);

            Assert.IsFalse(result);
        }

        [TestMethod]
        public void W3CPropagator_TryParseTraceParent_AllZeroSpanId_ReturnsFalse()
        {
            var result = W3CTraceContextPropagator.TryParseTraceParent(
                "00-0af7651916cd43dd8448eb211c80319c-0000000000000000-01",
                out _, out _, out _);

            Assert.IsFalse(result);
        }

        #endregion

        #region SpanLink Tests

        [TestMethod]
        public void SpanLink_FromContext_HasValidContext()
        {
            var context = new TraceContext("0af7651916cd43dd8448eb211c80319c", "b7ad6b7169203331");
            var link = new SpanLink(context);

            Assert.IsTrue(link.Context.IsValid);
            Assert.IsNull(link.Attributes);
        }

        [TestMethod]
        public void SpanLink_WithAttributes_HasAttributes()
        {
            var context = new TraceContext("0af7651916cd43dd8448eb211c80319c", "b7ad6b7169203331");
            var attributes = new Dictionary<string, object?> { ["key"] = "value" };
            var link = new SpanLink(context, attributes);

            Assert.IsNotNull(link.Attributes);
            Assert.AreEqual("value", link.Attributes["key"]);
        }

        [TestMethod]
        public void SpanLink_ForTransaction_HasTxHashAttribute()
        {
            var txHash = "0x1234567890abcdef1234567890abcdef1234567890abcdef1234567890abcdef";
            var link = SpanLink.ForTransaction(txHash);

            Assert.IsNotNull(link.Attributes);
            Assert.AreEqual(txHash, link.Attributes["neo.tx.hash"]);
        }

        [TestMethod]
        public void SpanLink_ForBlock_HasBlockAttributes()
        {
            var blockHash = "0xabcdef1234567890abcdef1234567890abcdef1234567890abcdef1234567890";
            var blockIndex = 12345u;
            var link = SpanLink.ForBlock(blockHash, blockIndex);

            Assert.IsNotNull(link.Attributes);
            Assert.AreEqual(blockHash, link.Attributes["neo.block.hash"]);
            Assert.AreEqual(blockIndex, link.Attributes["neo.block.index"]);
        }

        [TestMethod]
        public void SpanLink_ForInventory_HasInventoryAttributes()
        {
            var link = SpanLink.ForInventory("TX", "0x1234");

            Assert.IsNotNull(link.Attributes);
            Assert.AreEqual("TX", link.Attributes["neo.inventory.type"]);
            Assert.AreEqual("0x1234", link.Attributes["neo.inventory.hash"]);
        }

        [TestMethod]
        public void SpanLink_ToActivityLink_ConvertsCorrectly()
        {
            var context = new TraceContext("0af7651916cd43dd8448eb211c80319c", "b7ad6b7169203331");
            var link = new SpanLink(context);

            var activityLink = link.ToActivityLink();

            Assert.AreEqual(context.ActivityContext.TraceId, activityLink.Context.TraceId);
            Assert.AreEqual(context.ActivityContext.SpanId, activityLink.Context.SpanId);
        }

        #endregion

        #region Extended ITracer Tests

        [TestMethod]
        public void OpenTelemetryTracer_StartSpanWithParent_CreatesChildSpan()
        {
            using var activitySource = new ActivitySource("Test.Parent");
            using var listener = new ActivityListener
            {
                ShouldListenTo = source => source.Name == "Test.Parent",
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData
            };
            ActivitySource.AddActivityListener(listener);

            var tracer = new OpenTelemetryTracer(activitySource);
            var parentContext = new TraceContext("0af7651916cd43dd8448eb211c80319c", "b7ad6b7169203331", 1);

            using var span = tracer.StartSpan("ChildOp", SpanKind.Internal, parentContext);

            Assert.IsNotNull(span);
            Assert.AreNotEqual(string.Empty, span.Name);
        }

        [TestMethod]
        public void OpenTelemetryTracer_StartSpanWithLinks_CreatesSpanWithLinks()
        {
            using var activitySource = new ActivitySource("Test.Links");
            using var listener = new ActivityListener
            {
                ShouldListenTo = source => source.Name == "Test.Links",
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData
            };
            ActivitySource.AddActivityListener(listener);

            var tracer = new OpenTelemetryTracer(activitySource);
            var links = new[]
            {
                SpanLink.ForTransaction("0x1234"),
                SpanLink.ForBlock("0xabcd", 100)
            };

            using var span = tracer.StartSpan("LinkedOp", SpanKind.Consumer, links);

            Assert.IsNotNull(span);
        }

        [TestMethod]
        public void OpenTelemetryTracer_GetCurrentContext_ReturnsCurrentActivity()
        {
            using var activitySource = new ActivitySource("Test.GetCurrent");
            using var listener = new ActivityListener
            {
                ShouldListenTo = source => source.Name == "Test.GetCurrent",
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData
            };
            ActivitySource.AddActivityListener(listener);

            var tracer = new OpenTelemetryTracer(activitySource);

            using var span = tracer.StartSpan("TestOp");
            var context = tracer.GetCurrentContext();

            Assert.IsTrue(context.IsValid);
        }

        [TestMethod]
        public void NullTracer_StartSpanWithParent_ReturnsNullSpan()
        {
            var tracer = NullTracer.Instance;
            var parentContext = new TraceContext("0af7651916cd43dd8448eb211c80319c", "b7ad6b7169203331");

            using var span = tracer.StartSpan("Test", SpanKind.Internal, parentContext);

            Assert.AreSame(NullSpan.Instance, span);
        }

        [TestMethod]
        public void NullTracer_StartSpanWithLinks_ReturnsNullSpan()
        {
            var tracer = NullTracer.Instance;
            var links = new[] { SpanLink.ForTransaction("0x1234") };

            using var span = tracer.StartSpan("Test", SpanKind.Consumer, links);

            Assert.AreSame(NullSpan.Instance, span);
        }

        [TestMethod]
        public void NullTracer_GetCurrentContext_ReturnsEmpty()
        {
            var tracer = NullTracer.Instance;

            var context = tracer.GetCurrentContext();

            Assert.IsFalse(context.IsValid);
        }

        #endregion
    }
}
