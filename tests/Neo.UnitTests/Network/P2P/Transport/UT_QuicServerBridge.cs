// Copyright (C) 2015-2025 The Neo Project.
//
// UT_QuicServerBridge.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Akka.Actor;
using Akka.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.Network.P2P.Transport;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;

namespace Neo.UnitTests.Network.P2P.Transport
{
    [TestClass]
    [SupportedOSPlatform("linux")]
    [SupportedOSPlatform("windows")]
    [SupportedOSPlatform("osx")]
    public class UT_QuicServerBridge
    {
        public TestContext TestContext { get; set; } = null!;

        private sealed class TestTarget : UntypedActor
        {
            private readonly TaskCompletionSource<byte[]> _tcs;
            public TestTarget(TaskCompletionSource<byte[]> tcs) { _tcs = tcs; }
            protected override void OnReceive(object message)
            {
                switch (message)
                {
                    case Neo.P2P.Abstractions.DataReceived d:
                        _tcs.TrySetResult(d.Data);
                        break;
                    case Tcp.Received r:
                        _tcs.TrySetResult(r.Data.ToArray());
                        break;
                }
            }
        }

        private sealed class FakeQuicConnection : IQuicConnection
        {
#nullable enable
            public event Func<ReadOnlyMemory<byte>, Task>? OnMessageReceived;
            public event Action? OnDisconnected;
#nullable disable

            public Task StartReceivingAsync(CancellationToken cancellationToken = default)
            {
                // No background loop for the test
                return Task.CompletedTask;
            }

            public Task SendAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default)
            {
                // No-op for this test
                return Task.CompletedTask;
            }

            public async Task RaiseAsync(ReadOnlyMemory<byte> data)
            {
                var handler = OnMessageReceived;
                if (handler != null)
                    await handler.Invoke(data);
            }

            public ValueTask DisposeAsync()
            {
                OnDisconnected?.Invoke();
                return ValueTask.CompletedTask;
            }
        }

        [TestMethod]
        [TestCategory("Bridge")]
        public async Task QuicServerConnection_Forwards_To_Target()
        {
            using var system = ActorSystem.Create("test-quic");
            var tcs = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
            var target = system.ActorOf(Props.Create(() => new TestTarget(tcs)));

            var bridge = system.ActorOf(Props.Create(() => new QuicServerConnection()));
            bridge.Tell(new Neo.P2P.Abstractions.BridgeBind(target));

            var fake = new FakeQuicConnection();
            bridge.Tell(new QuicServerConnection.Start(fake));
            // Allow actor to process Start and attach handler
            await Task.Delay(TimeSpan.FromMilliseconds(50), TestContext.CancellationTokenSource.Token);

            var payload = new byte[] { 0xCA, 0xFE, 0xBA, 0xBE };
            await fake.RaiseAsync(payload);

            var received = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(3), TestContext.CancellationTokenSource.Token));
            Assert.AreEqual(tcs.Task, received, "Did not receive DataReceived on target in time");
            CollectionAssert.AreEqual(payload, tcs.Task.Result);

            await system.Terminate();
        }

        [TestMethod]
        [TestCategory("Bridge")]
        public async Task QuicServerConnection_Buffers_Before_Bind()
        {
            using var system = ActorSystem.Create("test-quic-buffer");
            var tcs = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
            var target = system.ActorOf(Props.Create(() => new TestTarget(tcs)));

            var bridge = system.ActorOf(Props.Create(() => new QuicServerConnection()));

            var fake = new FakeQuicConnection();
            bridge.Tell(new QuicServerConnection.Start(fake));
            await Task.Delay(TimeSpan.FromMilliseconds(50), TestContext.CancellationTokenSource.Token);

            // Send data BEFORE bind
            var payload = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF };
            await fake.RaiseAsync(payload);

            // Now bind - should flush buffered data
            bridge.Tell(new Neo.P2P.Abstractions.BridgeBind(target));

            var received = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(3), TestContext.CancellationTokenSource.Token));
            Assert.AreEqual(tcs.Task, received, "Did not receive buffered DataReceived on target in time");
            CollectionAssert.AreEqual(payload, tcs.Task.Result);

            await system.Terminate();
        }
    }
}
