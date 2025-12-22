// Copyright (C) 2015-2025 The Neo Project.
//
// UT_NodeInfoService.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.Services.NodeInfo;
using System;

namespace Neo.UnitTests.Services
{
    [TestClass]
    public class UT_NodeInfoService
    {
        private NeoSystem _system;
        private NodeInfoService _service;

        [TestInitialize]
        public void Setup()
        {
            _system = TestBlockchain.GetSystem();
            _service = new NodeInfoService(_system);
        }

        [TestMethod]
        public void Constructor_NullDependency_ThrowsArgumentNullException()
        {
            Assert.ThrowsExactly<ArgumentNullException>(() => new NodeInfoService(null!));
        }

        [TestMethod]
        public void GetNodeInfo_ReturnsValidNodeInfo()
        {
            // Act
            var network = _service.Network;
            var height = _service.Height;
            var mempoolCount = _service.MempoolCount;

            // Assert
            Assert.IsNotNull(network);
            Assert.IsTrue(height >= 0);
            Assert.IsTrue(mempoolCount >= 0);
        }

        [TestMethod]
        public void GetNetworkInfo_ReturnsNetworkDetails()
        {
            // Act
            var network = _service.Network;

            // Assert
            Assert.IsNotNull(network);
            Assert.AreEqual(_system.Settings.Network.ToString(), network);
        }

        [TestMethod]
        public void GetMempoolInfo_ReturnsMempoolStatistics()
        {
            // Act
            var mempoolCount = _service.MempoolCount;

            // Assert
            Assert.IsTrue(mempoolCount >= 0);
            Assert.AreEqual(_system.MemPool.Count, mempoolCount);
        }

        [TestMethod]
        public void GetNodeInfo_WithMockedDependencies_ReturnsExpectedValues()
        {
            // Arrange
            var system = TestBlockchain.GetSystem();
            var service = new NodeInfoService(system);

            // Act
            var network = service.Network;
            var height = service.Height;
            var mempoolCount = service.MempoolCount;

            // Assert
            Assert.AreEqual(system.Settings.Network.ToString(), network);
            Assert.IsTrue(height >= 0, "Height should be non-negative");
            Assert.AreEqual(system.MemPool.Count, mempoolCount);
        }

        [TestMethod]
        public void Network_Property_ReturnsConsistentValue()
        {
            // Act
            var network1 = _service.Network;
            var network2 = _service.Network;

            // Assert
            Assert.AreEqual(network1, network2);
        }

        [TestMethod]
        public void Height_Property_ReturnsNonNegativeValue()
        {
            // Act
            var height = _service.Height;

            // Assert
            Assert.IsTrue(height >= 0, "Height should never be negative");
        }

        [TestMethod]
        public void MempoolCount_Property_ReturnsNonNegativeValue()
        {
            // Act
            var count = _service.MempoolCount;

            // Assert
            Assert.IsTrue(count >= 0, "Mempool count should never be negative");
        }

        [TestMethod]
        public void Service_ImplementsInterface()
        {
            // Assert
            Assert.IsInstanceOfType(_service, typeof(INodeInfoService));
        }

        [TestMethod]
        public void Network_Property_MatchesSystemSettings()
        {
            // Act
            var network = _service.Network;
            var expectedNetwork = _system.Settings.Network.ToString();

            // Assert
            Assert.AreEqual(expectedNetwork, network);
        }
    }
}
