// Copyright (C) 2015-2025 The Neo Project.
//
// UT_HealthChecks.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

#pragma warning disable MSTEST0049 // TestMethod should not call GetAwaiter().GetResult()

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.Observability.Health;
using System;

namespace Neo.UnitTests.Observability
{
    [TestClass]
    public class UT_HealthChecks
    {
        #region HealthCheckService Tests

        [TestMethod]
        public void HealthCheckService_NoChecks_ReturnsHealthy()
        {
            var service = new HealthCheckService();
            var status = service.GetOverallStatusAsync().GetAwaiter().GetResult();
            Assert.AreEqual(HealthStatus.Healthy, status);
        }

        [TestMethod]
        public void HealthCheckService_AllHealthy_ReturnsHealthy()
        {
            var service = new HealthCheckService();
            service.Register(new NetworkHealthCheck(() => 10));
            service.Register(new MempoolHealthCheck(() => 100, () => 1000));

            var status = service.GetOverallStatusAsync().GetAwaiter().GetResult();
            Assert.AreEqual(HealthStatus.Healthy, status);
        }

        [TestMethod]
        public void HealthCheckService_OneDegraded_ReturnsDegraded()
        {
            var service = new HealthCheckService();
            service.Register(new NetworkHealthCheck(() => 10));
            service.Register(new NetworkHealthCheck(() => 4)); // Below degraded threshold

            var status = service.GetOverallStatusAsync().GetAwaiter().GetResult();
            Assert.AreEqual(HealthStatus.Degraded, status);
        }

        [TestMethod]
        public void HealthCheckService_OneUnhealthy_ReturnsUnhealthy()
        {
            var service = new HealthCheckService();
            service.Register(new NetworkHealthCheck(() => 10));
            service.Register(new NetworkHealthCheck(() => 0)); // No peers

            var status = service.GetOverallStatusAsync().GetAwaiter().GetResult();
            Assert.AreEqual(HealthStatus.Unhealthy, status);
        }

        [TestMethod]
        public void HealthCheckService_CheckAll_ReturnsAllResults()
        {
            var service = new HealthCheckService();
            service.Register(new NetworkHealthCheck(() => 10));
            service.Register(new MempoolHealthCheck(() => 100, () => 1000));

            var results = service.CheckAllAsync().GetAwaiter().GetResult();
            Assert.AreEqual(2, results.Count);
            Assert.IsTrue(results.ContainsKey("network"));
            Assert.IsTrue(results.ContainsKey("mempool"));
        }

        #endregion

        #region NetworkHealthCheck Tests

        [TestMethod]
        public void NetworkHealthCheck_ManyPeers_ReturnsHealthy()
        {
            var check = new NetworkHealthCheck(() => 10);
            var result = check.CheckHealthAsync().GetAwaiter().GetResult();

            Assert.AreEqual(HealthStatus.Healthy, result.Status);
            Assert.IsTrue(result.Description?.Contains("10"));
        }

        [TestMethod]
        public void NetworkHealthCheck_FewPeers_ReturnsDegraded()
        {
            var check = new NetworkHealthCheck(() => 4, minPeers: 3, degradedThreshold: 5);
            var result = check.CheckHealthAsync().GetAwaiter().GetResult();

            Assert.AreEqual(HealthStatus.Degraded, result.Status);
        }

        [TestMethod]
        public void NetworkHealthCheck_BelowMinimum_ReturnsUnhealthy()
        {
            var check = new NetworkHealthCheck(() => 2, minPeers: 3);
            var result = check.CheckHealthAsync().GetAwaiter().GetResult();

            Assert.AreEqual(HealthStatus.Unhealthy, result.Status);
        }

        [TestMethod]
        public void NetworkHealthCheck_NoPeers_ReturnsUnhealthy()
        {
            var check = new NetworkHealthCheck(() => 0);
            var result = check.CheckHealthAsync().GetAwaiter().GetResult();

            Assert.AreEqual(HealthStatus.Unhealthy, result.Status);
            Assert.IsTrue(result.Description?.Contains("No peers"));
        }

        [TestMethod]
        public void NetworkHealthCheck_HasCorrectName()
        {
            var check = new NetworkHealthCheck(() => 10);
            Assert.AreEqual("network", check.Name);
        }

        #endregion

        #region MempoolHealthCheck Tests

        [TestMethod]
        public void MempoolHealthCheck_LowUtilization_ReturnsHealthy()
        {
            var check = new MempoolHealthCheck(() => 100, () => 1000);
            var result = check.CheckHealthAsync().GetAwaiter().GetResult();

            Assert.AreEqual(HealthStatus.Healthy, result.Status);
        }

        [TestMethod]
        public void MempoolHealthCheck_HighUtilization_ReturnsDegraded()
        {
            var check = new MempoolHealthCheck(() => 750, () => 1000, warningThreshold: 0.7);
            var result = check.CheckHealthAsync().GetAwaiter().GetResult();

            Assert.AreEqual(HealthStatus.Degraded, result.Status);
        }

        [TestMethod]
        public void MempoolHealthCheck_CriticalUtilization_ReturnsUnhealthy()
        {
            var check = new MempoolHealthCheck(() => 950, () => 1000, criticalThreshold: 0.9);
            var result = check.CheckHealthAsync().GetAwaiter().GetResult();

            Assert.AreEqual(HealthStatus.Unhealthy, result.Status);
        }

        [TestMethod]
        public void MempoolHealthCheck_HasCorrectName()
        {
            var check = new MempoolHealthCheck(() => 0, () => 1000);
            Assert.AreEqual("mempool", check.Name);
        }

