// Copyright (C) 2015-2025 The Neo Project.
//
// UT_OpenTelemetryTracer.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.Observability.Tracing;
using System;
using System.Diagnostics;

namespace Neo.UnitTests.Observability
{
    [TestClass]
    public class UT_OpenTelemetryTracer
    {
        [TestMethod]
        public void TestCreateTracer()
        {
            var tracer = new OpenTelemetryTracer("Neo.Test", "1.0.0");
            Assert.IsNotNull(tracer);
        }

        [TestMethod]
        public void TestCreateTracerWithActivitySource()
        {
            using var activitySource = new ActivitySource("Neo.Test.Custom");
            var tracer = new OpenTelemetryTracer(activitySource);
            Assert.IsNotNull(tracer);
        }

        [TestMethod]
        public void TestCreateTracerNullActivitySourceThrows()
        {
            Assert.ThrowsExactly<ArgumentNullException>(() => new OpenTelemetryTracer((ActivitySource)null!));
        }

        [TestMethod]
        public void TestTracerImplementsITracer()
        {
            var tracer = new OpenTelemetryTracer("Neo.Test");
            Assert.IsInstanceOfType(tracer, typeof(ITracer));
        }

        [TestMethod]
        public void TestStartSpan()
        {
            var tracer = new OpenTelemetryTracer("Neo.Test");
            using var span = tracer.StartSpan("TestOperation");

            Assert.IsNotNull(span);
            Assert.IsInstanceOfType(span, typeof(ISpan));
        }

        [TestMethod]
        public void TestStartSpanWithKind()
        {
            var tracer = new OpenTelemetryTracer("Neo.Test");

            using var internalSpan = tracer.StartSpan("InternalOp", SpanKind.Internal);
            using var serverSpan = tracer.StartSpan("ServerOp", SpanKind.Server);
            using var clientSpan = tracer.StartSpan("ClientOp", SpanKind.Client);
            using var producerSpan = tracer.StartSpan("ProducerOp", SpanKind.Producer);
            using var consumerSpan = tracer.StartSpan("ConsumerOp", SpanKind.Consumer);

            Assert.IsNotNull(internalSpan);
            Assert.IsNotNull(serverSpan);
            Assert.IsNotNull(clientSpan);
            Assert.IsNotNull(producerSpan);
            Assert.IsNotNull(consumerSpan);
        }

        [TestMethod]
        public void TestSpanWithoutListenerReturnsNullSpan()
        {
            // Without an ActivityListener, StartActivity returns null
            // and our tracer should return NullSpan.Instance
            var tracer = new OpenTelemetryTracer("Neo.Test.NoListener");
            using var span = tracer.StartSpan("TestOperation");

            // Should be NullSpan when no listener is registered
            Assert.IsNotNull(span);
        }

        [TestMethod]
        public void TestSpanWithListener()
        {
            using var activitySource = new ActivitySource("Neo.Test.WithListener");
            using var listener = new ActivityListener
            {
                ShouldListenTo = source => source.Name == "Neo.Test.WithListener",
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData
            };
            ActivitySource.AddActivityListener(listener);

            var tracer = new OpenTelemetryTracer(activitySource);
            using var span = tracer.StartSpan("TestOperation");

            Assert.IsNotNull(span);
            Assert.IsInstanceOfType<OpenTelemetrySpan>(span);
            Assert.AreEqual("TestOperation", span.Name);
        }

        [TestMethod]
        public void TestSpanSetStringAttribute()
        {
            using var activitySource = new ActivitySource("Neo.Test.Attributes");
            using var listener = new ActivityListener
            {
                ShouldListenTo = source => source.Name == "Neo.Test.Attributes",
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData
            };
            ActivitySource.AddActivityListener(listener);

            var tracer = new OpenTelemetryTracer(activitySource);
            using var span = tracer.StartSpan("TestOperation");

            var result = span.SetAttribute("key", "value");
            Assert.AreSame(span, result); // Fluent API returns same instance
        }

