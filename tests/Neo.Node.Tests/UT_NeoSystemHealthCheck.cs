// Copyright (C) 2015-2025 The Neo Project.
//
// UT_NeoSystemHealthCheck.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Neo.Network.P2P;
using Neo.Persistence;
using Neo.Persistence.Providers;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace Neo.Node.Tests
{
    [TestClass]
    public class UT_NeoSystemHealthCheck
    {
        [TestMethod]
        public void TestHealthCheckImplementsInterface()
        {
            var healthCheckType = typeof(NeoSystemHealthCheck);
            Assert.IsTrue(typeof(IHealthCheck).IsAssignableFrom(healthCheckType));
        }

        [TestMethod]
        public async Task TestHealthCheckWithNullNodeReturnsUnhealthy()
        {
            var healthCheck = new NeoSystemHealthCheck(null);
            var context = new HealthCheckContext();

            var result = await healthCheck.CheckHealthAsync(context);

            Assert.AreEqual(HealthStatus.Unhealthy, result.Status);
            Assert.AreEqual("Neo system not initialized", result.Description);
        }

        [TestMethod]
        public void TestHealthCheckHasCheckHealthAsyncMethod()
        {
            var healthCheckType = typeof(NeoSystemHealthCheck);
            var method = healthCheckType.GetMethod("CheckHealthAsync");

            Assert.IsNotNull(method);
            Assert.AreEqual(typeof(Task<HealthCheckResult>), method.ReturnType);
        }

        [TestMethod]
        public async Task TestHealthCheckWithCancellationToken()
        {
            var healthCheck = new NeoSystemHealthCheck(null);
            var context = new HealthCheckContext();
            using var cts = new CancellationTokenSource();

            var result = await healthCheck.CheckHealthAsync(context, cts.Token);

            Assert.AreEqual(HealthStatus.Unhealthy, result.Status);
        }

        [TestMethod]
        public void TestHealthCheckDefaultConstructor()
        {
            var healthCheck = new NeoSystemHealthCheck();
            Assert.IsNotNull(healthCheck);
        }

        [TestMethod]
        public async Task TestHealthCheckWithNullContext()
        {
            var healthCheck = new NeoSystemHealthCheck(null);

            // Should not throw with null context
            var result = await healthCheck.CheckHealthAsync(null!);

            Assert.AreEqual(HealthStatus.Unhealthy, result.Status);
        }

        [TestMethod]
        public void TestHealthCheckConstructorAcceptsNullNode()
        {
            // Should not throw
            var healthCheck = new NeoSystemHealthCheck(null);
            Assert.IsNotNull(healthCheck);
        }

        [TestMethod]
        public void TestHealthCheckConstructorSignature()
        {
            var healthCheckType = typeof(NeoSystemHealthCheck);
            var constructor = healthCheckType.GetConstructor(new[] { typeof(NeoSystemNode) });

            Assert.IsNotNull(constructor);
            Assert.IsTrue(constructor.IsPublic);
        }
    }
}