        #endregion

        #region BlockchainHealthCheck Tests

        [TestMethod]
        public void BlockchainHealthCheck_Synced_ReturnsHealthy()
        {
            var check = new BlockchainHealthCheck(() => 1000, () => 1000);
            var result = check.CheckHealthAsync().GetAwaiter().GetResult();

            Assert.AreEqual(HealthStatus.Healthy, result.Status);
            Assert.IsTrue(result.Description?.Contains("synced"));
        }

        [TestMethod]
        public void BlockchainHealthCheck_SmallLag_ReturnsDegraded()
        {
            var check = new BlockchainHealthCheck(() => 995, () => 1000, maxBlockLag: 10);
            var result = check.CheckHealthAsync().GetAwaiter().GetResult();

            Assert.AreEqual(HealthStatus.Degraded, result.Status);
        }

        [TestMethod]
        public void BlockchainHealthCheck_LargeLag_ReturnsUnhealthy()
        {
            var check = new BlockchainHealthCheck(() => 900, () => 1000, maxBlockLag: 10);
            var result = check.CheckHealthAsync().GetAwaiter().GetResult();

            Assert.AreEqual(HealthStatus.Unhealthy, result.Status);
        }

        [TestMethod]
        public void BlockchainHealthCheck_HasCorrectName()
        {
            var check = new BlockchainHealthCheck(() => 0, () => 0);
            Assert.AreEqual("blockchain", check.Name);
        }

        #endregion

        #region ConsensusHealthCheck Tests

        [TestMethod]
        public void ConsensusHealthCheck_Disabled_ReturnsHealthy()
        {
            var check = new ConsensusHealthCheck(() => false, () => false, () => 0);
            var result = check.CheckHealthAsync().GetAwaiter().GetResult();

            Assert.AreEqual(HealthStatus.Healthy, result.Status);
            Assert.IsTrue(result.Description?.Contains("not enabled"));
        }

        [TestMethod]
        public void ConsensusHealthCheck_EnabledNotActive_ReturnsDegraded()
        {
            var check = new ConsensusHealthCheck(() => true, () => false, () => 0);
            var result = check.CheckHealthAsync().GetAwaiter().GetResult();

            Assert.AreEqual(HealthStatus.Degraded, result.Status);
        }

        [TestMethod]
        public void ConsensusHealthCheck_ActiveLowView_ReturnsHealthy()
        {
            var check = new ConsensusHealthCheck(() => true, () => true, () => 0);
            var result = check.CheckHealthAsync().GetAwaiter().GetResult();

            Assert.AreEqual(HealthStatus.Healthy, result.Status);
        }

        [TestMethod]
        public void ConsensusHealthCheck_HighViewNumber_ReturnsDegraded()
        {
            var check = new ConsensusHealthCheck(() => true, () => true, () => 5, maxViewNumber: 3);
            var result = check.CheckHealthAsync().GetAwaiter().GetResult();

            Assert.AreEqual(HealthStatus.Degraded, result.Status);
        }

        [TestMethod]
        public void ConsensusHealthCheck_HasCorrectName()
        {
            var check = new ConsensusHealthCheck(() => false, () => false, () => 0);
            Assert.AreEqual("consensus", check.Name);
        }

        #endregion

        #region StorageHealthCheck Tests

        [TestMethod]
        public void StorageHealthCheck_FastAccess_ReturnsHealthy()
        {
            var check = new StorageHealthCheck(() => true);
            var result = check.CheckHealthAsync().GetAwaiter().GetResult();

            Assert.AreEqual(HealthStatus.Healthy, result.Status);
        }

        [TestMethod]
        public void StorageHealthCheck_AccessFailed_ReturnsUnhealthy()
        {
            var check = new StorageHealthCheck(() => false);
            var result = check.CheckHealthAsync().GetAwaiter().GetResult();

            Assert.AreEqual(HealthStatus.Unhealthy, result.Status);
            Assert.IsTrue(result.Description?.Contains("failed"));
        }

        [TestMethod]
        public void StorageHealthCheck_Exception_ReturnsUnhealthy()
        {
            var check = new StorageHealthCheck(() => throw new Exception("Storage error"));
            var result = check.CheckHealthAsync().GetAwaiter().GetResult();

            Assert.AreEqual(HealthStatus.Unhealthy, result.Status);
            Assert.IsTrue(result.Description?.Contains("Storage error"));
        }

        [TestMethod]
        public void StorageHealthCheck_HasCorrectName()
        {
            var check = new StorageHealthCheck(() => true);
            Assert.AreEqual("storage", check.Name);
        }

        #endregion

        #region HealthCheckResult Tests

        [TestMethod]
        public void HealthCheckResult_Healthy_HasCorrectStatus()
        {
            var result = HealthCheckResult.Healthy("All good");
            Assert.AreEqual(HealthStatus.Healthy, result.Status);
            Assert.AreEqual("All good", result.Description);
        }

        [TestMethod]
        public void HealthCheckResult_Degraded_HasCorrectStatus()
        {
            var result = HealthCheckResult.Degraded("Slow");
            Assert.AreEqual(HealthStatus.Degraded, result.Status);
            Assert.AreEqual("Slow", result.Description);
        }

        [TestMethod]
        public void HealthCheckResult_Unhealthy_HasCorrectStatus()
        {
            var result = HealthCheckResult.Unhealthy("Down");
            Assert.AreEqual(HealthStatus.Unhealthy, result.Status);
            Assert.AreEqual("Down", result.Description);
        }

        #endregion
    }
}