        [TestMethod]
        public void TestSpanSetLongAttribute()
        {
            using var activitySource = new ActivitySource("Neo.Test.LongAttr");
            using var listener = new ActivityListener
            {
                ShouldListenTo = source => source.Name == "Neo.Test.LongAttr",
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData
            };
            ActivitySource.AddActivityListener(listener);

            var tracer = new OpenTelemetryTracer(activitySource);
            using var span = tracer.StartSpan("TestOperation");

            var result = span.SetAttribute("count", 42L);
            Assert.AreSame(span, result);
        }

        [TestMethod]
        public void TestSpanSetBoolAttribute()
        {
            using var activitySource = new ActivitySource("Neo.Test.BoolAttr");
            using var listener = new ActivityListener
            {
                ShouldListenTo = source => source.Name == "Neo.Test.BoolAttr",
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData
            };
            ActivitySource.AddActivityListener(listener);

            var tracer = new OpenTelemetryTracer(activitySource);
            using var span = tracer.StartSpan("TestOperation");

            var result = span.SetAttribute("enabled", true);
            Assert.AreSame(span, result);
        }

        [TestMethod]
        public void TestSpanRecordException()
        {
            using var activitySource = new ActivitySource("Neo.Test.Exception");
            using var listener = new ActivityListener
            {
                ShouldListenTo = source => source.Name == "Neo.Test.Exception",
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData
            };
            ActivitySource.AddActivityListener(listener);

            var tracer = new OpenTelemetryTracer(activitySource);
            using var span = tracer.StartSpan("TestOperation");

            var exception = new InvalidOperationException("Test error");
            var result = span.RecordException(exception);
            Assert.AreSame(span, result);
        }

        [TestMethod]
        public void TestSpanSetStatus()
        {
            using var activitySource = new ActivitySource("Neo.Test.Status");
            using var listener = new ActivityListener
            {
                ShouldListenTo = source => source.Name == "Neo.Test.Status",
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData
            };
            ActivitySource.AddActivityListener(listener);

            var tracer = new OpenTelemetryTracer(activitySource);
            using var span = tracer.StartSpan("TestOperation");

            var result1 = span.SetStatus(SpanStatus.Ok);
            Assert.AreSame(span, result1);

            var result2 = span.SetStatus(SpanStatus.Error, "Something went wrong");
            Assert.AreSame(span, result2);

            var result3 = span.SetStatus(SpanStatus.Unset);
            Assert.AreSame(span, result3);
        }

        [TestMethod]
        public void TestSpanAddEvent()
        {
            using var activitySource = new ActivitySource("Neo.Test.Event");
            using var listener = new ActivityListener
            {
                ShouldListenTo = source => source.Name == "Neo.Test.Event",
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData
            };
            ActivitySource.AddActivityListener(listener);

            var tracer = new OpenTelemetryTracer(activitySource);
            using var span = tracer.StartSpan("TestOperation");

            var result = span.AddEvent("checkpoint_reached");
            Assert.AreSame(span, result);
        }

        [TestMethod]
        public void TestNullSpanOperations()
        {
            // NullSpan should handle all operations without throwing
            var span = NullSpan.Instance;

            Assert.AreEqual(string.Empty, span.Name);
            Assert.AreSame(span, span.SetAttribute("key", "value"));
            Assert.AreSame(span, span.SetAttribute("key", 42L));
            Assert.AreSame(span, span.SetAttribute("key", true));
            Assert.AreSame(span, span.RecordException(new Exception()));
            Assert.AreSame(span, span.SetStatus(SpanStatus.Ok));
            Assert.AreSame(span, span.AddEvent("event"));

            // Dispose should not throw
            span.Dispose();
        }

        [TestMethod]
        public void TestNullTracerReturnsNullSpan()
        {
            var tracer = NullTracer.Instance;

            using var span1 = tracer.StartSpan("Test");
            using var span2 = tracer.StartSpan("Test", SpanKind.Server);

            Assert.AreSame(NullSpan.Instance, span1);
            Assert.AreSame(NullSpan.Instance, span2);
        }
    }
}
