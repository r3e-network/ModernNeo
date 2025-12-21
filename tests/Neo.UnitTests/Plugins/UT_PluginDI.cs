// Copyright (C) 2015-2025 The Neo Project.
//
// UT_PluginDI.cs file belongs to the neo project and is free
// software distributed under the MIT software license.

#nullable enable
#pragma warning disable MSTEST0039 // Use 'Assert.ThrowsExactly' instead of 'Assert.ThrowsException'
#pragma warning disable MSTEST0049 // Use 'CancellationToken' parameter

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.Plugins;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Neo.UnitTests.Plugins
{
    [TestClass]
    public class UT_PluginServiceScope
    {
        private sealed class MockServiceProvider : IServiceProvider
        {
            public object? GetService(Type serviceType) => null;
        }

        [TestMethod]
        public void TestPluginServiceScope_Constructor_NullScope_Throws()
        {
            Assert.ThrowsException<ArgumentNullException>(() =>
                new PluginServiceScope(null!, "TestPlugin"));
        }

        [TestMethod]
        public void TestPluginServiceScope_Constructor_NullPluginName_Throws()
        {
            var services = new ServiceCollection();
            var provider = services.BuildServiceProvider();
            var scope = provider.CreateScope();

            Assert.ThrowsException<ArgumentNullException>(() =>
                new PluginServiceScope(scope, null!));
        }

        [TestMethod]
        public void TestPluginServiceScope_PluginName_ReturnsCorrectValue()
        {
            var services = new ServiceCollection();
            var provider = services.BuildServiceProvider();
            var scope = provider.CreateScope();
            var pluginScope = new PluginServiceScope(scope, "TestPlugin");

            Assert.AreEqual("TestPlugin", pluginScope.PluginName);
        }

        [TestMethod]
        public void TestPluginServiceScope_ServiceProvider_NotNull()
        {
            var services = new ServiceCollection();
            var provider = services.BuildServiceProvider();
            var scope = provider.CreateScope();
            var pluginScope = new PluginServiceScope(scope, "TestPlugin");

            Assert.IsNotNull(pluginScope.ServiceProvider);
        }

        [TestMethod]
        public void TestPluginServiceScope_IsDisposed_InitiallyFalse()
        {
            var services = new ServiceCollection();
            var provider = services.BuildServiceProvider();
            var scope = provider.CreateScope();
            var pluginScope = new PluginServiceScope(scope, "TestPlugin");

            Assert.IsFalse(pluginScope.IsDisposed);
        }

        [TestMethod]
        public void TestPluginServiceScope_Dispose_SetsIsDisposed()
        {
            var services = new ServiceCollection();
            var provider = services.BuildServiceProvider();
            var scope = provider.CreateScope();
            var pluginScope = new PluginServiceScope(scope, "TestPlugin");

            pluginScope.Dispose();

            Assert.IsTrue(pluginScope.IsDisposed);
        }

        [TestMethod]
        public async Task TestPluginServiceScope_DisposeAsync_SetsIsDisposed()
        {
            var services = new ServiceCollection();
            var provider = services.BuildServiceProvider();
            var scope = provider.CreateScope();
            var pluginScope = new PluginServiceScope(scope, "TestPlugin");

            await pluginScope.DisposeAsync();

            Assert.IsTrue(pluginScope.IsDisposed);
        }

        [TestMethod]
        public void TestPluginServiceScope_GetService_ReturnsService()
        {
            var services = new ServiceCollection();
            services.AddSingleton<ITestService, TestService>();
            var provider = services.BuildServiceProvider();
            var scope = provider.CreateScope();
            var pluginScope = new PluginServiceScope(scope, "TestPlugin");

            var service = pluginScope.GetService<ITestService>();

            Assert.IsNotNull(service);
            Assert.IsInstanceOfType(service, typeof(TestService));
        }

        [TestMethod]
        public void TestPluginServiceScope_GetRequiredService_ReturnsService()
        {
            var services = new ServiceCollection();
            services.AddSingleton<ITestService, TestService>();
            var provider = services.BuildServiceProvider();
            var scope = provider.CreateScope();
            var pluginScope = new PluginServiceScope(scope, "TestPlugin");

            var service = pluginScope.GetRequiredService<ITestService>();

            Assert.IsNotNull(service);
            Assert.IsInstanceOfType(service, typeof(TestService));
        }

        [TestMethod]
        public void TestPluginServiceScope_GetRequiredService_NotFound_Throws()
        {
            var services = new ServiceCollection();
            var provider = services.BuildServiceProvider();
            var scope = provider.CreateScope();
            var pluginScope = new PluginServiceScope(scope, "TestPlugin");

            Assert.ThrowsException<InvalidOperationException>(() =>
                pluginScope.GetRequiredService<ITestService>());
        }

        [TestMethod]
        public void TestPluginServiceScope_GetService_AfterDispose_Throws()
        {
            var services = new ServiceCollection();
            var provider = services.BuildServiceProvider();
            var scope = provider.CreateScope();
            var pluginScope = new PluginServiceScope(scope, "TestPlugin");

            pluginScope.Dispose();

            Assert.ThrowsException<ObjectDisposedException>(() =>
                pluginScope.GetService<ITestService>());
        }

        [TestMethod]
        public void TestPluginServiceScope_CreateScope_ReturnsNewScope()
        {
            var services = new ServiceCollection();
            var provider = services.BuildServiceProvider();
            var scope = provider.CreateScope();
            var pluginScope = new PluginServiceScope(scope, "TestPlugin");

            var childScope = pluginScope.CreateScope();

            Assert.IsNotNull(childScope);
        }

        private interface ITestService { }
        private sealed class TestService : ITestService { }
    }

    [TestClass]
    public class UT_PluginServiceScopeFactory
    {
        [TestMethod]
        public void TestPluginServiceScopeFactory_Constructor_NullProvider_Throws()
        {
            Assert.ThrowsException<ArgumentNullException>(() =>
                new PluginServiceScopeFactory(null!));
        }

        [TestMethod]
        public void TestPluginServiceScopeFactory_CreateScope_NullPluginName_Throws()
        {
            var services = new ServiceCollection();
            var provider = services.BuildServiceProvider();
            var factory = new PluginServiceScopeFactory(provider);

            Assert.ThrowsException<ArgumentException>(() =>
                factory.CreateScope(null!));
        }

        [TestMethod]
        public void TestPluginServiceScopeFactory_CreateScope_EmptyPluginName_Throws()
        {
            var services = new ServiceCollection();
            var provider = services.BuildServiceProvider();
            var factory = new PluginServiceScopeFactory(provider);

            Assert.ThrowsException<ArgumentException>(() =>
                factory.CreateScope(string.Empty));
        }

        [TestMethod]
        public void TestPluginServiceScopeFactory_CreateScope_WithoutConfiguration_ReturnsScope()
        {
            var services = new ServiceCollection();
            var provider = services.BuildServiceProvider();
            var factory = new PluginServiceScopeFactory(provider);

            var scope = factory.CreateScope("TestPlugin");

            Assert.IsNotNull(scope);
            Assert.AreEqual("TestPlugin", scope.PluginName);
        }

        [TestMethod]
        public void TestPluginServiceScopeFactory_CreateScope_WithConfiguration_ReturnsScope()
        {
            var services = new ServiceCollection();
            var provider = services.BuildServiceProvider();
            var factory = new PluginServiceScopeFactory(provider);

            var scope = factory.CreateScope("TestPlugin", svc =>
            {
                svc.AddSingleton<ITestService, TestService>();
            });

            Assert.IsNotNull(scope);
            Assert.AreEqual("TestPlugin", scope.PluginName);
        }

        [TestMethod]
        public void TestPluginServiceScopeFactory_CreateChildScope_NullParent_Throws()
        {
            var services = new ServiceCollection();
            var provider = services.BuildServiceProvider();
            var factory = new PluginServiceScopeFactory(provider);

            Assert.ThrowsException<ArgumentNullException>(() =>
                factory.CreateChildScope(null!));
        }

        [TestMethod]
        public void TestPluginServiceScopeFactory_CreateChildScope_ReturnsScope()
        {
            var services = new ServiceCollection();
            var provider = services.BuildServiceProvider();
            var factory = new PluginServiceScopeFactory(provider);
            var parentScope = factory.CreateScope("TestPlugin");

            var childScope = factory.CreateChildScope(parentScope);

            Assert.IsNotNull(childScope);
        }

        private interface ITestService { }
        private sealed class TestService : ITestService { }
    }

    [TestClass]
    public class UT_IPluginServiceRegistrar
    {
        [TestMethod]
        public void TestPluginServiceRegistrarExtensions_AddPluginService_RegistersService()
        {
            var services = new ServiceCollection();

            services.AddPluginService<ITestService, TestService>();

            var provider = services.BuildServiceProvider();
            var service = provider.GetService<ITestService>();

            Assert.IsNotNull(service);
            Assert.IsInstanceOfType(service, typeof(TestService));
        }

        [TestMethod]
        public void TestPluginServiceRegistrarExtensions_AddPluginSingleton_RegistersSingleton()
        {
            var services = new ServiceCollection();

            services.AddPluginSingleton<ITestService, TestService>();

            var provider = services.BuildServiceProvider();
            var service1 = provider.GetService<ITestService>();
            var service2 = provider.GetService<ITestService>();

            Assert.IsNotNull(service1);
            Assert.IsNotNull(service2);
            Assert.AreSame(service1, service2);
        }

        [TestMethod]
        public void TestPluginServiceRegistrarExtensions_AddPluginScoped_RegistersScoped()
        {
            var services = new ServiceCollection();

            services.AddPluginScoped<ITestService, TestService>();

            var provider = services.BuildServiceProvider();
            using var scope1 = provider.CreateScope();
            using var scope2 = provider.CreateScope();

            var service1a = scope1.ServiceProvider.GetService<ITestService>();
            var service1b = scope1.ServiceProvider.GetService<ITestService>();
            var service2 = scope2.ServiceProvider.GetService<ITestService>();

            Assert.IsNotNull(service1a);
            Assert.IsNotNull(service1b);
            Assert.IsNotNull(service2);
            Assert.AreSame(service1a, service1b);
            Assert.AreNotSame(service1a, service2);
        }

        [TestMethod]
        public void TestPluginServiceRegistrarExtensions_AddPluginTransient_RegistersTransient()
        {
            var services = new ServiceCollection();

            services.AddPluginTransient<ITestService, TestService>();

            var provider = services.BuildServiceProvider();
            var service1 = provider.GetService<ITestService>();
            var service2 = provider.GetService<ITestService>();

            Assert.IsNotNull(service1);
            Assert.IsNotNull(service2);
            Assert.AreNotSame(service1, service2);
        }

        [TestMethod]
        public void TestPluginServiceRegistrarExtensions_TryReplace_ExistingService_ReplacesService()
        {
            var services = new ServiceCollection();
            services.AddSingleton<ITestService, TestService>();

            var result = services.TryReplace<ITestService, AlternateTestService>();

            Assert.IsTrue(result);

            var provider = services.BuildServiceProvider();
            var service = provider.GetService<ITestService>();

            Assert.IsNotNull(service);
            Assert.IsInstanceOfType(service, typeof(AlternateTestService));
        }

        [TestMethod]
        public void TestPluginServiceRegistrarExtensions_TryReplace_NonExistingService_ReturnsFalse()
        {
            var services = new ServiceCollection();

            var result = services.TryReplace<ITestService, TestService>();

            Assert.IsFalse(result);
        }

        [TestMethod]
        public void TestPluginServiceRegistrarExtensions_Decorate_ExistingService_DecoratesService()
        {
            var services = new ServiceCollection();
            services.AddSingleton<ITestService, TestService>();

            services.Decorate<ITestService, TestServiceDecorator>();

            var provider = services.BuildServiceProvider();
            var service = provider.GetService<ITestService>();

            Assert.IsNotNull(service);
            Assert.IsInstanceOfType(service, typeof(TestServiceDecorator));
        }

        [TestMethod]
        public void TestPluginServiceRegistrarExtensions_Decorate_NonExistingService_Throws()
        {
            var services = new ServiceCollection();

            Assert.ThrowsException<InvalidOperationException>(() =>
                services.Decorate<ITestService, TestServiceDecorator>());
        }

        private interface ITestService { }
        private sealed class TestService : ITestService { }
        private sealed class AlternateTestService : ITestService { }
        private sealed class TestServiceDecorator : ITestService
        {
            public TestServiceDecorator(ITestService inner) { }
        }
    }

    [TestClass]
    public class UT_PluginServiceBase
    {
        [TestMethod]
        public void TestPluginServiceBase_IsDisposed_InitiallyFalse()
        {
            var service = new TestPluginService();

            Assert.IsFalse(service.IsDisposedPublic);
        }

        [TestMethod]
        public async Task TestPluginServiceBase_DisposeAsync_SetsIsDisposed()
        {
            var service = new TestPluginService();

            await service.DisposeAsync();

            Assert.IsTrue(service.IsDisposedPublic);
        }

        [TestMethod]
        public async Task TestPluginServiceBase_DisposeAsync_CallsOnDisposingAsync()
        {
            var service = new TestPluginService();

            await service.DisposeAsync();

            Assert.IsTrue(service.OnDisposingAsyncCalled);
        }

        [TestMethod]
        public async Task TestPluginServiceBase_OnInitializingAsync_CanBeCalled()
        {
            var service = new TestPluginService();

            await service.OnInitializingAsync();

            Assert.IsTrue(service.OnInitializingAsyncCalled);
        }

        [TestMethod]
        public void TestPluginServiceBase_ThrowIfDisposed_AfterDispose_Throws()
        {
            var service = new TestPluginService();

            service.DisposeAsync().AsTask().Wait();

            Assert.ThrowsException<ObjectDisposedException>(() =>
                service.ThrowIfDisposedPublic());
        }

        private sealed class TestPluginService : PluginServiceBase
        {
            public bool OnInitializingAsyncCalled { get; private set; }
            public bool OnDisposingAsyncCalled { get; private set; }
            public bool IsDisposedPublic => IsDisposed;

            public override Task OnInitializingAsync(CancellationToken cancellationToken = default)
            {
                OnInitializingAsyncCalled = true;
                return Task.CompletedTask;
            }

            public override Task OnDisposingAsync(CancellationToken cancellationToken = default)
            {
                OnDisposingAsyncCalled = true;
                return Task.CompletedTask;
            }

            public void ThrowIfDisposedPublic() => ThrowIfDisposed();
        }
    }

    [TestClass]
    public class UT_PluginConfigurationProvider
    {
        private string _tempDirectory = string.Empty;

        [TestInitialize]
        public void Setup()
        {
            _tempDirectory = Path.Combine(Path.GetTempPath(), $"PluginConfigTest_{Guid.NewGuid()}");
            Directory.CreateDirectory(_tempDirectory);
        }

        [TestCleanup]
        public void Cleanup()
        {
            if (Directory.Exists(_tempDirectory))
            {
                try
                {
                    Directory.Delete(_tempDirectory, true);
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
        }

        [TestMethod]
        public void TestPluginConfigurationProvider_Constructor_NullDirectory_Throws()
        {
            Assert.ThrowsException<ArgumentNullException>(() =>
                new PluginConfigurationProvider(null!));
        }

        [TestMethod]
        public void TestPluginConfigurationProvider_LoadConfiguration_NullPluginName_Throws()
        {
            var provider = new PluginConfigurationProvider(_tempDirectory);

            Assert.ThrowsException<ArgumentException>(() =>
                provider.LoadConfiguration(null!));
        }

        [TestMethod]
        public void TestPluginConfigurationProvider_LoadConfiguration_NonExistentFile_ReturnsNull()
        {
            var provider = new PluginConfigurationProvider(_tempDirectory);

            var config = provider.LoadConfiguration("NonExistentPlugin");

            Assert.IsNull(config);
        }

        [TestMethod]
        public void TestPluginConfigurationProvider_LoadConfiguration_ExistingFile_ReturnsConfiguration()
        {
            var pluginDir = Path.Combine(_tempDirectory, "TestPlugin");
            Directory.CreateDirectory(pluginDir);
            var configPath = Path.Combine(pluginDir, "config.json");
            File.WriteAllText(configPath, "{\"TestKey\":\"TestValue\"}");

            var provider = new PluginConfigurationProvider(_tempDirectory);
            var config = provider.LoadConfiguration("TestPlugin");

            Assert.IsNotNull(config);
            Assert.AreEqual("TestValue", config["TestKey"]);
        }

        [TestMethod]
        public void TestPluginConfigurationProvider_GetConfiguration_LoadedPlugin_ReturnsConfiguration()
        {
            var pluginDir = Path.Combine(_tempDirectory, "TestPlugin");
            Directory.CreateDirectory(pluginDir);
            var configPath = Path.Combine(pluginDir, "config.json");
            File.WriteAllText(configPath, "{\"TestKey\":\"TestValue\"}");

            var provider = new PluginConfigurationProvider(_tempDirectory);
            provider.LoadConfiguration("TestPlugin");
            var config = provider.GetConfiguration("TestPlugin");

            Assert.IsNotNull(config);
            Assert.AreEqual("TestValue", config["TestKey"]);
        }

        [TestMethod]
        public void TestPluginConfigurationProvider_GetSection_ReturnsSection()
        {
            var pluginDir = Path.Combine(_tempDirectory, "TestPlugin");
            Directory.CreateDirectory(pluginDir);
            var configPath = Path.Combine(pluginDir, "config.json");
            File.WriteAllText(configPath, "{\"Section\":{\"Key\":\"Value\"}}");

            var provider = new PluginConfigurationProvider(_tempDirectory);
            provider.LoadConfiguration("TestPlugin");
            var section = provider.GetSection("TestPlugin", "Section");

            Assert.IsNotNull(section);
            Assert.AreEqual("Value", section["Key"]);
        }

        [TestMethod]
        public void TestPluginConfigurationProvider_Bind_ReturnsTypedObject()
        {
            var pluginDir = Path.Combine(_tempDirectory, "TestPlugin");
            Directory.CreateDirectory(pluginDir);
            var configPath = Path.Combine(pluginDir, "config.json");
            File.WriteAllText(configPath, "{\"Name\":\"Test\",\"Value\":42}");

            var provider = new PluginConfigurationProvider(_tempDirectory);
            provider.LoadConfiguration("TestPlugin");
            var config = provider.Bind<TestConfig>("TestPlugin");

            Assert.IsNotNull(config);
            Assert.AreEqual("Test", config.Name);
            Assert.AreEqual(42, config.Value);
        }

        [TestMethod]
        public void TestPluginConfigurationProvider_UnloadConfiguration_RemovesConfiguration()
        {
            var pluginDir = Path.Combine(_tempDirectory, "TestPlugin");
            Directory.CreateDirectory(pluginDir);
            var configPath = Path.Combine(pluginDir, "config.json");
            File.WriteAllText(configPath, "{\"TestKey\":\"TestValue\"}");

            var provider = new PluginConfigurationProvider(_tempDirectory);
            provider.LoadConfiguration("TestPlugin");
            var result = provider.UnloadConfiguration("TestPlugin");

            Assert.IsTrue(result);
            Assert.IsNull(provider.GetConfiguration("TestPlugin"));
        }

        [TestMethod]
        public void TestPluginConfigurationProvider_ConfigurationCount_ReturnsCorrectCount()
        {
            var pluginDir = Path.Combine(_tempDirectory, "TestPlugin");
            Directory.CreateDirectory(pluginDir);
            var configPath = Path.Combine(pluginDir, "config.json");
            File.WriteAllText(configPath, "{\"TestKey\":\"TestValue\"}");

            var provider = new PluginConfigurationProvider(_tempDirectory);
            Assert.AreEqual(0, provider.ConfigurationCount);

            provider.LoadConfiguration("TestPlugin");
            Assert.AreEqual(1, provider.ConfigurationCount);
        }

        [TestMethod]
        public void TestPluginConfigurationProvider_Dispose_DisposesProvider()
        {
            var provider = new PluginConfigurationProvider(_tempDirectory);

            provider.Dispose();

            Assert.ThrowsException<ObjectDisposedException>(() =>
                provider.LoadConfiguration("TestPlugin"));
        }

        private sealed class TestConfig
        {
            public string Name { get; set; } = string.Empty;
            public int Value { get; set; }
        }
    }

    [TestClass]
    public class UT_PluginConfigurationExtensions
    {
        [TestMethod]
        public void TestGetValueOrDefault_ExistingKey_ReturnsValue()
        {
            var builder = new ConfigurationBuilder();
            builder.AddInMemoryCollection(new[]
            {
                new KeyValuePair<string, string?>("TestKey", "TestValue")
            });
            var config = builder.Build();

            var value = config.GetValueOrDefault("TestKey", "Default");

            Assert.AreEqual("TestValue", value);
        }

        [TestMethod]
        public void TestGetValueOrDefault_NonExistingKey_ReturnsDefault()
        {
            var builder = new ConfigurationBuilder();
            var config = builder.Build();

            var value = config.GetValueOrDefault("NonExistentKey", "Default");

            Assert.AreEqual("Default", value);
        }

        [TestMethod]
        public void TestHasSection_ExistingSection_ReturnsTrue()
        {
            var builder = new ConfigurationBuilder();
            builder.AddInMemoryCollection(new[]
            {
                new KeyValuePair<string, string?>("Section:Key", "Value")
            });
            var config = builder.Build();

            var hasSection = config.HasSection("Section");

            Assert.IsTrue(hasSection);
        }

        [TestMethod]
        public void TestHasSection_NonExistingSection_ReturnsFalse()
        {
            var builder = new ConfigurationBuilder();
            var config = builder.Build();

            var hasSection = config.HasSection("NonExistentSection");

            Assert.IsFalse(hasSection);
        }
    }

    [TestClass]
    public class UT_IPluginFactory
    {
        [TestMethod]
        public void TestDefaultPluginFactory_CreatePlugin_NullType_Throws()
        {
            var factory = new DefaultPluginFactory();
            var services = new ServiceCollection();
            var provider = services.BuildServiceProvider();

            Assert.ThrowsException<ArgumentNullException>(() =>
                factory.CreatePlugin(null!, provider));
        }

        [TestMethod]
        public void TestDefaultPluginFactory_CreatePlugin_NullProvider_Throws()
        {
            var factory = new DefaultPluginFactory();

            Assert.ThrowsException<ArgumentNullException>(() =>
                factory.CreatePlugin(typeof(TestPlugin), null!));
        }

        [TestMethod]
        public void TestDefaultPluginFactory_CreatePlugin_NonPluginType_Throws()
        {
            var factory = new DefaultPluginFactory();
            var services = new ServiceCollection();
            var provider = services.BuildServiceProvider();

            Assert.ThrowsException<ArgumentException>(() =>
                factory.CreatePlugin(typeof(string), provider));
        }

        [TestMethod]
        public void TestDefaultPluginFactory_CreatePlugin_ValidType_ReturnsPlugin()
        {
            var factory = new DefaultPluginFactory();
            var services = new ServiceCollection();
            var provider = services.BuildServiceProvider();

            var plugin = factory.CreatePlugin(typeof(TestPlugin), provider);

            Assert.IsNotNull(plugin);
            Assert.IsInstanceOfType(plugin, typeof(TestPlugin));
        }

        [TestMethod]
        public void TestDefaultPluginFactory_CanCreatePlugin_ValidType_ReturnsTrue()
        {
            var factory = new DefaultPluginFactory();

            var canCreate = factory.CanCreatePlugin(typeof(TestPlugin));

            Assert.IsTrue(canCreate);
        }

        [TestMethod]
        public void TestDefaultPluginFactory_CanCreatePlugin_AbstractType_ReturnsFalse()
        {
            var factory = new DefaultPluginFactory();

            var canCreate = factory.CanCreatePlugin(typeof(PluginBase));

            Assert.IsFalse(canCreate);
        }

        [TestMethod]
        public void TestDefaultPluginFactory_CanCreatePlugin_InterfaceType_ReturnsFalse()
        {
            var factory = new DefaultPluginFactory();

            var canCreate = factory.CanCreatePlugin(typeof(IPlugin));

            Assert.IsFalse(canCreate);
        }

        [TestMethod]
        public void TestStaticFactoryMethodPluginFactory_CreatePlugin_WithFactoryMethod_ReturnsPlugin()
        {
            var factory = new StaticFactoryMethodPluginFactory();
            var services = new ServiceCollection();
            var provider = services.BuildServiceProvider();

            var plugin = factory.CreatePlugin(typeof(TestPluginWithFactory), provider);

            Assert.IsNotNull(plugin);
            Assert.IsInstanceOfType(plugin, typeof(TestPluginWithFactory));
        }

        [TestMethod]
        public void TestStaticFactoryMethodPluginFactory_CanCreatePlugin_WithFactoryMethod_ReturnsTrue()
        {
            var factory = new StaticFactoryMethodPluginFactory();

            var canCreate = factory.CanCreatePlugin(typeof(TestPluginWithFactory));

            Assert.IsTrue(canCreate);
        }

        [TestMethod]
        public void TestStaticFactoryMethodPluginFactory_CanCreatePlugin_WithoutFactoryMethod_ReturnsFalse()
        {
            var factory = new StaticFactoryMethodPluginFactory();

            var canCreate = factory.CanCreatePlugin(typeof(TestPlugin));

            Assert.IsFalse(canCreate);
        }

        [TestMethod]
        public void TestCompositePluginFactory_CreatePlugin_UsesFirstMatchingFactory()
        {
            var factory1 = new DefaultPluginFactory();
            var factory2 = new StaticFactoryMethodPluginFactory();
            var composite = new CompositePluginFactory(factory1, factory2);
            var services = new ServiceCollection();
            var provider = services.BuildServiceProvider();

            var plugin = composite.CreatePlugin(typeof(TestPlugin), provider);

            Assert.IsNotNull(plugin);
            Assert.IsInstanceOfType(plugin, typeof(TestPlugin));
        }

        [TestMethod]
        public void TestCompositePluginFactory_Constructor_EmptyFactories_Throws()
        {
            Assert.ThrowsException<ArgumentException>(() =>
                new CompositePluginFactory());
        }

        [TestMethod]
        public void TestDelegatePluginFactory_CreatePlugin_UsesDelegate()
        {
            var factory = new DelegatePluginFactory((type, provider) =>
            {
                if (type == typeof(TestPlugin))
                    return new TestPlugin();
                return null;
            });
            var services = new ServiceCollection();
            var provider = services.BuildServiceProvider();

            var plugin = factory.CreatePlugin(typeof(TestPlugin), provider);

            Assert.IsNotNull(plugin);
            Assert.IsInstanceOfType(plugin, typeof(TestPlugin));
        }

        [TestMethod]
        public void TestPluginFactoryExtensions_CreatePlugin_Generic_ReturnsTypedPlugin()
        {
            var factory = new DefaultPluginFactory();
            var services = new ServiceCollection();
            var provider = services.BuildServiceProvider();

            var plugin = factory.CreatePlugin<TestPlugin>(provider);

            Assert.IsNotNull(plugin);
            Assert.IsInstanceOfType(plugin, typeof(TestPlugin));
        }

        [TestMethod]
        public void TestPluginFactoryExtensions_TryCreatePlugin_Success_ReturnsTrue()
        {
            var factory = new DefaultPluginFactory();
            var services = new ServiceCollection();
            var provider = services.BuildServiceProvider();

            var result = factory.TryCreatePlugin(typeof(TestPlugin), provider, out var plugin);

            Assert.IsTrue(result);
            Assert.IsNotNull(plugin);
        }

        [TestMethod]
        public void TestPluginFactoryExtensions_TryCreatePlugin_Failure_ReturnsFalse()
        {
            var factory = new DelegatePluginFactory((type, provider) => null);
            var services = new ServiceCollection();
            var provider = services.BuildServiceProvider();

            var result = factory.TryCreatePlugin(typeof(TestPlugin), provider, out var plugin);

            Assert.IsFalse(result);
            Assert.IsNull(plugin);
        }

        private sealed class TestPlugin : PluginBase
        {
            public override string Name => "TestPlugin";
        }

        private sealed class TestPluginWithFactory : PluginBase
        {
            public override string Name => "TestPluginWithFactory";

            public static IPlugin Create(IServiceProvider serviceProvider)
            {
                return new TestPluginWithFactory();
            }
        }
    }
}
