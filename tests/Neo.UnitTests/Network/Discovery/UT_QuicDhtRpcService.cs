// Copyright (C) 2015-2025 The Neo Project.
//
// UT_QuicDhtRpcService.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.Network.Discovery;
using System;
using System.Collections.Generic;
using System.Net;
using System.Runtime.Versioning;
using System.Threading.Tasks;

namespace Neo.UnitTests.Network.Discovery
{
    [TestClass]
    [SupportedOSPlatform("linux")]
    [SupportedOSPlatform("windows")]
    [SupportedOSPlatform("osx")]
    public class UT_QuicDhtRpcService
    {
        public TestContext TestContext { get; set; } = null!;
        [TestMethod]
        [TestCategory("DHT")]
        public void PeerInfo_Serialization_Roundtrip()
        {
            // Test PeerInfo can be created with required properties
            var peerId = new byte[32];
            Random.Shared.NextBytes(peerId);

            var peer = new PeerInfo
            {
                PeerId = peerId,
                Addresses = new List<IPEndPoint> { new IPEndPoint(IPAddress.Loopback, 10333) },
                DiscoverySource = "test"
            };

            Assert.IsNotNull(peer.PeerId);
            Assert.AreEqual(32, peer.PeerId.Length);
            Assert.AreEqual(1, peer.Addresses.Count);
            Assert.AreEqual("test", peer.DiscoverySource);
        }

        [TestMethod]
        [TestCategory("DHT")]
        public void PeerInfo_MultipleAddresses()
        {
            var peerId = new byte[32];
            Random.Shared.NextBytes(peerId);

            var addresses = new List<IPEndPoint>
            {
                new IPEndPoint(IPAddress.Parse("192.168.1.1"), 10333),
                new IPEndPoint(IPAddress.Parse("10.0.0.1"), 10334),
                new IPEndPoint(IPAddress.Loopback, 10335)
            };

            var peer = new PeerInfo
            {
                PeerId = peerId,
                Addresses = addresses,
                Protocols = new[] { "neo-p2p", "neo-dht" },
                DiscoverySource = "dht"
            };

            Assert.AreEqual(3, peer.Addresses.Count);
            Assert.AreEqual(2, peer.Protocols.Count);
        }

        [TestMethod]
        [TestCategory("DHT")]
        public void PeerInfo_LastSeen_DefaultsToNow()
        {
            var before = DateTimeOffset.UtcNow;

            var peer = new PeerInfo
            {
                PeerId = new byte[32],
                Addresses = new List<IPEndPoint> { new IPEndPoint(IPAddress.Loopback, 10333) }
            };

            var after = DateTimeOffset.UtcNow;

            Assert.IsTrue(peer.LastSeen >= before);
            Assert.IsTrue(peer.LastSeen <= after);
        }

        [TestMethod]
        [TestCategory("DHT")]
        public void PeerInfo_Metadata_Optional()
        {
            var peer = new PeerInfo
            {
                PeerId = new byte[32],
                Addresses = new List<IPEndPoint> { new IPEndPoint(IPAddress.Loopback, 10333) },
                Metadata = new Dictionary<string, string>
                {
                    ["version"] = "1.0",
                    ["capabilities"] = "full-node"
                }
            };

            Assert.IsNotNull(peer.Metadata);
            Assert.AreEqual(2, peer.Metadata.Count);
            Assert.AreEqual("1.0", peer.Metadata["version"]);
        }

        [TestMethod]
        [TestCategory("DHT")]
        public void IDhtRpcService_Interface_HasRequiredMethods()
        {
            // Verify interface contract
            var interfaceType = typeof(IDhtRpcService);

            Assert.IsNotNull(interfaceType.GetMethod("FindNodeAsync"));
            Assert.IsNotNull(interfaceType.GetMethod("GetProvidersAsync"));
            Assert.IsNotNull(interfaceType.GetMethod("AddProviderAsync"));
            Assert.IsNotNull(interfaceType.GetMethod("PingAsync"));
        }

