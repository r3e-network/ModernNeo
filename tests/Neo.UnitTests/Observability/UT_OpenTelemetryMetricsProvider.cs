// Copyright (C) 2015-2025 The Neo Project.
//
// UT_OpenTelemetryMetricsProvider.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.Observability.Metrics;
using System;

namespace Neo.UnitTests.Observability
{
    [TestClass]
    public class UT_OpenTelemetryMetricsProvider
    {
        [TestMethod]
        public void TestCreateProvider()
        {
            using var provider = new OpenTelemetryMetricsProvider("Neo.Test", "1.0.0");
            Assert.IsNotNull(provider);
            Assert.AreEqual("Neo.Test", provider.ServiceName);
            Assert.AreEqual("1.0.0", provider.ServiceVersion);
        }

        [TestMethod]
        public void TestCreateProviderWithDefaults()
        {
            using var provider = new OpenTelemetryMetricsProvider();
            Assert.IsNotNull(provider);
            Assert.AreEqual("Neo.Blockchain", provider.ServiceName);
            Assert.IsNull(provider.ServiceVersion);
        }

        [TestMethod]
        public void TestProviderImplementsIMetricsProvider()
        {
            using var provider = new OpenTelemetryMetricsProvider();
            Assert.IsInstanceOfType(provider, typeof(IMetricsProvider));
        }

        [TestMethod]
        public void TestProviderImplementsIDisposable()
        {
            var provider = new OpenTelemetryMetricsProvider();
            Assert.IsInstanceOfType(provider, typeof(IDisposable));
            provider.Dispose();
            // Should not throw on double dispose
            provider.Dispose();
        }

        [TestMethod]
        public void TestMeterProperty()
        {
            using var provider = new OpenTelemetryMetricsProvider("Neo.Test");
            Assert.IsNotNull(provider.Meter);
            Assert.AreEqual("Neo.Test", provider.Meter.Name);
        }

        [TestMethod]
        public void TestCreateCounter()
        {
            using var provider = new OpenTelemetryMetricsProvider("Neo.Test");
            var counter = provider.CreateCounter("test_counter", "Test counter description");

            Assert.IsNotNull(counter);
            Assert.AreEqual("test_counter", counter.Name);
            Assert.AreEqual(0, counter.Value);
        }

        [TestMethod]
        public void TestCreateCounterWithUnit()
        {
            using var provider = new OpenTelemetryMetricsProvider("Neo.Test");
            var counter = provider.CreateCounter("test_counter", "Test counter", "requests");

            Assert.IsNotNull(counter);
            Assert.AreEqual("test_counter", counter.Name);
        }

        [TestMethod]
        public void TestCounterIncrement()
        {
            using var provider = new OpenTelemetryMetricsProvider("Neo.Test");
            var counter = provider.CreateCounter("test_counter", "Test counter");

            counter.Increment();
            Assert.AreEqual(1, counter.Value);

            counter.Increment();
            Assert.AreEqual(2, counter.Value);
        }

        [TestMethod]
        public void TestCounterIncrementByValue()
        {
            using var provider = new OpenTelemetryMetricsProvider("Neo.Test");
            var counter = provider.CreateCounter("test_counter", "Test counter");

            counter.Increment(5);
            Assert.AreEqual(5, counter.Value);

            counter.Increment(10);
            Assert.AreEqual(15, counter.Value);
        }

        [TestMethod]
        public void TestCounterIncrementNegativeThrows()
        {
            using var provider = new OpenTelemetryMetricsProvider("Neo.Test");
            var counter = provider.CreateCounter("test_counter", "Test counter");

            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => counter.Increment(-1));
        }

        [TestMethod]
        public void TestCreateGauge()
        {
            using var provider = new OpenTelemetryMetricsProvider("Neo.Test");
            var gauge = provider.CreateGauge("test_gauge", "Test gauge description");

            Assert.IsNotNull(gauge);
            Assert.AreEqual("test_gauge", gauge.Name);
            Assert.AreEqual(0.0, gauge.Value);
        }

        [TestMethod]
        public void TestCreateGaugeWithUnit()
        {
            using var provider = new OpenTelemetryMetricsProvider("Neo.Test");
            var gauge = provider.CreateGauge("test_gauge", "Test gauge", "bytes");

            Assert.IsNotNull(gauge);
            Assert.AreEqual("test_gauge", gauge.Name);
        }

        [TestMethod]
        public void TestGaugeSet()
        {
            using var provider = new OpenTelemetryMetricsProvider("Neo.Test");
            var gauge = provider.CreateGauge("test_gauge", "Test gauge");

            gauge.Set(42.5);
            Assert.AreEqual(42.5, gauge.Value);

            gauge.Set(100.0);
            Assert.AreEqual(100.0, gauge.Value);
        }

        [TestMethod]
        public void TestGaugeIncrementDecrement()
        {
            using var provider = new OpenTelemetryMetricsProvider("Neo.Test");
            var gauge = provider.CreateGauge("test_gauge", "Test gauge");

            gauge.Increment();
            Assert.AreEqual(1.0, gauge.Value);

            gauge.Increment();
            Assert.AreEqual(2.0, gauge.Value);

            gauge.Decrement();
            Assert.AreEqual(1.0, gauge.Value);
        }

