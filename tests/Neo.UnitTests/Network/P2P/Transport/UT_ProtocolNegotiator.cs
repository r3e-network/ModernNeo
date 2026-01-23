// Copyright (C) 2015-2025 The Neo Project.
//
// UT_ProtocolNegotiator.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.Network.P2P.Transport;
using System.Net;
using System.Threading.Tasks;

namespace Neo.UnitTests.Network.P2P.Transport
{
    [TestClass]
    public class UT_ProtocolNegotiator
    {
        public TestContext TestContext { get; set; } = null!;
        [TestMethod]
        public void TestQuicAvailable_WithoutTransport_ReturnsFalse()
        {
            var negotiator = new ProtocolNegotiator(quicTransport: null);
            Assert.IsFalse(negotiator.QuicAvailable);
        }

        [TestMethod]
        public void TestRecommendProtocol_NoQuic_ReturnsTcp()
        {
            var negotiator = new ProtocolNegotiator(quicTransport: null);
            var result = negotiator.RecommendProtocol(PeerCapabilities.Tcp | PeerCapabilities.Quic);
            Assert.AreEqual(TransportProtocol.Tcp, result);
        }

        [TestMethod]
        public void TestRecommendProtocol_WebSocket_ReturnsWebSocket()
        {
            var negotiator = new ProtocolNegotiator(quicTransport: null);
            var result = negotiator.RecommendProtocol(PeerCapabilities.Tcp | PeerCapabilities.WebSocket);
            Assert.AreEqual(TransportProtocol.WebSocket, result);
        }

        [TestMethod]
        public void TestRecommendProtocol_TcpOnly_ReturnsTcp()
        {
            var negotiator = new ProtocolNegotiator(quicTransport: null);
            var result = negotiator.RecommendProtocol(PeerCapabilities.Tcp);
            Assert.AreEqual(TransportProtocol.Tcp, result);
        }

        [TestMethod]
        public async Task TestConnectAsync_NoQuic_FallsBackToTcp()
        {
            var negotiator = new ProtocolNegotiator(quicTransport: null);
            var tcpEndPoint = new IPEndPoint(IPAddress.Loopback, 10333);
            var quicEndPoint = new IPEndPoint(IPAddress.Loopback, 10334);

            var result = await negotiator.ConnectAsync(tcpEndPoint, quicEndPoint, wsUri: null, TestContext!.CancellationTokenSource.Token);

            Assert.AreEqual(TransportProtocol.Tcp, result.Protocol);
            Assert.IsNull(result.QuicConnection);
            Assert.AreEqual(tcpEndPoint, result.RemoteEndPoint);
            Assert.IsFalse(result.IsQuic);
        }

        [TestMethod]
        public async Task TestConnectAsync_NoQuicEndPoint_ReturnsTcp()
        {
            var negotiator = new ProtocolNegotiator(quicTransport: null);
            var tcpEndPoint = new IPEndPoint(IPAddress.Loopback, 10333);

            var result = await negotiator.ConnectAsync(tcpEndPoint, quicEndPoint: null, wsUri: null, TestContext!.CancellationTokenSource.Token);

            Assert.AreEqual(TransportProtocol.Tcp, result.Protocol);
            Assert.AreEqual(tcpEndPoint, result.RemoteEndPoint);
        }

        [TestMethod]
        public void TestPeerCapabilities_Flags()
        {
            var caps = PeerCapabilities.Tcp | PeerCapabilities.Quic | PeerCapabilities.FullNode | PeerCapabilities.WebSocket;

            Assert.IsTrue(caps.HasFlag(PeerCapabilities.Tcp));
            Assert.IsTrue(caps.HasFlag(PeerCapabilities.Quic));
            Assert.IsTrue(caps.HasFlag(PeerCapabilities.FullNode));
            Assert.IsTrue(caps.HasFlag(PeerCapabilities.WebSocket));
            Assert.IsFalse(caps.HasFlag(PeerCapabilities.ArchivalNode));
            Assert.IsFalse(caps.HasFlag(PeerCapabilities.Compression));
        }

        [TestMethod]
        public void TestTransportProtocol_Values()
        {
            Assert.AreEqual(0, (int)TransportProtocol.Tcp);
            Assert.AreEqual(1, (int)TransportProtocol.Quic);
            Assert.AreEqual(2, (int)TransportProtocol.WebSocket);
        }

        [TestMethod]
        public void TestConnectionResult_IsQuic_Property()
        {
            var tcpResult = new ConnectionResult
            {
                Protocol = TransportProtocol.Tcp,
                QuicConnection = null,
                WsConnection = null,
                RemoteEndPoint = new IPEndPoint(IPAddress.Loopback, 10333)
            };

            var quicResult = new ConnectionResult
            {
                Protocol = TransportProtocol.Quic,
                QuicConnection = null, // Would be non-null in real scenario
                WsConnection = null,
                RemoteEndPoint = new IPEndPoint(IPAddress.Loopback, 10334)
            };

            var wsResult = new ConnectionResult
            {
                Protocol = TransportProtocol.WebSocket,
                QuicConnection = null,
                WsConnection = null, // Would be non-null in real scenario
                RemoteEndPoint = new IPEndPoint(IPAddress.Loopback, 10335)
            };

            Assert.IsFalse(tcpResult.IsQuic);
            Assert.IsTrue(quicResult.IsQuic);
            Assert.IsFalse(tcpResult.IsWebSocket);
            Assert.IsTrue(wsResult.IsWebSocket);
        }
    }
}
