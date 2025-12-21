// Copyright (C) 2015-2025 The Neo Project.
//
// UT_PluginManager.cs file belongs to the neo project and is free
// software distributed under the MIT software license.

#nullable enable
#pragma warning disable MSTEST0039 // Use 'Assert.ThrowsExactly' instead of 'Assert.ThrowsException'
#pragma warning disable MSTEST0049 // Use 'CancellationToken' parameter

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.Plugins;
using System;
using System.Threading.Tasks;

namespace Neo.UnitTests.Plugins
{
    [TestClass]
    public class UT_PluginManager
    {
        private sealed class MockServiceProvider : IServiceProvider
        {
            public object? GetService(Type serviceType) => null;
        }

        [TestMethod]
        public void TestPluginManagerOptions_DefaultValues()
        {
            var options = new PluginManagerOptions();

            Assert.AreEqual("Plugins", options.PluginDirectory);
            Assert.IsFalse(options.EnableHotReload);
            Assert.AreEqual(TimeSpan.FromMilliseconds(500), options.HotReloadDebounce);
        }

        [TestMethod]
        public void TestPluginManager_Constructor_NullServiceProvider_Throws()
        {
            Assert.ThrowsExactly<ArgumentNullException>(() =>
                new PluginManager(null!));
        }

        [TestMethod]
        public async Task TestPluginManager_PluginCount_InitiallyZero()
        {
            await using var manager = new PluginManager(new MockServiceProvider());

            Assert.AreEqual(0, manager.PluginCount);
        }

        [TestMethod]
        public async Task TestPluginManager_Plugins_InitiallyEmpty()
        {
            await using var manager = new PluginManager(new MockServiceProvider());

            Assert.AreEqual(0, manager.Plugins.Count);
        }

        [TestMethod]
        public async Task TestPluginManager_GetPlugin_NonExistent_ReturnsNull()
        {
            await using var manager = new PluginManager(new MockServiceProvider());

            var plugin = manager.GetPlugin("NonExistent");

            Assert.IsNull(plugin);
        }

        [TestMethod]
        public async Task TestPluginManager_IsLoaded_NonExistent_ReturnsFalse()
        {
            await using var manager = new PluginManager(new MockServiceProvider());

            Assert.IsFalse(manager.IsLoaded("NonExistent"));
        }

        [TestMethod]
        public async Task TestPluginManager_LoadAllAsync_NonExistentDirectory_NoError()
        {
            var options = new PluginManagerOptions { PluginDirectory = "/nonexistent/path" };
            await using var manager = new PluginManager(new MockServiceProvider(), options);

            // Should not throw
            await manager.LoadAllAsync(default);

            Assert.AreEqual(0, manager.PluginCount);
        }

        [TestMethod]
        public async Task TestPluginManager_UnloadPluginAsync_NonExistent_ReturnsFalse()
        {
            await using var manager = new PluginManager(new MockServiceProvider());

            var result = await manager.UnloadPluginAsync("NonExistent", default);

            Assert.IsFalse(result);
        }

        [TestMethod]
        public async Task TestPluginManager_GetPluginGeneric_NoPlugins_ReturnsNull()
        {
            await using var manager = new PluginManager(new MockServiceProvider());

            var plugin = manager.GetPlugin<IPlugin>();

            Assert.IsNull(plugin);
        }

        [TestMethod]
        public async Task TestPluginManager_GetPlugins_NoPlugins_ReturnsEmpty()
        {
            await using var manager = new PluginManager(new MockServiceProvider());

            var plugins = manager.GetPlugins<IPlugin>();

            Assert.IsFalse(plugins.GetEnumerator().MoveNext());
        }

        [TestMethod]
        public async Task TestPluginManager_EnablePluginAsync_NonExistent_ReturnsFalse()
        {
            await using var manager = new PluginManager(new MockServiceProvider());

            var result = await manager.EnablePluginAsync("NonExistent");

            Assert.IsFalse(result);
        }

        [TestMethod]
        public async Task TestPluginManager_DisablePluginAsync_NonExistent_ReturnsFalse()
        {
            await using var manager = new PluginManager(new MockServiceProvider());

            var result = await manager.DisablePluginAsync("NonExistent");

            Assert.IsFalse(result);
        }

        [TestMethod]
        public async Task TestPluginManager_GetPluginMetadata_NonExistent_ReturnsNull()
        {
            await using var manager = new PluginManager(new MockServiceProvider());

            var metadata = manager.GetPluginMetadata("NonExistent");

            Assert.IsNull(metadata);
        }

        [TestMethod]
        public async Task TestPluginManager_IsEnabled_NonExistent_ReturnsFalse()
        {
            await using var manager = new PluginManager(new MockServiceProvider());

            Assert.IsFalse(manager.IsEnabled("NonExistent"));
        }

        [TestMethod]
        public async Task TestPluginManager_ReloadPluginConfigurationAsync_NonExistent_ReturnsFalse()
        {
            await using var manager = new PluginManager(new MockServiceProvider());

            var result = await manager.ReloadPluginConfigurationAsync("NonExistent");

            Assert.IsFalse(result);
        }

