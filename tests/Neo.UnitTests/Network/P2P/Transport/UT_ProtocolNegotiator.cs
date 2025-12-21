// Copyright (C) 2015-2025 The Neo Project.
//
// UT_ProtocolNegotiator.cs file belongs to the neo project and is free
// software distributed under the MIT software license.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.Network.P2P.Transport;
using System.Net;
using System.Threading.Tasks;

namespace Neo.UnitTests.Network.P2P.Transport
{
    [TestClass]
    public class UT_ProtocolNegotiator
    {
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

            var result = await negotiator.ConnectAsync(tcpEndPoint, quicEndPoint, default);

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

            var result = await negotiator.ConnectAsync(tcpEndPoint, quicEndPoint: null, default);

            Assert.AreEqual(TransportProtocol.Tcp, result.Protocol);
            Assert.AreEqual(tcpEndPoint, result.RemoteEndPoint);
        }

        [TestMethod]
        public void TestPeerCapabilities_Flags()
        {
            var caps = PeerCapabilities.Tcp | PeerCapabilities.Quic | PeerCapabilities.FullNode;

            Assert.IsTrue(caps.HasFlag(PeerCapabilities.Tcp));
            Assert.IsTrue(caps.HasFlag(PeerCapabilities.Quic));
            Assert.IsTrue(caps.HasFlag(PeerCapabilities.FullNode));
            Assert.IsFalse(caps.HasFlag(PeerCapabilities.ArchivalNode));
            Assert.IsFalse(caps.HasFlag(PeerCapabilities.Compression));
        }

        [TestMethod]
        public void TestTransportProtocol_Values()
        {
            Assert.AreEqual(0, (int)TransportProtocol.Tcp);
            Assert.AreEqual(1, (int)TransportProtocol.Quic);
        }

        [TestMethod]
        public void TestConnectionResult_IsQuic_Property()
        {
            var tcpResult = new ConnectionResult
            {
                Protocol = TransportProtocol.Tcp,
                QuicConnection = null,
                RemoteEndPoint = new IPEndPoint(IPAddress.Loopback, 10333)
            };

            var quicResult = new ConnectionResult
            {
                Protocol = TransportProtocol.Quic,
                QuicConnection = null, // Would be non-null in real scenario
                RemoteEndPoint = new IPEndPoint(IPAddress.Loopback, 10334)
            };

            Assert.IsFalse(tcpResult.IsQuic);
            Assert.IsTrue(quicResult.IsQuic);
        }
    }
}
