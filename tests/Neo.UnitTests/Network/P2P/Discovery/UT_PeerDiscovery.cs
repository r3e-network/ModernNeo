// Copyright (C) 2015-2025 The Neo Project.
//
// UT_PeerDiscovery.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

#nullable enable

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.Network.Discovery;
using System.Net;

namespace Neo.UnitTests.Network.P2P.Discovery
{
    /// <summary>
    /// Unit tests for peer discovery mechanisms.
    /// </summary>
    [TestClass]
    public class UT_PeerDiscovery
    {
        public TestContext TestContext { get; set; } = null!;

        #region PeerInfo Tests

        [TestMethod]
        public void PeerInfo_Create_WithRequiredProperties()
        {
            // Arrange & Act
            var peerId = new byte[] { 1, 2, 3, 4 };
            var addresses = new List<IPEndPoint> { new IPEndPoint(IPAddress.Loopback, 10333) };

            var peerInfo = new PeerInfo
            {
                PeerId = peerId,
                Addresses = addresses
            };

            // Assert
            Assert.AreEqual(peerId, peerInfo.PeerId);
            Assert.AreEqual(1, peerInfo.Addresses.Count);
            Assert.AreEqual(10333, peerInfo.Addresses[0].Port);
        }

        [TestMethod]
        public void PeerInfo_Create_WithAllProperties()
        {
            // Arrange & Act
            var peerId = new byte[] { 1, 2, 3, 4 };
            var addresses = new List<IPEndPoint> { new IPEndPoint(IPAddress.Loopback, 10333) };
            var protocols = new List<string> { "neo/1.0", "neo/2.0" };
            var metadata = new Dictionary<string, string> { ["version"] = "3.0" };

            var peerInfo = new PeerInfo
            {
                PeerId = peerId,
                Addresses = addresses,
                Protocols = protocols,
                DiscoverySource = "test",
                Metadata = metadata
            };

            // Assert
            Assert.AreEqual(2, peerInfo.Protocols.Count);
            Assert.AreEqual("test", peerInfo.DiscoverySource);
            Assert.AreEqual("3.0", peerInfo.Metadata!["version"]);
        }

        #endregion

        #region MdnsDiscovery Tests

        [TestMethod]
        public void MdnsDiscovery_Create_WithDefaultOptions()
        {
            // Arrange & Act
            var discovery = new MdnsDiscovery();

            // Assert
            Assert.AreEqual("mDNS", discovery.Name);
            Assert.IsFalse(discovery.IsRunning);
        }

        [TestMethod]
        public void MdnsDiscovery_Create_WithCustomOptions()
        {
            // Arrange
            var options = new MdnsDiscoveryOptions
            {
                NodeId = "test-node",
                NetworkId = 12345,
                ServicePort = 20333
            };

            // Act
            var discovery = new MdnsDiscovery(options);

            // Assert
            Assert.AreEqual("mDNS", discovery.Name);
            Assert.IsFalse(discovery.IsRunning);
        }

        [TestMethod]
        public async Task MdnsDiscovery_FindPeers_WhenNotStarted_ReturnsEmpty()
        {
            // Arrange
            var discovery = new MdnsDiscovery();

            // Act
            var peers = await discovery.FindPeersAsync(10, TestContext.CancellationTokenSource.Token);

            // Assert
            Assert.AreEqual(0, peers.Count());
        }

        [TestMethod]
        public async Task MdnsDiscovery_DisposeAsync_WhenNotStarted_DoesNotThrow()
        {
            // Arrange
            var discovery = new MdnsDiscovery();

            // Act & Assert - should not throw
            await discovery.DisposeAsync();
        }

        #endregion

        #region KademliaDht Tests

        [TestMethod]
        public void KademliaDht_Create_WithDefaultOptions()
        {
            // Arrange & Act
            var dht = new KademliaDht();

            // Assert
            Assert.AreEqual("Kademlia DHT", dht.Name);
            Assert.IsFalse(dht.IsRunning);
            Assert.AreEqual(32, dht.LocalId.Length); // SHA-256 = 32 bytes
        }

        [TestMethod]
        public void KademliaDht_Create_WithCustomNodeId()
        {
            // Arrange
            var nodeId = new byte[32];
            nodeId[0] = 0xAB;
            nodeId[31] = 0xCD;

            var options = new KademliaDhtOptions { NodeId = nodeId };

            // Act
            var dht = new KademliaDht(options);

            // Assert
            Assert.AreEqual(0xAB, dht.LocalId[0]);
            Assert.AreEqual(0xCD, dht.LocalId[31]);
        }

