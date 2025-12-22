// Copyright (C) 2015-2025 The Neo Project.
//
// UT_PluginLifecycle.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

#nullable enable
#pragma warning disable MSTEST0039 // Use 'Assert.ThrowsExactly' instead of 'Assert.ThrowsException'
#pragma warning disable MSTEST0049 // Use 'CancellationToken' parameter

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.Plugins;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Neo.UnitTests.Plugins
{
    [TestClass]
    public class UT_PluginLifecycle
    {
        #region Test Plugin Implementations

        private sealed class LifecycleTrackingPlugin : PluginBase
        {
            public override string Name => "LifecycleTracker";
            public override string Description => "Tracks lifecycle events";

            public bool InitializeCalled { get; private set; }
            public bool StartCalled { get; private set; }
            public bool StopCalled { get; private set; }
            public bool ShutdownCalled { get; private set; }
            public bool DisposeCalled { get; private set; }

            public int InitializeCallCount { get; private set; }
            public int StartCallCount { get; private set; }
            public int StopCallCount { get; private set; }
            public int ShutdownCallCount { get; private set; }

            protected override void OnInitialize()
            {
                InitializeCalled = true;
                InitializeCallCount++;
            }

            protected override void OnStart()
            {
                StartCalled = true;
                StartCallCount++;
            }

            protected override void OnStop()
            {
                StopCalled = true;
                StopCallCount++;
            }

            protected override void OnShutdown()
            {
                ShutdownCalled = true;
                ShutdownCallCount++;
            }

            public override async ValueTask DisposeAsync()
            {
                DisposeCalled = true;
                await base.DisposeAsync();
            }
        }

        private sealed class FaultyPlugin : PluginBase
        {
            public override string Name => "FaultyPlugin";

            public bool ThrowOnInitialize { get; set; }
            public bool ThrowOnStart { get; set; }
            public bool ThrowOnStop { get; set; }
            public bool ThrowOnShutdown { get; set; }

            protected override void OnInitialize()
            {
                if (ThrowOnInitialize)
                    throw new InvalidOperationException("Initialize failed");
            }

            protected override void OnStart()
            {
                if (ThrowOnStart)
                    throw new InvalidOperationException("Start failed");
            }

            protected override void OnStop()
            {
                if (ThrowOnStop)
                    throw new InvalidOperationException("Stop failed");
            }

            protected override void OnShutdown()
            {
                if (ThrowOnShutdown)
                    throw new InvalidOperationException("Shutdown failed");
            }
        }

        private sealed class AsyncPlugin : PluginBase
        {
            public override string Name => "AsyncPlugin";

            public TimeSpan InitializeDelay { get; set; } = TimeSpan.Zero;
            public TimeSpan StartDelay { get; set; } = TimeSpan.Zero;
            public TimeSpan StopDelay { get; set; } = TimeSpan.Zero;
            public TimeSpan ShutdownDelay { get; set; } = TimeSpan.Zero;

            public override async Task InitializeAsync(CancellationToken cancellationToken = default)
            {
                State = PluginState.Initializing;
                if (InitializeDelay > TimeSpan.Zero)
                    await Task.Delay(InitializeDelay, cancellationToken);
                State = PluginState.Initialized;
            }

            public override async Task StartAsync(CancellationToken cancellationToken = default)
            {
                State = PluginState.Starting;
                if (StartDelay > TimeSpan.Zero)
                    await Task.Delay(StartDelay, cancellationToken);
                State = PluginState.Running;
            }

            public override async Task StopAsync(CancellationToken cancellationToken = default)
            {
                State = PluginState.Stopping;
                if (StopDelay > TimeSpan.Zero)
                    await Task.Delay(StopDelay, cancellationToken);
                State = PluginState.Stopped;
            }

            public override async Task ShutdownAsync(CancellationToken cancellationToken = default)
            {
                if (ShutdownDelay > TimeSpan.Zero)
                    await Task.Delay(ShutdownDelay, cancellationToken);
                State = PluginState.Unloaded;
            }
        }

        #endregion

        #region Lifecycle State Transition Tests

        [TestMethod]
        public async Task TestPluginLifecycle_FullCycle_StateTransitionsCorrect()
        {
            var plugin = new LifecycleTrackingPlugin();

            Assert.AreEqual(PluginState.Unloaded, plugin.State);

            await plugin.InitializeAsync();
            Assert.AreEqual(PluginState.Initialized, plugin.State);
            Assert.IsTrue(plugin.InitializeCalled);

            await plugin.StartAsync();
            Assert.AreEqual(PluginState.Running, plugin.State);
            Assert.IsTrue(plugin.StartCalled);

            await plugin.StopAsync();
            Assert.AreEqual(PluginState.Stopped, plugin.State);
            Assert.IsTrue(plugin.StopCalled);

            await plugin.ShutdownAsync();
            Assert.AreEqual(PluginState.Unloaded, plugin.State);
            Assert.IsTrue(plugin.ShutdownCalled);
        }

        [TestMethod]
        public async Task TestPluginLifecycle_MultipleInitialize_OnlyCallsOnce()
        {
            var plugin = new LifecycleTrackingPlugin();

            await plugin.InitializeAsync();
            await plugin.InitializeAsync();
            await plugin.InitializeAsync();

            Assert.AreEqual(3, plugin.InitializeCallCount);
        }

        [TestMethod]
        public async Task TestPluginLifecycle_StartWithoutInitialize_Succeeds()
        {
            var plugin = new LifecycleTrackingPlugin();

            await plugin.StartAsync();

            Assert.AreEqual(PluginState.Running, plugin.State);
            Assert.IsTrue(plugin.StartCalled);
            Assert.IsFalse(plugin.InitializeCalled);
        }

        [TestMethod]
        public async Task TestPluginLifecycle_StopWithoutStart_Succeeds()
        {
            var plugin = new LifecycleTrackingPlugin();

            await plugin.StopAsync();

            Assert.AreEqual(PluginState.Stopped, plugin.State);
            Assert.IsTrue(plugin.StopCalled);
        }

        [TestMethod]
        public async Task TestPluginLifecycle_Restart_StateTransitionsCorrect()
        {
            var plugin = new LifecycleTrackingPlugin();

            await plugin.InitializeAsync();
            await plugin.StartAsync();
            await plugin.StopAsync();

            Assert.AreEqual(1, plugin.StartCallCount);
            Assert.AreEqual(1, plugin.StopCallCount);

            await plugin.StartAsync();

            Assert.AreEqual(2, plugin.StartCallCount);
            Assert.AreEqual(PluginState.Running, plugin.State);
        }

        [TestMethod]
        public async Task TestPluginLifecycle_DisposeAsync_CallsDispose()
        {
            var plugin = new LifecycleTrackingPlugin();

            await plugin.InitializeAsync();
            await plugin.StartAsync();
            await plugin.DisposeAsync();

            Assert.IsTrue(plugin.DisposeCalled);
        }

        #endregion

        #region Exception Handling Tests

        [TestMethod]
        public async Task TestPluginLifecycle_InitializeThrows_StateSetToFaulted()
        {
            var plugin = new FaultyPlugin { ThrowOnInitialize = true };

            try
            {
                await plugin.InitializeAsync();
                Assert.Fail("Expected exception was not thrown");
            }
            catch (InvalidOperationException ex)
            {
                Assert.AreEqual("Initialize failed", ex.Message);
            }
        }

        [TestMethod]
        public async Task TestPluginLifecycle_StartThrows_StateSetToFaulted()
        {
            var plugin = new FaultyPlugin { ThrowOnStart = true };

            try
            {
                await plugin.StartAsync();
                Assert.Fail("Expected exception was not thrown");
            }
            catch (InvalidOperationException ex)
            {
                Assert.AreEqual("Start failed", ex.Message);
            }
        }

        [TestMethod]
        public async Task TestPluginLifecycle_StopThrows_ExceptionPropagates()
        {
            var plugin = new FaultyPlugin { ThrowOnStop = true };

            await plugin.InitializeAsync();
            await plugin.StartAsync();

            try
            {
                await plugin.StopAsync();
                Assert.Fail("Expected exception was not thrown");
            }
            catch (InvalidOperationException ex)
            {
                Assert.AreEqual("Stop failed", ex.Message);
            }
        }

        [TestMethod]
        public async Task TestPluginLifecycle_ShutdownThrows_ExceptionPropagates()
        {
            var plugin = new FaultyPlugin { ThrowOnShutdown = true };

            try
            {
                await plugin.ShutdownAsync();
                Assert.Fail("Expected exception was not thrown");
            }
            catch (InvalidOperationException ex)
            {
                Assert.AreEqual("Shutdown failed", ex.Message);
            }
        }

        #endregion

        #region Cancellation Tests

        [TestMethod]
        public async Task TestPluginLifecycle_InitializeCancelled_ThrowsOperationCancelled()
        {
            var plugin = new AsyncPlugin { InitializeDelay = TimeSpan.FromSeconds(10) };
            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

            await Assert.ThrowsExceptionAsync<TaskCanceledException>(async () =>
            {
                await plugin.InitializeAsync(cts.Token);
            });
        }

        [TestMethod]
        public async Task TestPluginLifecycle_StartCancelled_ThrowsOperationCancelled()
        {
            var plugin = new AsyncPlugin { StartDelay = TimeSpan.FromSeconds(10) };
            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

            await Assert.ThrowsExceptionAsync<TaskCanceledException>(async () =>
            {
                await plugin.StartAsync(cts.Token);
            });
        }

        [TestMethod]
        public async Task TestPluginLifecycle_StopCancelled_ThrowsOperationCancelled()
        {
            var plugin = new AsyncPlugin { StopDelay = TimeSpan.FromSeconds(10) };
            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

            await Assert.ThrowsExceptionAsync<TaskCanceledException>(async () =>
            {
                await plugin.StopAsync(cts.Token);
            });
        }

        [TestMethod]
        public async Task TestPluginLifecycle_ShutdownCancelled_ThrowsOperationCancelled()
        {
            var plugin = new AsyncPlugin { ShutdownDelay = TimeSpan.FromSeconds(10) };
            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

            await Assert.ThrowsExceptionAsync<TaskCanceledException>(async () =>
            {
                await plugin.ShutdownAsync(cts.Token);
            });
        }

        #endregion

        #region Concurrent Lifecycle Tests

        [TestMethod]
        public async Task TestPluginLifecycle_ConcurrentInitialize_HandledCorrectly()
        {
            var plugin = new LifecycleTrackingPlugin();

            var tasks = new Task[10];
            for (int i = 0; i < tasks.Length; i++)
            {
                tasks[i] = plugin.InitializeAsync();
            }

            await Task.WhenAll(tasks);

            Assert.AreEqual(10, plugin.InitializeCallCount);
        }

        [TestMethod]
        public async Task TestPluginLifecycle_ConcurrentStartStop_HandledCorrectly()
        {
            var plugin = new LifecycleTrackingPlugin();

            await plugin.InitializeAsync();

            var tasks = new Task[20];
            for (int i = 0; i < 10; i++)
            {
                tasks[i] = plugin.StartAsync();
                tasks[i + 10] = plugin.StopAsync();
            }

            await Task.WhenAll(tasks);

            Assert.IsTrue(plugin.StartCallCount >= 1);
            Assert.IsTrue(plugin.StopCallCount >= 1);
        }

        #endregion

        #region Edge Case Tests

        [TestMethod]
        public async Task TestPluginLifecycle_ShutdownBeforeInitialize_Succeeds()
        {
            var plugin = new LifecycleTrackingPlugin();

            await plugin.ShutdownAsync();

            Assert.AreEqual(PluginState.Unloaded, plugin.State);
            Assert.IsTrue(plugin.ShutdownCalled);
            Assert.IsFalse(plugin.InitializeCalled);
        }

        [TestMethod]
        public async Task TestPluginLifecycle_MultipleShutdown_HandledCorrectly()
        {
            var plugin = new LifecycleTrackingPlugin();

            await plugin.InitializeAsync();
            await plugin.ShutdownAsync();
            await plugin.ShutdownAsync();
            await plugin.ShutdownAsync();

            Assert.AreEqual(3, plugin.ShutdownCallCount);
        }

        [TestMethod]
        public async Task TestPluginLifecycle_StartAfterShutdown_Succeeds()
        {
            var plugin = new LifecycleTrackingPlugin();

            await plugin.InitializeAsync();
            await plugin.StartAsync();
            await plugin.ShutdownAsync();

            await plugin.StartAsync();

            Assert.AreEqual(PluginState.Running, plugin.State);
            Assert.AreEqual(2, plugin.StartCallCount);
        }

        #endregion
    }
}
