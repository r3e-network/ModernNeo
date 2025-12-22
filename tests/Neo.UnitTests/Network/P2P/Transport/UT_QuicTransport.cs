// Copyright (C) 2015-2025 The Neo Project.
//
// UT_QuicTransport.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.Network.P2P.Transport;
using System;
using System.Net;
using System.Runtime.Versioning;
using System.Threading.Tasks;

namespace Neo.UnitTests.Network.P2P.Transport
{
    [TestClass]
    public class UT_QuicTransport
    {
        [TestMethod]
        public void TestIsSupported_ReturnsBoolean()
        {
            // IsSupported should return a boolean without throwing
            var isSupported = QuicTransport.IsSupported;
            Assert.IsInstanceOfType(isSupported, typeof(bool));
        }

        [TestMethod]
        public void TestQuicTransportOptions_DefaultValues()
        {
            var options = new QuicTransportOptions();

            Assert.AreEqual(IPAddress.Any, options.ListenEndPoint.Address);
            Assert.AreEqual(10334, options.ListenEndPoint.Port);
            Assert.AreEqual("neo-p2p", options.ApplicationProtocol);
            Assert.IsNull(options.Certificate);
            Assert.IsNull(options.RemoteCertificateValidationCallback);
            Assert.AreEqual(100, options.MaxConnections);
            Assert.AreEqual(TimeSpan.FromMinutes(2), options.IdleTimeout);
        }

        [TestMethod]
        public void TestQuicTransportOptions_CustomValues()
        {
            var customEndPoint = new IPEndPoint(IPAddress.Loopback, 12345);
            var options = new QuicTransportOptions
            {
                ListenEndPoint = customEndPoint,
                ApplicationProtocol = "custom-protocol",
                MaxConnections = 50,
                IdleTimeout = TimeSpan.FromMinutes(5)
            };

            Assert.AreEqual(customEndPoint, options.ListenEndPoint);
            Assert.AreEqual("custom-protocol", options.ApplicationProtocol);
            Assert.AreEqual(50, options.MaxConnections);
            Assert.AreEqual(TimeSpan.FromMinutes(5), options.IdleTimeout);
        }

        [TestMethod]
        [SupportedOSPlatform("linux")]
        [SupportedOSPlatform("windows")]
        [SupportedOSPlatform("osx")]
        public void TestQuicTransport_Constructor_NullOptions_Throws()
        {
            Assert.ThrowsExactly<ArgumentNullException>(() => new QuicTransport(null!));
        }

        [TestMethod]
        [SupportedOSPlatform("linux")]
        [SupportedOSPlatform("windows")]
        [SupportedOSPlatform("osx")]
        public void TestQuicTransport_Constructor_ValidOptions_Succeeds()
        {
            var options = new QuicTransportOptions();
            var transport = new QuicTransport(options);

            Assert.IsNotNull(transport);
            Assert.AreEqual(0, transport.ConnectionCount);
        }

        [TestMethod]
        [SupportedOSPlatform("linux")]
        [SupportedOSPlatform("windows")]
        [SupportedOSPlatform("osx")]
        public void TestQuicTransport_ConnectionCount_InitiallyZero()
        {
            var options = new QuicTransportOptions();
            var transport = new QuicTransport(options);

            Assert.AreEqual(0, transport.ConnectionCount);
        }

        [TestMethod]
        [SupportedOSPlatform("linux")]
        [SupportedOSPlatform("windows")]
        [SupportedOSPlatform("osx")]
        public async Task TestQuicTransport_StartAsync_UnsupportedPlatform_Throws()
        {
            if (QuicTransport.IsSupported)
            {
                // Skip test on supported platforms - would need certificate
                Assert.Inconclusive("QUIC is supported on this platform, skipping unsupported test");
                return;
            }

            var options = new QuicTransportOptions();
            var transport = new QuicTransport(options);

            await Assert.ThrowsExactlyAsync<PlatformNotSupportedException>(
                async () => await transport.StartAsync(default));
        }

        [TestMethod]
        [SupportedOSPlatform("linux")]
        [SupportedOSPlatform("windows")]
        [SupportedOSPlatform("osx")]
        public async Task TestQuicTransport_ConnectAsync_UnsupportedPlatform_Throws()
        {
            if (QuicTransport.IsSupported)
            {
                // Skip test on supported platforms
                Assert.Inconclusive("QUIC is supported on this platform, skipping unsupported test");
                return;
            }

            var options = new QuicTransportOptions();
            var transport = new QuicTransport(options);
            var endpoint = new IPEndPoint(IPAddress.Loopback, 10334);

            await Assert.ThrowsExactlyAsync<PlatformNotSupportedException>(
                async () => await transport.ConnectAsync(endpoint, default));
        }

        [TestMethod]
        [SupportedOSPlatform("linux")]
        [SupportedOSPlatform("windows")]
        [SupportedOSPlatform("osx")]
        public async Task TestQuicTransport_DisposeAsync_Succeeds()
        {
            var options = new QuicTransportOptions();
            var transport = new QuicTransport(options);

            // Should not throw
            await transport.DisposeAsync();
        }

        [TestMethod]
        [SupportedOSPlatform("linux")]
        [SupportedOSPlatform("windows")]
        [SupportedOSPlatform("osx")]
        public async Task TestQuicTransport_StopAsync_WithoutStart_Succeeds()
        {
            var options = new QuicTransportOptions();
            var transport = new QuicTransport(options);

            // Should not throw even if not started
            await transport.StopAsync();
        }
    }
}
