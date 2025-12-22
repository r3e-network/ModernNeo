// Copyright (C) 2015-2025 The Neo Project.
//
// UT_WsTransportService.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.Orleans.Services;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Neo.UnitTests.Orleans.Services
{
    [TestClass]
    public class UT_WsTransportService
    {
        public TestContext TestContext { get; set; } = null!;

        [TestMethod]
        [TestCategory("Transport")]
        public async Task WsTransportService_CanBeCreated()
        {
            await using var service = new WsTransportService();
            Assert.IsNotNull(service);
        }

        [TestMethod]
        [TestCategory("Transport")]
        public async Task WsTransportService_WithCustomTimeouts()
        {
            await using var service = new WsTransportService(
                connectTimeout: TimeSpan.FromSeconds(5),
                sendTimeout: TimeSpan.FromSeconds(3));
            Assert.IsNotNull(service);
        }

        [TestMethod]
        [TestCategory("Transport")]
        public async Task IsConnected_ReturnsFalse_WhenNotConnected()
        {
            await using var service = new WsTransportService();

            var result = service.IsConnected("127.0.0.1", 10333);

            Assert.IsFalse(result);
        }

        [TestMethod]
        [TestCategory("Transport")]
        public async Task ConnectAsync_ReturnsFalse_WhenNoServer()
        {
            await using var service = new WsTransportService(
                connectTimeout: TimeSpan.FromMilliseconds(100));

            // Try to connect to a non-existent server
            var result = await service.ConnectAsync("127.0.0.1", 59999, TestContext.CancellationTokenSource.Token);

            Assert.IsFalse(result);
        }

        [TestMethod]
        [TestCategory("Transport")]
        public async Task SendAsync_ReturnsFalse_WhenNotConnected()
        {
            await using var service = new WsTransportService(
                connectTimeout: TimeSpan.FromMilliseconds(100),
                sendTimeout: TimeSpan.FromMilliseconds(100));

            var message = new byte[] { 0x01, 0x02, 0x03 };
            var result = await service.SendAsync("127.0.0.1", 59999, message, TestContext.CancellationTokenSource.Token);

            Assert.IsFalse(result);
        }

        [TestMethod]
        [TestCategory("Transport")]
        public async Task DisconnectAsync_DoesNotThrow_WhenNotConnected()
        {
            await using var service = new WsTransportService();

            // Should not throw even if not connected
            await service.DisconnectAsync("127.0.0.1", 10333, TestContext.CancellationTokenSource.Token);
        }

        [TestMethod]
        [TestCategory("Transport")]
        public async Task ConnectAsync_ReturnsFalse_WhenCancelled()
        {
            await using var service = new WsTransportService();
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            var result = await service.ConnectAsync("127.0.0.1", 10333, cts.Token);

            Assert.IsFalse(result);
        }

        [TestMethod]
        [TestCategory("Transport")]
        public async Task SendAsync_ReturnsFalse_WhenCancelled()
        {
            await using var service = new WsTransportService();
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            var message = new byte[] { 0x01, 0x02, 0x03 };
            var result = await service.SendAsync("127.0.0.1", 10333, message, cts.Token);

            Assert.IsFalse(result);
        }

        [TestMethod]
        [TestCategory("Transport")]
        public async Task DisposeAsync_CanBeCalledMultipleTimes()
        {
            var service = new WsTransportService();

            await service.DisposeAsync();
            await service.DisposeAsync(); // Should not throw
        }

        [TestMethod]
        [TestCategory("Transport")]
        public async Task SendAsync_ReturnsFalse_AfterDispose()
        {
            var service = new WsTransportService();
            await service.DisposeAsync();

            var message = new byte[] { 0x01, 0x02, 0x03 };
            var result = await service.SendAsync("127.0.0.1", 10333, message, TestContext.CancellationTokenSource.Token);

            Assert.IsFalse(result);
        }

        [TestMethod]
        [TestCategory("Transport")]
        public async Task ConnectAsync_ReturnsFalse_AfterDispose()
        {
            var service = new WsTransportService();
            await service.DisposeAsync();

            var result = await service.ConnectAsync("127.0.0.1", 10333, TestContext.CancellationTokenSource.Token);

            Assert.IsFalse(result);
        }

        [TestMethod]
        [TestCategory("Transport")]
        public void ITransportService_Interface_HasRequiredMethods()
        {
            var interfaceType = typeof(ITransportService);

            Assert.IsNotNull(interfaceType.GetMethod("SendAsync"));
            Assert.IsNotNull(interfaceType.GetMethod("ConnectAsync"));
            Assert.IsNotNull(interfaceType.GetMethod("DisconnectAsync"));
            Assert.IsNotNull(interfaceType.GetMethod("IsConnected"));
        }

        [TestMethod]
        [TestCategory("Transport")]
        public void WsTransportService_ImplementsITransportService()
        {
            Assert.IsTrue(typeof(ITransportService).IsAssignableFrom(typeof(WsTransportService)));
        }

        [TestMethod]
        [TestCategory("Transport")]
        public void WsTransportService_ImplementsIAsyncDisposable()
        {
            Assert.IsTrue(typeof(IAsyncDisposable).IsAssignableFrom(typeof(WsTransportService)));
        }
    }
}