        [TestMethod]
        [TestCategory("DHT")]
        public void KademliaDht_WithNullRpcService_OperatesInLocalMode()
        {
            // KademliaDht should work without RPC service (local-only mode)
            var options = new KademliaDhtOptions
            {
                NodeId = new byte[32],
                BootstrapNodes = Array.Empty<IPEndPoint>()
            };

            var dht = new KademliaDht(options, rpcService: null);

            Assert.IsNotNull(dht);
            Assert.AreEqual("Kademlia DHT", dht.Name);
            Assert.IsFalse(dht.IsRunning);
            Assert.AreEqual(0, dht.RoutingTableSize);
        }

        [TestMethod]
        [TestCategory("DHT")]
        public void KademliaDht_GeneratesNodeId_WhenNotProvided()
        {
            var dht1 = new KademliaDht();
            var dht2 = new KademliaDht();

            Assert.IsNotNull(dht1.LocalId);
            Assert.IsNotNull(dht2.LocalId);
            Assert.AreEqual(32, dht1.LocalId.Length);
            Assert.AreEqual(32, dht2.LocalId.Length);

            // Node IDs should be different (random)
            CollectionAssert.AreNotEqual(dht1.LocalId, dht2.LocalId);
        }

        [TestMethod]
        [TestCategory("DHT")]
        public void KademliaDht_AddPeer_UpdatesRoutingTable()
        {
            var dht = new KademliaDht();

            var peerId = new byte[32];
            Random.Shared.NextBytes(peerId);

            var peer = new PeerInfo
            {
                PeerId = peerId,
                Addresses = new List<IPEndPoint> { new IPEndPoint(IPAddress.Loopback, 10333) },
                DiscoverySource = "test"
            };

            dht.AddPeer(peer);

            Assert.AreEqual(1, dht.RoutingTableSize);
        }

        [TestMethod]
        [TestCategory("DHT")]
        public void KademliaDht_AddPeer_IgnoresSelf()
        {
            var nodeId = new byte[32];
            Random.Shared.NextBytes(nodeId);

            var options = new KademliaDhtOptions { NodeId = nodeId };
            var dht = new KademliaDht(options);

            var selfPeer = new PeerInfo
            {
                PeerId = nodeId, // Same as local node
                Addresses = new List<IPEndPoint> { new IPEndPoint(IPAddress.Loopback, 10333) }
            };

            dht.AddPeer(selfPeer);

            Assert.AreEqual(0, dht.RoutingTableSize); // Should not add self
        }

        [TestMethod]
        [TestCategory("DHT")]
        public void KademliaDht_RemovePeer_UpdatesRoutingTable()
        {
            var dht = new KademliaDht();

            var peerId = new byte[32];
            Random.Shared.NextBytes(peerId);

            var peer = new PeerInfo
            {
                PeerId = peerId,
                Addresses = new List<IPEndPoint> { new IPEndPoint(IPAddress.Loopback, 10333) }
            };

            dht.AddPeer(peer);
            Assert.AreEqual(1, dht.RoutingTableSize);

            dht.RemovePeer(peerId);
            Assert.AreEqual(0, dht.RoutingTableSize);
        }

        [TestMethod]
        [TestCategory("DHT")]
        public async Task KademliaDht_FindPeersAsync_ReturnsEmpty_WhenNoRpcService()
        {
            var dht = new KademliaDht();

            var peers = await dht.FindPeersAsync(10, TestContext.CancellationTokenSource.Token);

            Assert.IsNotNull(peers);
            Assert.AreEqual(0, System.Linq.Enumerable.Count(peers));
        }

        [TestMethod]
        [TestCategory("DHT")]
        public void KademliaDhtOptions_HasSensibleDefaults()
        {
            var options = new KademliaDhtOptions();

            Assert.IsNull(options.NodeId); // Will be generated
            Assert.AreEqual(0, options.BootstrapNodes.Count);
            Assert.AreEqual(TimeSpan.FromMinutes(10), options.RefreshInterval);
            Assert.AreEqual(TimeSpan.FromHours(1), options.BucketRefreshInterval);
            Assert.AreEqual(TimeSpan.FromMinutes(15), options.PeerTimeout);
            Assert.AreEqual(TimeSpan.FromHours(24), options.ProviderRecordTtl);
        }
    }
}