        [TestMethod]
        public void TestCreateHistogram()
        {
            using var provider = new OpenTelemetryMetricsProvider("Neo.Test");
            var histogram = provider.CreateHistogram("test_histogram", "Test histogram description");

            Assert.IsNotNull(histogram);
            Assert.AreEqual("test_histogram", histogram.Name);
            Assert.AreEqual(0, histogram.Count);
            Assert.AreEqual(0.0, histogram.Sum);
        }

        [TestMethod]
        public void TestCreateHistogramWithBuckets()
        {
            using var provider = new OpenTelemetryMetricsProvider("Neo.Test");
            var buckets = new double[] { 0.1, 0.5, 1.0, 5.0, 10.0 };
            var histogram = provider.CreateHistogram("test_histogram", "Test histogram", buckets);

            Assert.IsNotNull(histogram);
            Assert.AreEqual("test_histogram", histogram.Name);
        }

        [TestMethod]
        public void TestCreateHistogramWithUnit()
        {
            using var provider = new OpenTelemetryMetricsProvider("Neo.Test");
            var histogram = provider.CreateHistogram("test_histogram", "Test histogram", "seconds", null);

            Assert.IsNotNull(histogram);
        }

        [TestMethod]
        public void TestHistogramObserve()
        {
            using var provider = new OpenTelemetryMetricsProvider("Neo.Test");
            var histogram = provider.CreateHistogram("test_histogram", "Test histogram");

            histogram.Observe(10.0);
            Assert.AreEqual(1, histogram.Count);
            Assert.AreEqual(10.0, histogram.Sum);

            histogram.Observe(20.0);
            Assert.AreEqual(2, histogram.Count);
            Assert.AreEqual(30.0, histogram.Sum);

            histogram.Observe(15.0);
            Assert.AreEqual(3, histogram.Count);
            Assert.AreEqual(45.0, histogram.Sum);
        }

        [TestMethod]
        public void TestHistogramMinMax()
        {
            using var provider = new OpenTelemetryMetricsProvider("Neo.Test");
            var histogram = provider.CreateHistogram("test_histogram", "Test histogram");

            // Cast to OTelHistogram to access Min/Max
            var otelHistogram = histogram as OTelHistogram;
            Assert.IsNotNull(otelHistogram);

            otelHistogram.Observe(10.0);
            otelHistogram.Observe(5.0);
            otelHistogram.Observe(20.0);

            Assert.AreEqual(5.0, otelHistogram.Min);
            Assert.AreEqual(20.0, otelHistogram.Max);
        }

        [TestMethod]
        public void TestCounterReuseSameName()
        {
            using var provider = new OpenTelemetryMetricsProvider("Neo.Test");
            var counter1 = provider.CreateCounter("same_counter", "Counter 1");
            var counter2 = provider.CreateCounter("same_counter", "Counter 2");

            Assert.AreSame(counter1, counter2);

            counter1.Increment();
            Assert.AreEqual(1, counter2.Value);
        }

        [TestMethod]
        public void TestGaugeReuseSameName()
        {
            using var provider = new OpenTelemetryMetricsProvider("Neo.Test");
            var gauge1 = provider.CreateGauge("same_gauge", "Gauge 1");
            var gauge2 = provider.CreateGauge("same_gauge", "Gauge 2");

            Assert.AreSame(gauge1, gauge2);

            gauge1.Set(50.0);
            Assert.AreEqual(50.0, gauge2.Value);
        }

        [TestMethod]
        public void TestHistogramReuseSameName()
        {
            using var provider = new OpenTelemetryMetricsProvider("Neo.Test");
            var histogram1 = provider.CreateHistogram("same_histogram", "Histogram 1");
            var histogram2 = provider.CreateHistogram("same_histogram", "Histogram 2");

            Assert.AreSame(histogram1, histogram2);

            histogram1.Observe(100.0);
            Assert.AreEqual(1, histogram2.Count);
        }

        [TestMethod]
        public void TestDisposedProviderThrows()
        {
            var provider = new OpenTelemetryMetricsProvider("Neo.Test");
            provider.Dispose();

            Assert.ThrowsExactly<ObjectDisposedException>(() => provider.CreateCounter("test", "test"));
            Assert.ThrowsExactly<ObjectDisposedException>(() => provider.CreateGauge("test", "test"));
            Assert.ThrowsExactly<ObjectDisposedException>(() => provider.CreateHistogram("test", "test"));
        }

        [TestMethod]
        public void TestCreateObservableCounter()
        {
            using var provider = new OpenTelemetryMetricsProvider("Neo.Test");
            long value = 0;

            // Should not throw
            provider.CreateObservableCounter("observable_counter", () => value, "Observable counter", "items");

            value = 100;
            // The observable counter will report the value when observed by a listener
        }

        [TestMethod]
        public void TestCreateObservableGauge()
        {
            using var provider = new OpenTelemetryMetricsProvider("Neo.Test");
            double value = 0;

            // Should not throw
            provider.CreateObservableGauge("observable_gauge", () => value, "Observable gauge", "percent");

            value = 75.5;
            // The observable gauge will report the value when observed by a listener
        }
    }
}
