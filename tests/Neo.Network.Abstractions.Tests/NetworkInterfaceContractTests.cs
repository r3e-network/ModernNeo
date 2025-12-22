// Copyright (C) 2015-2025 The Neo Project.
//
// NetworkInterfaceContractTests.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.Network.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace Neo.Network.Abstractions.Tests
{
    [TestClass]
    public class NetworkInterfaceContractTests
    {
        #region ITransport Tests

        [TestMethod]
        public void ITransport_HasRequiredMembers()
        {
            var type = typeof(ITransport);

            // Properties
            Assert.IsNotNull(type.GetProperty("Type"));
            Assert.IsNotNull(type.GetProperty("IsListening"));

            // Methods
            Assert.IsNotNull(type.GetMethod("StartListeningAsync"));
            Assert.IsNotNull(type.GetMethod("StopListeningAsync"));
            Assert.IsNotNull(type.GetMethod("ConnectAsync"));

            // Events
            Assert.IsNotNull(type.GetEvent("ConnectionAccepted"));
        }

        [TestMethod]
        public void ITransport_InheritsIAsyncDisposable()
        {
            Assert.IsTrue(typeof(IAsyncDisposable).IsAssignableFrom(typeof(ITransport)));
        }

        [TestMethod]
        public void TransportType_HasExpectedValues()
        {
            Assert.AreEqual(0, (int)TransportType.Tcp);
            Assert.AreEqual(1, (int)TransportType.Quic);
            Assert.AreEqual(2, (int)TransportType.WebSocket);
        }

        #endregion

        #region IPeerConnection Tests

        [TestMethod]
        public void IPeerConnection_HasRequiredMembers()
        {
            var type = typeof(IPeerConnection);

            // Properties
            Assert.IsNotNull(type.GetProperty("ConnectionId"));
            Assert.IsNotNull(type.GetProperty("RemoteEndPoint"));
            Assert.IsNotNull(type.GetProperty("LocalEndPoint"));
            Assert.IsNotNull(type.GetProperty("IsConnected"));
            Assert.IsNotNull(type.GetProperty("TransportType"));

            // Methods
            Assert.IsNotNull(type.GetMethod("SendAsync"));
            Assert.IsNotNull(type.GetMethod("ReceiveAsync"));
            Assert.IsNotNull(type.GetMethod("CloseAsync"));

            // Events
            Assert.IsNotNull(type.GetEvent("DataReceived"));
            Assert.IsNotNull(type.GetEvent("ConnectionClosed"));
        }

        [TestMethod]
        public void IPeerConnection_InheritsIAsyncDisposable()
        {
            Assert.IsTrue(typeof(IAsyncDisposable).IsAssignableFrom(typeof(IPeerConnection)));
        }

        [TestMethod]
        public void DataReceivedEventArgs_CanBeConstructed()
        {
            var data = new byte[] { 1, 2, 3 };
            var args = new DataReceivedEventArgs(data);

            Assert.AreEqual(3, args.Data.Length);
            Assert.AreEqual(1, args.Data.Span[0]);
        }

        [TestMethod]
        public void ConnectionClosedEventArgs_CanBeConstructed()
        {
            var args = new ConnectionClosedEventArgs("Test reason", true);

            Assert.AreEqual("Test reason", args.Reason);
            Assert.IsTrue(args.IsLocal);
        }

        [TestMethod]
        public void ConnectionClosedEventArgs_DefaultValues()
        {
            var args = new ConnectionClosedEventArgs();

            Assert.IsNull(args.Reason);
            Assert.IsFalse(args.IsLocal);
        }

        #endregion

        #region IPeerDiscovery Tests

        [TestMethod]
        public void IPeerDiscovery_HasRequiredMembers()
        {
            var type = typeof(IPeerDiscovery);

            // Properties
            Assert.IsNotNull(type.GetProperty("Name"));
            Assert.IsNotNull(type.GetProperty("IsActive"));

            // Methods
            Assert.IsNotNull(type.GetMethod("StartAsync"));
            Assert.IsNotNull(type.GetMethod("StopAsync"));
            Assert.IsNotNull(type.GetMethod("DiscoverPeersAsync"));
            Assert.IsNotNull(type.GetMethod("AnnounceAsync"));

            // Events
            Assert.IsNotNull(type.GetEvent("PeersDiscovered"));
        }

        [TestMethod]
        public void IPeerDiscovery_InheritsIAsyncDisposable()
        {
            Assert.IsTrue(typeof(IAsyncDisposable).IsAssignableFrom(typeof(IPeerDiscovery)));
        }

        [TestMethod]
        public void PeersDiscoveredEventArgs_CanBeConstructed()
        {
            var peers = new List<IPEndPoint>
            {
                new IPEndPoint(IPAddress.Loopback, 10333),
                new IPEndPoint(IPAddress.Loopback, 10334)
            };
            var args = new PeersDiscoveredEventArgs(peers, "DNS");

            Assert.AreEqual(2, args.Peers.Count);
            Assert.AreEqual("DNS", args.Source);
        }

        [TestMethod]
        public void PeersDiscoveredEventArgs_ThrowsOnNullPeers()
        {
            Assert.ThrowsExactly<ArgumentNullException>(() => new PeersDiscoveredEventArgs(null!, "DNS"));
        }

        [TestMethod]
        public void PeersDiscoveredEventArgs_ThrowsOnNullSource()
        {
            Assert.ThrowsExactly<ArgumentNullException>(() => new PeersDiscoveredEventArgs(new List<IPEndPoint>(), null!));
        }

        #endregion

        #region IMessageCodec Tests

        [TestMethod]
        public void IMessageCodec_HasRequiredMembers()
        {
            var type = typeof(IMessageCodec);

            // Methods
            Assert.IsNotNull(type.GetMethod("Encode"));
            Assert.IsNotNull(type.GetMethod("TryDecode"));
        }

        [TestMethod]
        public void INetworkMessage_HasRequiredMembers()
        {
            var type = typeof(INetworkMessage);

            // Properties
            Assert.IsNotNull(type.GetProperty("Command"));
            Assert.IsNotNull(type.GetProperty("Payload"));
            Assert.IsNotNull(type.GetProperty("Flags"));
        }

        [TestMethod]
        public void MessageFlags_HasExpectedValues()
        {
            Assert.AreEqual(0, (int)MessageFlags.None);
            Assert.AreEqual(1, (int)MessageFlags.Compressed);
        }

        [TestMethod]
        public void MessageFlags_IsFlagsEnum()
        {
            Assert.IsTrue(typeof(MessageFlags).GetCustomAttributes(typeof(FlagsAttribute), false).Any());
        }

        #endregion

        #region ILocalNodeService Tests

        [TestMethod]
        public void ILocalNodeService_HasRequiredMembers()
        {
            var type = typeof(ILocalNodeService);

            // Properties
            Assert.IsNotNull(type.GetProperty("ConnectedCount"));
            Assert.IsNotNull(type.GetProperty("UnconnectedCount"));
            Assert.IsNotNull(type.GetProperty("MaxConnections"));

            // Methods
            Assert.IsNotNull(type.GetMethod("GetConnectedPeers"));
            Assert.IsNotNull(type.GetMethod("BroadcastAsync"));
            Assert.IsNotNull(type.GetMethod("RelayAsync"));
            Assert.IsNotNull(type.GetMethod("SendToPeerAsync"));
            Assert.IsNotNull(type.GetMethod("DisconnectPeerAsync"));

            // Events
            Assert.IsNotNull(type.GetEvent("PeerConnected"));
            Assert.IsNotNull(type.GetEvent("PeerDisconnected"));
            Assert.IsNotNull(type.GetEvent("MessageReceived"));
        }

        [TestMethod]
        public void IPeerInfo_HasRequiredMembers()
        {
            var type = typeof(IPeerInfo);

            Assert.IsNotNull(type.GetProperty("PeerId"));
            Assert.IsNotNull(type.GetProperty("Address"));
            Assert.IsNotNull(type.GetProperty("UserAgent"));
            Assert.IsNotNull(type.GetProperty("Version"));
            Assert.IsNotNull(type.GetProperty("LastBlockIndex"));
            Assert.IsNotNull(type.GetProperty("ConnectedAt"));
        }

        [TestMethod]
        public void PeerConnectedEventArgs_CanBeConstructed()
        {
            var peer = new TestPeerInfo();
            var args = new PeerConnectedEventArgs(peer);

            Assert.AreSame(peer, args.Peer);
        }

        [TestMethod]
        public void PeerConnectedEventArgs_ThrowsOnNullPeer()
        {
            Assert.ThrowsExactly<ArgumentNullException>(() => new PeerConnectedEventArgs(null!));
        }

        [TestMethod]
        public void PeerDisconnectedEventArgs_CanBeConstructed()
        {
            var args = new PeerDisconnectedEventArgs("peer-123", "Timeout");

            Assert.AreEqual("peer-123", args.PeerId);
            Assert.AreEqual("Timeout", args.Reason);
        }

        [TestMethod]
        public void PeerDisconnectedEventArgs_ThrowsOnNullPeerId()
        {
            Assert.ThrowsExactly<ArgumentNullException>(() => new PeerDisconnectedEventArgs(null!));
        }

        [TestMethod]
        public void PeerMessageReceivedEventArgs_CanBeConstructed()
        {
            var message = new TestNetworkMessage();
            var args = new PeerMessageReceivedEventArgs("peer-123", message);

            Assert.AreEqual("peer-123", args.PeerId);
            Assert.AreSame(message, args.Message);
        }

        [TestMethod]
        public void PeerMessageReceivedEventArgs_ThrowsOnNullPeerId()
        {
            Assert.ThrowsExactly<ArgumentNullException>(() => new PeerMessageReceivedEventArgs(null!, new TestNetworkMessage()));
        }

        [TestMethod]
        public void PeerMessageReceivedEventArgs_ThrowsOnNullMessage()
        {
            Assert.ThrowsExactly<ArgumentNullException>(() => new PeerMessageReceivedEventArgs("peer-123", null!));
        }

        #endregion

        #region ConnectionAcceptedEventArgs Tests

        [TestMethod]
        public void ConnectionAcceptedEventArgs_CanBeConstructed()
        {
            var connection = new TestPeerConnection();
            var args = new ConnectionAcceptedEventArgs(connection);

            Assert.AreSame(connection, args.Connection);
        }

        [TestMethod]
        public void ConnectionAcceptedEventArgs_ThrowsOnNullConnection()
        {
            Assert.ThrowsExactly<ArgumentNullException>(() => new ConnectionAcceptedEventArgs(null!));
        }

        #endregion

        #region Test Helpers

        private class TestPeerInfo : IPeerInfo
        {
            public string PeerId => "test-peer";
            public string Address => "127.0.0.1:10333";
            public string UserAgent => "/Neo:3.0.0/";
            public uint Version => 0;
            public uint LastBlockIndex => 0;
            public DateTimeOffset ConnectedAt => DateTimeOffset.UtcNow;
        }

        private class TestNetworkMessage : INetworkMessage
        {
            public string Command => "test";
            public ReadOnlyMemory<byte> Payload => Array.Empty<byte>();
            public MessageFlags Flags => MessageFlags.None;
        }

        private class TestPeerConnection : IPeerConnection
        {
            public string ConnectionId => "test-connection";
            public IPEndPoint RemoteEndPoint => new IPEndPoint(IPAddress.Loopback, 10333);
            public IPEndPoint LocalEndPoint => new IPEndPoint(IPAddress.Loopback, 10334);
            public bool IsConnected => true;
            public TransportType TransportType => TransportType.Tcp;

#pragma warning disable CS0067 // Event is never used - required by interface
            public event EventHandler<DataReceivedEventArgs>? DataReceived;
            public event EventHandler<ConnectionClosedEventArgs>? ConnectionClosed;
#pragma warning restore CS0067

            public Task SendAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default) => Task.CompletedTask;
            public Task<int> ReceiveAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) => Task.FromResult(0);
            public Task CloseAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }

        #endregion
    }
}