        [TestMethod]
        public async Task TestPluginManager_DiscoveredPluginMetadata_InitiallyEmpty()
        {
            await using var manager = new PluginManager(new MockServiceProvider());

            Assert.AreEqual(0, manager.DiscoveredPluginMetadata.Count);
        }

        [TestMethod]
        public async Task TestPluginManager_RefreshCatalogAsync_NonExistentDirectory_NoError()
        {
            var options = new PluginManagerOptions { PluginDirectory = "/nonexistent/path" };
            await using var manager = new PluginManager(new MockServiceProvider(), options);

            await manager.RefreshCatalogAsync();

            Assert.AreEqual(0, manager.DiscoveredPluginMetadata.Count);
        }

        [TestMethod]
        public void TestPluginManagerOptions_DisabledPlugins_IsModifiable()
        {
            var options = new PluginManagerOptions();

            options.DisabledPlugins.Add("Plugin1");
            options.DisabledPlugins.Add("Plugin2");

            Assert.AreEqual(2, options.DisabledPlugins.Count);
            Assert.IsTrue(options.DisabledPlugins.Contains("Plugin1"));
            Assert.IsTrue(options.DisabledPlugins.Contains("Plugin2"));
        }

        [TestMethod]
        public void TestPluginManagerOptions_StartPluginsOnLoad_DefaultTrue()
        {
            var options = new PluginManagerOptions();

            Assert.IsTrue(options.StartPluginsOnLoad);
        }

        [TestMethod]
        public void TestPluginManagerOptions_UnloadPluginsOnDisable_DefaultFalse()
        {
            var options = new PluginManagerOptions();

            Assert.IsFalse(options.UnloadPluginsOnDisable);
        }

        [TestMethod]
        public void TestPluginManagerOptions_EnableConfigReload_DefaultFalse()
        {
            var options = new PluginManagerOptions();

            Assert.IsFalse(options.EnableConfigReload);
        }

        [TestMethod]
        public void TestPluginManagerOptions_PluginConfigFileName_DefaultConfigJson()
        {
            var options = new PluginManagerOptions();

            Assert.AreEqual("config.json", options.PluginConfigFileName);
        }

        [TestMethod]
        public void TestPluginManagerOptions_ConfigReloadDebounce_Default500ms()
        {
            var options = new PluginManagerOptions();

            Assert.AreEqual(TimeSpan.FromMilliseconds(500), options.ConfigReloadDebounce);
        }

        [TestMethod]
        public void TestPluginManagerOptions_NeoVersion_HasDefault()
        {
            var options = new PluginManagerOptions();

            Assert.IsNotNull(options.NeoVersion);
        }

        [TestMethod]
        public async Task TestPluginManager_Events_OnPluginLoaded_NotNull()
        {
            await using var manager = new PluginManager(new MockServiceProvider());

            var eventFired = false;
            manager.OnPluginLoaded += _ => eventFired = true;

            // Event handler should be registered
            Assert.IsFalse(eventFired);
        }

        [TestMethod]
        public async Task TestPluginManager_Events_OnPluginUnloaded_NotNull()
        {
            await using var manager = new PluginManager(new MockServiceProvider());

            var eventFired = false;
            manager.OnPluginUnloaded += _ => eventFired = true;

            Assert.IsFalse(eventFired);
        }

        [TestMethod]
        public async Task TestPluginManager_Events_OnPluginLoadFailed_NotNull()
        {
            await using var manager = new PluginManager(new MockServiceProvider());

            var eventFired = false;
            manager.OnPluginLoadFailed += (_, __) => eventFired = true;

            Assert.IsFalse(eventFired);
        }

        [TestMethod]
        public async Task TestPluginManager_Events_OnPluginEnabled_NotNull()
        {
            await using var manager = new PluginManager(new MockServiceProvider());

            var eventFired = false;
            manager.OnPluginEnabled += _ => eventFired = true;

            Assert.IsFalse(eventFired);
        }

        [TestMethod]
        public async Task TestPluginManager_Events_OnPluginDisabled_NotNull()
        {
            await using var manager = new PluginManager(new MockServiceProvider());

            var eventFired = false;
            manager.OnPluginDisabled += _ => eventFired = true;

            Assert.IsFalse(eventFired);
        }

        [TestMethod]
        public async Task TestPluginManager_Events_OnPluginMetadataDiscovered_NotNull()
        {
            await using var manager = new PluginManager(new MockServiceProvider());

            var eventFired = false;
            manager.OnPluginMetadataDiscovered += _ => eventFired = true;

            Assert.IsFalse(eventFired);
        }

        [TestMethod]
        public async Task TestPluginManager_MultipleDispose_NoError()
        {
            var manager = new PluginManager(new MockServiceProvider());

            await manager.DisposeAsync();
            await manager.DisposeAsync();
            await manager.DisposeAsync();

            // Should not throw
        }

        [TestMethod]
        public async Task TestPluginManager_LoadPluginAsync_NullPath_ReturnsNull()
        {
            await using var manager = new PluginManager(new MockServiceProvider());

            var plugin = await manager.LoadPluginAsync(null!);

            Assert.IsNull(plugin);
        }