        [TestMethod]
        public void KademliaDht_AddPeer_IncreasesRoutingTableSize()
        {
            // Arrange
            var dht = new KademliaDht();
            var peerId = new byte[32];
            peerId[0] = 0xFF; // Different from local ID

            var peer = new PeerInfo
            {
                PeerId = peerId,
                Addresses = [new IPEndPoint(IPAddress.Loopback, 10333)]
            };

            // Act
            dht.AddPeer(peer);

            // Assert
            Assert.AreEqual(1, dht.RoutingTableSize);
        }

        [TestMethod]
        public void KademliaDht_AddPeer_DoesNotAddSelf()
        {
            // Arrange
            var dht = new KademliaDht();
            var selfPeer = new PeerInfo
            {
                PeerId = dht.LocalId,
                Addresses = [new IPEndPoint(IPAddress.Loopback, 10333)]
            };

            // Act
            dht.AddPeer(selfPeer);

            // Assert
            Assert.AreEqual(0, dht.RoutingTableSize);
        }

        [TestMethod]
        public void KademliaDht_RemovePeer_DecreasesRoutingTableSize()
        {
            // Arrange
            var dht = new KademliaDht();
            var peerId = new byte[32];
            peerId[0] = 0xFF;

            var peer = new PeerInfo
            {
                PeerId = peerId,
                Addresses = [new IPEndPoint(IPAddress.Loopback, 10333)]
            };

            dht.AddPeer(peer);
            Assert.AreEqual(1, dht.RoutingTableSize);

            // Act
            dht.RemovePeer(peerId);

            // Assert
            Assert.AreEqual(0, dht.RoutingTableSize);
        }

        [TestMethod]
        public async Task KademliaDht_FindPeers_ReturnsAddedPeers()
        {
            // Arrange
            var dht = new KademliaDht();

            for (int i = 0; i < 5; i++)
            {
                var peerId = new byte[32];
                peerId[0] = (byte)(i + 1);
                dht.AddPeer(new PeerInfo
                {
                    PeerId = peerId,
                    Addresses = [new IPEndPoint(IPAddress.Loopback, 10333 + i)]
                });
            }

            // Act
            var peers = await dht.FindPeersAsync(10, TestContext.CancellationTokenSource.Token);

            // Assert - FindPeersAsync returns up to Alpha (3) peers initially
            // since there's no network to query for more
            Assert.IsTrue(peers.Count() >= 3 && peers.Count() <= 5);
        }

        [TestMethod]
        public async Task KademliaDht_DisposeAsync_WhenNotStarted_DoesNotThrow()
        {
            // Arrange
            var dht = new KademliaDht();

            // Act & Assert - should not throw
            await dht.DisposeAsync();
        }

        [TestMethod]
        public void KademliaDht_OnPeerDiscovered_RaisedWhenPeerAdded()
        {
            // Arrange
            var dht = new KademliaDht();
            PeerInfo? discoveredPeer = null;
            dht.OnPeerDiscovered += p => discoveredPeer = p;

            var peerId = new byte[32];
            peerId[0] = 0xFF;
            var peer = new PeerInfo
            {
                PeerId = peerId,
                Addresses = [new IPEndPoint(IPAddress.Loopback, 10333)]
            };

            // Act
            dht.AddPeer(peer);

            // Assert
            Assert.IsNotNull(discoveredPeer);
            Assert.AreEqual(0xFF, discoveredPeer.PeerId[0]);
        }

        [TestMethod]
        public void KademliaDht_OnPeerLost_RaisedWhenPeerRemoved()
        {
            // Arrange
            var dht = new KademliaDht();
            PeerInfo? lostPeer = null;
            dht.OnPeerLost += p => lostPeer = p;

            var peerId = new byte[32];
            peerId[0] = 0xFF;
            var peer = new PeerInfo
            {
                PeerId = peerId,
                Addresses = [new IPEndPoint(IPAddress.Loopback, 10333)]
            };

            dht.AddPeer(peer);

            // Act
            dht.RemovePeer(peerId);

            // Assert
            Assert.IsNotNull(lostPeer);
            Assert.AreEqual(0xFF, lostPeer.PeerId[0]);
        }

        #endregion

        #region CompositeDiscovery Tests

        [TestMethod]
        public void CompositeDiscovery_Create_Empty()
        {
            // Arrange & Act
            var composite = new CompositeDiscovery();

            // Assert
            Assert.AreEqual("Composite", composite.Name);
            Assert.AreEqual(0, composite.DiscoveryCount);
            Assert.IsFalse(composite.IsRunning);
        }

