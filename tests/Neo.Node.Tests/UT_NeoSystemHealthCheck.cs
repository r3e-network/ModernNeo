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
    }
}