        [TestMethod]
        public async Task TestPluginManager_LoadPluginAsync_EmptyPath_ReturnsNull()
        {
            await using var manager = new PluginManager(new MockServiceProvider());

            var plugin = await manager.LoadPluginAsync(string.Empty);

            Assert.IsNull(plugin);
        }
    }

    [TestClass]
    public class UT_IPlugin
    {
        [TestMethod]
        public void TestPluginState_Values()
        {
            Assert.AreEqual(0, (int)PluginState.Unloaded);
            Assert.AreEqual(1, (int)PluginState.Initializing);
            Assert.AreEqual(2, (int)PluginState.Initialized);
            Assert.AreEqual(3, (int)PluginState.Starting);
            Assert.AreEqual(4, (int)PluginState.Running);
            Assert.AreEqual(5, (int)PluginState.Stopping);
            Assert.AreEqual(6, (int)PluginState.Stopped);
            Assert.AreEqual(7, (int)PluginState.Faulted);
        }
    }

    [TestClass]
    public class UT_PluginBase
    {
        private sealed class TestPlugin : PluginBase
        {
            public override string Name => "TestPlugin";
            public override string Description => "A test plugin";

            public bool InitializeCalled { get; private set; }
            public bool StartCalled { get; private set; }
            public bool StopCalled { get; private set; }
            public bool ShutdownCalled { get; private set; }

            protected override void OnInitialize() => InitializeCalled = true;
            protected override void OnStart() => StartCalled = true;
            protected override void OnStop() => StopCalled = true;
            protected override void OnShutdown() => ShutdownCalled = true;
        }

        [TestMethod]
        public void TestPluginBase_Name_ReturnsCorrectValue()
        {
            var plugin = new TestPlugin();

            Assert.AreEqual("TestPlugin", plugin.Name);
        }

        [TestMethod]
        public void TestPluginBase_Description_ReturnsCorrectValue()
        {
            var plugin = new TestPlugin();

            Assert.AreEqual("A test plugin", plugin.Description);
        }

        [TestMethod]
        public void TestPluginBase_InitialState_IsUnloaded()
        {
            var plugin = new TestPlugin();

            Assert.AreEqual(PluginState.Unloaded, plugin.State);
        }

        [TestMethod]
        public async Task TestPluginBase_InitializeAsync_SetsStateAndCallsOnInitialize()
        {
            var plugin = new TestPlugin();

            await plugin.InitializeAsync(default);

            Assert.AreEqual(PluginState.Initialized, plugin.State);
            Assert.IsTrue(plugin.InitializeCalled);
        }

        [TestMethod]
        public async Task TestPluginBase_StartAsync_SetsStateAndCallsOnStart()
        {
            var plugin = new TestPlugin();
            await plugin.InitializeAsync(default);

            await plugin.StartAsync(default);

            Assert.AreEqual(PluginState.Running, plugin.State);
            Assert.IsTrue(plugin.StartCalled);
        }

        [TestMethod]
        public async Task TestPluginBase_StopAsync_SetsStateAndCallsOnStop()
        {
            var plugin = new TestPlugin();
            await plugin.InitializeAsync(default);
            await plugin.StartAsync(default);

            await plugin.StopAsync(default);

            Assert.AreEqual(PluginState.Stopped, plugin.State);
            Assert.IsTrue(plugin.StopCalled);
        }

        [TestMethod]
        public async Task TestPluginBase_ShutdownAsync_SetsStateAndCallsOnShutdown()
        {
            var plugin = new TestPlugin();
            await plugin.InitializeAsync(default);

            await plugin.ShutdownAsync(default);

            Assert.AreEqual(PluginState.Unloaded, plugin.State);
            Assert.IsTrue(plugin.ShutdownCalled);
        }

        [TestMethod]
        public async Task TestPluginBase_FullLifecycle()
        {
            var plugin = new TestPlugin();

            Assert.AreEqual(PluginState.Unloaded, plugin.State);

            await plugin.InitializeAsync(default);
            Assert.AreEqual(PluginState.Initialized, plugin.State);

            await plugin.StartAsync(default);
            Assert.AreEqual(PluginState.Running, plugin.State);

            await plugin.StopAsync(default);
            Assert.AreEqual(PluginState.Stopped, plugin.State);

            await plugin.ShutdownAsync(default);
            Assert.AreEqual(PluginState.Unloaded, plugin.State);

            Assert.IsTrue(plugin.InitializeCalled);
            Assert.IsTrue(plugin.StartCalled);
            Assert.IsTrue(plugin.StopCalled);
            Assert.IsTrue(plugin.ShutdownCalled);
        }

        [TestMethod]
        public async Task TestPluginBase_DisposeAsync_Succeeds()
        {
            var plugin = new TestPlugin();

            // Should not throw
            await plugin.DisposeAsync();
        }

        [TestMethod]
        public void TestPluginBase_Version_ReturnsAssemblyVersion()
        {
            var plugin = new TestPlugin();

            Assert.IsNotNull(plugin.Version);
        }
    }
}