        [TestMethod]
        public void CompositeDiscovery_AddDiscovery_IncreasesCount()
        {
            // Arrange
            var composite = new CompositeDiscovery();
            var mdns = new MdnsDiscovery();
            var dht = new KademliaDht();

            // Act
            composite.AddDiscovery(mdns);
            composite.AddDiscovery(dht);

            // Assert
            Assert.AreEqual(2, composite.DiscoveryCount);
        }

        [TestMethod]
        public void CompositeDiscovery_RemoveDiscovery_DecreasesCount()
        {
            // Arrange
            var composite = new CompositeDiscovery();
            var mdns = new MdnsDiscovery();
            composite.AddDiscovery(mdns);

            // Act
            var removed = composite.RemoveDiscovery(mdns);

            // Assert
            Assert.IsTrue(removed);
            Assert.AreEqual(0, composite.DiscoveryCount);
        }

        [TestMethod]
        public async Task CompositeDiscovery_FindPeers_AggregatesFromAllSources()
        {
            // Arrange
            var composite = new CompositeDiscovery();
            var dht1 = new KademliaDht();
            var dht2 = new KademliaDht();

            // Add peers to each DHT
            var peer1Id = new byte[32];
            peer1Id[0] = 0x01;
            dht1.AddPeer(new PeerInfo
            {
                PeerId = peer1Id,
                Addresses = [new IPEndPoint(IPAddress.Loopback, 10333)]
            });

            var peer2Id = new byte[32];
            peer2Id[0] = 0x02;
            dht2.AddPeer(new PeerInfo
            {
                PeerId = peer2Id,
                Addresses = [new IPEndPoint(IPAddress.Loopback, 10334)]
            });

            composite.AddDiscovery(dht1);
            composite.AddDiscovery(dht2);

            // Act
            var peers = await composite.FindPeersAsync(10, TestContext.CancellationTokenSource.Token);

            // Assert
            Assert.AreEqual(2, peers.Count());
        }

        [TestMethod]
        public async Task CompositeDiscovery_DisposeAsync_DisposesAllDiscoveries()
        {
            // Arrange
            var composite = new CompositeDiscovery();
            composite.AddDiscovery(new MdnsDiscovery());
            composite.AddDiscovery(new KademliaDht());

            // Act & Assert - should not throw
            await composite.DisposeAsync();
            Assert.AreEqual(0, composite.DiscoveryCount);
        }

        #endregion

        #region MdnsDiscoveryOptions Tests

        [TestMethod]
        public void MdnsDiscoveryOptions_DefaultValues()
        {
            // Arrange & Act
            var options = new MdnsDiscoveryOptions();

            // Assert
            Assert.IsNotNull(options.NodeId);
            Assert.AreEqual(8, options.NodeId.Length);
            Assert.AreEqual(860833102u, options.NetworkId); // Neo N3 MainNet
            Assert.AreEqual(10333, options.ServicePort);
            Assert.AreEqual(TimeSpan.FromSeconds(30), options.AnnounceInterval);
            Assert.AreEqual(120, options.TtlSeconds);
            Assert.AreEqual(TimeSpan.FromMinutes(5), options.PeerTimeout);
        }

        #endregion

        #region KademliaDhtOptions Tests

        [TestMethod]
        public void KademliaDhtOptions_DefaultValues()
        {
            // Arrange & Act
            var options = new KademliaDhtOptions();

            // Assert
            Assert.IsNull(options.NodeId);
            Assert.AreEqual(0, options.BootstrapNodes.Count);
            Assert.AreEqual(TimeSpan.FromMinutes(10), options.RefreshInterval);
            Assert.AreEqual(TimeSpan.FromHours(1), options.BucketRefreshInterval);
            Assert.AreEqual(TimeSpan.FromMinutes(15), options.PeerTimeout);
            Assert.AreEqual(TimeSpan.FromHours(24), options.ProviderRecordTtl);
        }

        [TestMethod]
        public void KademliaDhtOptions_WithBootstrapNodes()
        {
            // Arrange & Act
            var options = new KademliaDhtOptions
            {
                BootstrapNodes = [
                    new IPEndPoint(IPAddress.Parse("192.168.1.1"), 10333),
                    new IPEndPoint(IPAddress.Parse("192.168.1.2"), 10333)
                ]
            };

            // Assert
            Assert.AreEqual(2, options.BootstrapNodes.Count);
        }

        #endregion
    }
}
