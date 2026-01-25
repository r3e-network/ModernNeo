// Copyright (C) 2015-2025 The Neo Project.
//
// PeerManagementIntegrationTests.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Orleans.Interfaces;
using Orleans.TestingHost;

namespace Neo.Orleans.Tests.Integration
{
    /// <summary>
    /// Integration tests for peer management across LocalNodeGrain and RemoteNodeGrain.
    /// Tests grain-to-grain collaboration for peer registration/deregistration.
    /// </summary>
    [TestClass]
    public class PeerManagementIntegrationTests
    {
        private TestCluster? _cluster;

        [TestInitialize]
        public async Task Setup()
        {
            var builder = new TestClusterBuilder();
            builder.Options.InitialSilosCount = 1;
            builder.AddSiloBuilderConfigurator<IntegrationTestSiloConfigurator>();
            _cluster = builder.Build();
            await _cluster.DeployAsync();
        }

        [TestCleanup]
        public async Task Cleanup()
        {
            if (_cluster != null)
            {
                await _cluster.StopAllSilosAsync();
                await _cluster.DisposeAsync();
            }
        }

        /// <summary>
        /// Scenario 1: RemoteNode completing handshake should register peer in LocalNode.
        /// Tests: RemoteNodeGrain.CompleteHandshakeAsync -> LocalNodeGrain.RegisterPeerAsync
        /// </summary>
        [TestMethod]
        public async Task RemoteNode_CompleteHandshake_RegistersPeerInLocalNode()
        {
            // Arrange
            var localNode = _cluster!.GrainFactory.GetGrain<ILocalNodeGrain>(0);
            var remoteNode = _cluster.GrainFactory.GetGrain<IRemoteNodeGrain>("192.168.1.100:10333");

            var initialCount = await localNode.GetConnectedPeerCountAsync();
            Assert.AreEqual(0, initialCount, "LocalNode should start with no peers");

            // Act - RemoteNode completes handshake, which internally calls LocalNode.RegisterPeerAsync
            await remoteNode.CompleteHandshakeAsync(
                remoteHeight: 1000,
                listenerPort: 10333,
                isFullNode: true,
                userAgent: "/Neo:3.6.0/");

            // Assert - Verify peer is registered in LocalNode
            var peerCount = await localNode.GetConnectedPeerCountAsync();
            Assert.AreEqual(1, peerCount, "LocalNode should have 1 peer after handshake");

            var peers = await localNode.GetConnectedPeersAsync();
            var peerList = peers.ToList();
            Assert.AreEqual(1, peerList.Count);
            Assert.AreEqual("192.168.1.100", peerList[0].Address);
            Assert.AreEqual(10333, peerList[0].Port);
            Assert.AreEqual(1000u, peerList[0].Height);
        }

        /// <summary>
        /// Scenario 2: RemoteNode disconnecting should unregister peer from LocalNode.
        /// Tests: RemoteNodeGrain.DisconnectAsync -> LocalNodeGrain.UnregisterPeerAsync
        /// </summary>
        [TestMethod]
        public async Task RemoteNode_Disconnect_UnregistersPeerFromLocalNode()
        {
            // Arrange - First establish a connection
            var localNode = _cluster!.GrainFactory.GetGrain<ILocalNodeGrain>(0);
            var remoteNode = _cluster.GrainFactory.GetGrain<IRemoteNodeGrain>("192.168.1.101:10333");

            await remoteNode.CompleteHandshakeAsync(
                remoteHeight: 500,
                listenerPort: 10333,
                isFullNode: true,
                userAgent: "/Neo:3.6.0/");

            var countBefore = await localNode.GetConnectedPeerCountAsync();
            Assert.AreEqual(1, countBefore, "Should have 1 peer before disconnect");

            // Act - Disconnect the remote node
            await remoteNode.DisconnectAsync();

            // Assert - Peer should be unregistered from LocalNode
            var countAfter = await localNode.GetConnectedPeerCountAsync();
            Assert.AreEqual(0, countAfter, "LocalNode should have 0 peers after disconnect");
        }

        /// <summary>
        /// Scenario: Multiple RemoteNodes registering with same LocalNode.
        /// Tests concurrent peer management.
        /// </summary>
        [TestMethod]
        public async Task MultipleRemoteNodes_CompleteHandshake_AllRegisteredInLocalNode()
        {
            // Arrange
            var localNode = _cluster!.GrainFactory.GetGrain<ILocalNodeGrain>(0);
            var remote1 = _cluster.GrainFactory.GetGrain<IRemoteNodeGrain>("10.0.0.1:10333");
            var remote2 = _cluster.GrainFactory.GetGrain<IRemoteNodeGrain>("10.0.0.2:10333");
            var remote3 = _cluster.GrainFactory.GetGrain<IRemoteNodeGrain>("10.0.0.3:10333");

            // Act - Complete handshakes concurrently
            await Task.WhenAll(
                remote1.CompleteHandshakeAsync(100, 10333, true, "/Neo:3.6.0/"),
                remote2.CompleteHandshakeAsync(200, 10333, true, "/Neo:3.6.0/"),
                remote3.CompleteHandshakeAsync(300, 10333, false, "/Neo:3.5.0/")
            );

            // Assert
            var peerCount = await localNode.GetConnectedPeerCountAsync();
            Assert.AreEqual(3, peerCount, "LocalNode should have 3 peers");

            var peers = (await localNode.GetConnectedPeersAsync()).ToList();
            Assert.AreEqual(3, peers.Count);

            // Verify each peer's height
            var heights = peers.Select(p => p.Height).OrderBy(h => h).ToList();
            CollectionAssert.AreEqual(new uint[] { 100, 200, 300 }, heights);
        }

        /// <summary>
        /// Scenario: Partial disconnect - some peers remain connected.
        /// </summary>
        [TestMethod]
        public async Task PartialDisconnect_RemainingPeersStayRegistered()
        {
            // Arrange
            var localNode = _cluster!.GrainFactory.GetGrain<ILocalNodeGrain>(0);
            var remote1 = _cluster.GrainFactory.GetGrain<IRemoteNodeGrain>("10.0.1.1:10333");
            var remote2 = _cluster.GrainFactory.GetGrain<IRemoteNodeGrain>("10.0.1.2:10333");

            await remote1.CompleteHandshakeAsync(100, 10333, true, "/Neo:3.6.0/");
            await remote2.CompleteHandshakeAsync(200, 10333, true, "/Neo:3.6.0/");

            Assert.AreEqual(2, await localNode.GetConnectedPeerCountAsync());

            // Act - Disconnect only remote1
            await remote1.DisconnectAsync();

            // Assert - remote2 should still be registered
            var peerCount = await localNode.GetConnectedPeerCountAsync();
            Assert.AreEqual(1, peerCount, "Should have 1 peer remaining");

            var peers = (await localNode.GetConnectedPeersAsync()).ToList();
            Assert.AreEqual("10.0.1.2", peers[0].Address);
            Assert.AreEqual(200u, peers[0].Height);
        }
    }
}
