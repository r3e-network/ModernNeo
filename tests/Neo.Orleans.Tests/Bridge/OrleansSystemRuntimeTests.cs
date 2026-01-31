// Copyright (C) 2015-2025 The Neo Project.
//
// OrleansSystemRuntimeTests.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

#nullable enable

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;
using Neo;
using Neo.Core.Interfaces;
using Neo.Network.P2P.Payloads;
using Neo.Orleans.Adapters;
using Neo.Orleans.Bridge;
using Neo.Orleans.Hosting;
using Neo.Orleans.Interfaces;
using Neo.Orleans.Options;
using Neo.Orleans.Services;
using Neo.Orleans.Tests;
using Orleans.TestingHost;

namespace Neo.Orleans.Tests.Bridge
{
    /// <summary>
    /// Unit tests for OrleansSystemRuntime and its runtime adapters.
    /// Uses Orleans TestCluster for integration testing with real grains.
    /// </summary>
    [TestClass]
    public class OrleansSystemRuntimeTests
    {
        public TestContext TestContext { get; set; } = null!;

        private TestCluster? _cluster;
        private IHost? _testHost;

        [TestInitialize]
        public async Task Setup()
        {
            var builder = new TestClusterBuilder();
            builder.AddSiloBuilderConfigurator<BridgeTestSiloConfigurator>();
            _cluster = builder.Build();
            await _cluster.DeployAsync();

            // Create a test host that wraps the cluster's service provider
            var hostBuilder = Host.CreateDefaultBuilder()
                .ConfigureServices(services =>
                {
                    services.AddSingleton(_cluster.GrainFactory);
                });
            _testHost = hostBuilder.Build();
        }

        [TestCleanup]
        public async Task Cleanup()
        {
            _testHost?.Dispose();
            if (_cluster != null)
            {
                await _cluster.StopAllSilosAsync();
                await _cluster.DisposeAsync();
            }
        }

        [TestMethod]
        public void TestIsOrleans_ReturnsTrue()
        {
            // Arrange
            var runtime = new OrleansSystemRuntime(_testHost!);

            // Assert
            Assert.IsTrue(runtime.IsOrleans);
        }

        [TestMethod]
        public void TestBlockchain_NotNull()
        {
            // Arrange
            var runtime = new OrleansSystemRuntime(_testHost!);

            // Assert
            Assert.IsNotNull(runtime.Blockchain);
        }

        [TestMethod]
        public void TestMemoryPool_NotNull()
        {
            // Arrange
            var runtime = new OrleansSystemRuntime(_testHost!);

            // Assert
            Assert.IsNotNull(runtime.MemoryPool);
        }

        [TestMethod]
        public void TestLocalNode_NotNull()
        {
            // Arrange
            var runtime = new OrleansSystemRuntime(_testHost!);

            // Assert
            Assert.IsNotNull(runtime.LocalNode);
        }

        [TestMethod]
        public void TestTaskManager_NotNull()
        {
            // Arrange
            var runtime = new OrleansSystemRuntime(_testHost!);

            // Assert
            Assert.IsNotNull(runtime.TaskManager);
        }

        [TestMethod]
        public void TestConsensus_NotNull()
        {
            // Arrange
            var runtime = new OrleansSystemRuntime(_testHost!);

            // Assert
            Assert.IsNotNull(runtime.Consensus);
        }

        [TestMethod]
        public void TestConstructor_NullHost_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.ThrowsExactly<ArgumentNullException>(() => new OrleansSystemRuntime(null!));
        }

        [TestMethod]
        public async Task TestInitializeAsync_Completes()
        {
            // Arrange
            var runtime = new OrleansSystemRuntime(_testHost!);

            // Act & Assert - should not throw
            await runtime.InitializeAsync(TestContext.CancellationTokenSource.Token);
        }

        [TestMethod]
        public async Task TestDisposeAsync_Completes()
        {
            // Arrange
            var runtime = new OrleansSystemRuntime(_testHost!);

            // Act & Assert - should not throw
            await runtime.DisposeAsync();
        }
    }

    /// <summary>
    /// Unit tests for OrleansBlockchainRuntime.
    /// </summary>
    [TestClass]
    public class OrleansBlockchainRuntimeTests
    {
        public TestContext TestContext { get; set; } = null!;

        private TestCluster? _cluster;
        private IHost? _testHost;

        [TestInitialize]
        public async Task Setup()
        {
            var builder = new TestClusterBuilder();
            builder.AddSiloBuilderConfigurator<BridgeTestSiloConfigurator>();
            _cluster = builder.Build();
            await _cluster.DeployAsync();

            var hostBuilder = Host.CreateDefaultBuilder()
                .ConfigureServices(services =>
                {
                    services.AddSingleton(_cluster.GrainFactory);
                });
            _testHost = hostBuilder.Build();
        }

        [TestCleanup]
        public async Task Cleanup()
        {
            _testHost?.Dispose();
            if (_cluster != null)
            {
                await _cluster.StopAllSilosAsync();
                await _cluster.DisposeAsync();
            }
        }

        [TestMethod]
        public async Task TestGetHeightAsync_InitialState_ReturnsZero()
        {
            // Arrange
            var runtime = new OrleansSystemRuntime(_testHost!);

            // Act
            var height = await runtime.Blockchain.GetHeightAsync();

            // Assert
            Assert.AreEqual(0u, height);
        }

        [TestMethod]
        public async Task TestGetHeaderHeightAsync_InitialState_ReturnsZero()
        {
            // Arrange
            var runtime = new OrleansSystemRuntime(_testHost!);

            // Act
            var height = await runtime.Blockchain.GetHeaderHeightAsync();

            // Assert
            Assert.AreEqual(0u, height);
        }

        [TestMethod]
        public async Task TestContainsBlockAsync_NonExistent_ReturnsFalse()
        {
            // Arrange
            var runtime = new OrleansSystemRuntime(_testHost!);
            var hash = new byte[32];

            // Act
            var result = await runtime.Blockchain.ContainsBlockAsync(hash);

            // Assert
            Assert.IsFalse(result);
        }

        [TestMethod]
        public async Task TestContainsTransactionAsync_NonExistent_ReturnsFalse()
        {
            // Arrange
            var runtime = new OrleansSystemRuntime(_testHost!);
            var hash = new byte[32];

            // Act
            var result = await runtime.Blockchain.ContainsTransactionAsync(hash);

            // Assert
            Assert.IsFalse(result);
        }

        [TestMethod]
        public async Task TestGetBlockByHashAsync_NonExistent_ReturnsNull()
        {
            // Arrange
            var runtime = new OrleansSystemRuntime(_testHost!);
            var hash = new byte[32];

            // Act
            var block = await runtime.Blockchain.GetBlockByHashAsync(hash);

            // Assert
            Assert.IsNull(block);
        }

        [TestMethod]
        public async Task TestGetBlockByIndexAsync_NonExistent_ReturnsNull()
        {
            // Arrange
            var runtime = new OrleansSystemRuntime(_testHost!);

            // Act
            var block = await runtime.Blockchain.GetBlockByIndexAsync(999);

            // Assert
            Assert.IsNull(block);
        }

        [TestMethod]
        public async Task TestGetBlockHashByIndexAsync_NonExistent_ReturnsNull()
        {
            // Arrange
            var runtime = new OrleansSystemRuntime(_testHost!);

            // Act
            var hash = await runtime.Blockchain.GetBlockHashByIndexAsync(999);

            // Assert
            Assert.IsNull(hash);
        }

        [TestMethod]
        public void TestTell_DoesNotThrow()
        {
            // Arrange
            var runtime = new OrleansSystemRuntime(_testHost!);

            // Act & Assert - should not throw
            runtime.Blockchain.Tell("test message");
        }

        [TestMethod]
        public async Task TestAskAsync_ThrowsNotSupportedException()
        {
            // Arrange
            var runtime = new OrleansSystemRuntime(_testHost!);

            // Act & Assert
            await Assert.ThrowsExactlyAsync<NotSupportedException>(async () =>
                await runtime.Blockchain.AskAsync<string, string>("test", TestContext.CancellationTokenSource.Token));
        }
    }

    /// <summary>
    /// Unit tests for OrleansMemoryPoolRuntime.
    /// </summary>
    [TestClass]
    public class OrleansMemoryPoolRuntimeTests
    {
        public TestContext TestContext { get; set; } = null!;

        private TestCluster? _cluster;
        private IHost? _testHost;

        [TestInitialize]
        public async Task Setup()
        {
            var builder = new TestClusterBuilder();
            builder.AddSiloBuilderConfigurator<BridgeTestSiloConfigurator>();
            _cluster = builder.Build();
            await _cluster.DeployAsync();

            var hostBuilder = Host.CreateDefaultBuilder()
                .ConfigureServices(services =>
                {
                    services.AddSingleton(_cluster.GrainFactory);
                });
            _testHost = hostBuilder.Build();
        }

        [TestCleanup]
        public async Task Cleanup()
        {
            _testHost?.Dispose();
            if (_cluster != null)
            {
                await _cluster.StopAllSilosAsync();
                await _cluster.DisposeAsync();
            }
        }

        [TestMethod]
        public async Task TestGetVerifiedCountAsync_InitialState_ReturnsZero()
        {
            // Arrange
            var runtime = new OrleansSystemRuntime(_testHost!);

            // Act
            var count = await runtime.MemoryPool.GetVerifiedCountAsync();

            // Assert
            Assert.AreEqual(0, count);
        }

        [TestMethod]
        public async Task TestGetUnverifiedCountAsync_InitialState_ReturnsZero()
        {
            // Arrange
            var runtime = new OrleansSystemRuntime(_testHost!);

            // Act
            var count = await runtime.MemoryPool.GetUnverifiedCountAsync();

            // Assert
            Assert.AreEqual(0, count);
        }

        [TestMethod]
        public async Task TestContainsKeyAsync_NonExistent_ReturnsFalse()
        {
            // Arrange
            var runtime = new OrleansSystemRuntime(_testHost!);
            var hash = new byte[32];

            // Act
            var result = await runtime.MemoryPool.ContainsKeyAsync(hash);

            // Assert
            Assert.IsFalse(result);
        }

        [TestMethod]
        public async Task TestGetVerifiedTransactionsAsync_InitialState_ReturnsEmpty()
        {
            // Arrange
            var runtime = new OrleansSystemRuntime(_testHost!);

            // Act
            var transactions = await runtime.MemoryPool.GetVerifiedTransactionsAsync(10);

            // Assert
            Assert.IsNotNull(transactions);
            Assert.AreEqual(0, transactions.Count());
        }
    }

    /// <summary>
    /// Unit tests for OrleansLocalNodeRuntime.
    /// </summary>
    [TestClass]
    public class OrleansLocalNodeRuntimeTests
    {
        public TestContext TestContext { get; set; } = null!;

        private TestCluster? _cluster;
        private IHost? _testHost;

        [TestInitialize]
        public async Task Setup()
        {
            var builder = new TestClusterBuilder();
            builder.AddSiloBuilderConfigurator<BridgeTestSiloConfigurator>();
            _cluster = builder.Build();
            await _cluster.DeployAsync();

            var hostBuilder = Host.CreateDefaultBuilder()
                .ConfigureServices(services =>
                {
                    services.AddSingleton(_cluster.GrainFactory);
                });
            _testHost = hostBuilder.Build();
        }

        [TestCleanup]
        public async Task Cleanup()
        {
            _testHost?.Dispose();
            if (_cluster != null)
            {
                await _cluster.StopAllSilosAsync();
                await _cluster.DisposeAsync();
            }
        }

        [TestMethod]
        public async Task TestGetConnectedPeerCountAsync_InitialState_ReturnsZero()
        {
            // Arrange
            var runtime = new OrleansSystemRuntime(_testHost!);

            // Act
            var count = await runtime.LocalNode.GetConnectedPeerCountAsync();

            // Assert
            Assert.AreEqual(0, count);
        }

        [TestMethod]
        public async Task TestGetUnconnectedPeerCountAsync_InitialState_ReturnsZero()
        {
            // Arrange
            var runtime = new OrleansSystemRuntime(_testHost!);

            // Act
            var count = await runtime.LocalNode.GetUnconnectedPeerCountAsync();

            // Assert
            Assert.AreEqual(0, count);
        }

        [TestMethod]
        public void TestTell_DoesNotThrow()
        {
            // Arrange
            var runtime = new OrleansSystemRuntime(_testHost!);

            // Act & Assert - should not throw
            runtime.LocalNode.Tell("test message");
        }

        [TestMethod]
        public async Task TestBroadcastAsync_DoesNotThrow()
        {
            // Arrange
            var runtime = new OrleansSystemRuntime(_testHost!);

            // Act & Assert - should not throw
            await runtime.LocalNode.BroadcastAsync(new byte[] { 1, 2, 3 });
        }

        [TestMethod]
        public async Task TestRelayAsync_DoesNotThrow()
        {
            // Arrange
            var runtime = new OrleansSystemRuntime(_testHost!);

            // Act & Assert - should not throw
            await runtime.LocalNode.RelayAsync(new byte[32], (byte)InventoryType.Block);
        }
    }

    /// <summary>
    /// Unit tests for OrleansTaskManagerRuntime.
    /// </summary>
    [TestClass]
    public class OrleansTaskManagerRuntimeTests
    {
        public TestContext TestContext { get; set; } = null!;

        private TestCluster? _cluster;
        private IHost? _testHost;

        [TestInitialize]
        public async Task Setup()
        {
            var builder = new TestClusterBuilder();
            builder.AddSiloBuilderConfigurator<BridgeTestSiloConfigurator>();
            _cluster = builder.Build();
            await _cluster.DeployAsync();

            var hostBuilder = Host.CreateDefaultBuilder()
                .ConfigureServices(services =>
                {
                    services.AddSingleton(_cluster.GrainFactory);
                });
            _testHost = hostBuilder.Build();
        }

        [TestCleanup]
        public async Task Cleanup()
        {
            _testHost?.Dispose();
            if (_cluster != null)
            {
                await _cluster.StopAllSilosAsync();
                await _cluster.DisposeAsync();
            }
        }

        [TestMethod]
        public async Task TestRegisterNodeAsync_DoesNotThrow()
        {
            // Arrange
            var runtime = new OrleansSystemRuntime(_testHost!);

            // Act & Assert - should not throw
            await runtime.TaskManager.RegisterNodeAsync("test-node-1");
        }

        [TestMethod]
        public async Task TestUnregisterNodeAsync_DoesNotThrow()
        {
            // Arrange
            var runtime = new OrleansSystemRuntime(_testHost!);

            // Act & Assert - should not throw
            await runtime.TaskManager.UnregisterNodeAsync("test-node-1");
        }

        [TestMethod]
        public async Task TestGetPendingTaskCountAsync_InitialState_ReturnsZero()
        {
            // Arrange
            var runtime = new OrleansSystemRuntime(_testHost!);

            // Act
            var count = await runtime.TaskManager.GetPendingTaskCountAsync();

            // Assert
            Assert.AreEqual(0, count);
        }

        [TestMethod]
        public async Task TestRequestBlocksAsync_DoesNotThrow()
        {
            // Arrange
            var runtime = new OrleansSystemRuntime(_testHost!);

            // Act & Assert - should not throw
            await runtime.TaskManager.RequestBlocksAsync(0, 10);
        }

        [TestMethod]
        public async Task TestRequestHeadersAsync_DoesNotThrow()
        {
            // Arrange
            var runtime = new OrleansSystemRuntime(_testHost!);

            // Act & Assert - should not throw
            await runtime.TaskManager.RequestHeadersAsync(0);
        }

        [TestMethod]
        public async Task TestNotifyHeadersReceivedAsync_DoesNotThrow()
        {
            // Arrange
            var runtime = new OrleansSystemRuntime(_testHost!);

            // Act & Assert - should not throw
            await runtime.TaskManager.NotifyHeadersReceivedAsync(100);
        }

        [TestMethod]
        public void TestTell_DoesNotThrow()
        {
            // Arrange
            var runtime = new OrleansSystemRuntime(_testHost!);

            // Act & Assert - should not throw
            runtime.TaskManager.Tell("test message");
        }
    }

    /// <summary>
    /// Unit tests for OrleansConsensusRuntime.
    /// </summary>
    [TestClass]
    public class OrleansConsensusRuntimeTests
    {
        public TestContext TestContext { get; set; } = null!;

        private TestCluster? _cluster;
        private IHost? _testHost;

        [TestInitialize]
        public async Task Setup()
        {
            var builder = new TestClusterBuilder();
            builder.AddSiloBuilderConfigurator<BridgeTestSiloConfigurator>();
            _cluster = builder.Build();
            await _cluster.DeployAsync();

            var hostBuilder = Host.CreateDefaultBuilder()
                .ConfigureServices(services =>
                {
                    services.AddSingleton(_cluster.GrainFactory);
                });
            _testHost = hostBuilder.Build();
        }

        [TestCleanup]
        public async Task Cleanup()
        {
            _testHost?.Dispose();
            if (_cluster != null)
            {
                await _cluster.StopAllSilosAsync();
                await _cluster.DisposeAsync();
            }
        }

        [TestMethod]
        public async Task TestInitializeAsync_DoesNotThrow()
        {
            // Arrange
            var runtime = new OrleansSystemRuntime(_testHost!);

            // Act & Assert - should not throw
            await runtime.Consensus!.InitializeAsync(0, 7);
        }

        [TestMethod]
        public async Task TestStartAsync_DoesNotThrow()
        {
            // Arrange
            var runtime = new OrleansSystemRuntime(_testHost!);
            await runtime.Consensus!.InitializeAsync(0, 7);

            // Act & Assert - should not throw
            await runtime.Consensus.StartAsync();
        }

        [TestMethod]
        public async Task TestStopAsync_DoesNotThrow()
        {
            // Arrange
            var runtime = new OrleansSystemRuntime(_testHost!);

            // Act & Assert - should not throw
            await runtime.Consensus!.StopAsync();
        }

        [TestMethod]
        public async Task TestGetViewNumberAsync_InitialState_ReturnsZero()
        {
            // Arrange
            var runtime = new OrleansSystemRuntime(_testHost!);

            // Act
            var viewNumber = await runtime.Consensus!.GetViewNumberAsync();

            // Assert
            Assert.AreEqual((byte)0, viewNumber);
        }

        [TestMethod]
        public async Task TestGetBlockIndexAsync_InitialState_ReturnsZero()
        {
            // Arrange
            var runtime = new OrleansSystemRuntime(_testHost!);

            // Act
            var blockIndex = await runtime.Consensus!.GetBlockIndexAsync();

            // Assert
            Assert.AreEqual(0u, blockIndex);
        }

        [TestMethod]
        public async Task TestIsPrimaryAsync_InitialState_ReturnsFalse()
        {
            // Arrange
            var runtime = new OrleansSystemRuntime(_testHost!);

            // Act
            var isPrimary = await runtime.Consensus!.IsPrimaryAsync();

            // Assert
            Assert.IsFalse(isPrimary);
        }

        [TestMethod]
        public async Task TestIsRunningAsync_InitialState_ReturnsFalse()
        {
            // Arrange
            var runtime = new OrleansSystemRuntime(_testHost!);

            // Act
            var isRunning = await runtime.Consensus!.IsRunningAsync();

            // Assert
            Assert.IsFalse(isRunning);
        }

        [TestMethod]
        public async Task TestOnMessageAsync_DoesNotThrow()
        {
            // Arrange
            var runtime = new OrleansSystemRuntime(_testHost!);

            // Act & Assert - should not throw
            await runtime.Consensus!.OnMessageAsync(new byte[] { 1, 2, 3 }, "sender-address");
        }

        [TestMethod]
        public void TestTell_DoesNotThrow()
        {
            // Arrange
            var runtime = new OrleansSystemRuntime(_testHost!);

            // Act & Assert - should not throw
            runtime.Consensus!.Tell(new byte[] { 1, 2, 3 });
        }
    }

    /// <summary>
    /// Test silo configurator for Bridge tests.
    /// </summary>
    public class BridgeTestSiloConfigurator : ISiloConfigurator
    {
        public void Configure(ISiloBuilder siloBuilder)
        {
            siloBuilder.AddMemoryGrainStorage("BlockchainStore");
            siloBuilder.AddMemoryGrainStorage("MemoryPoolStore");
            siloBuilder.AddMemoryGrainStorage("LocalNodeStore");
            siloBuilder.AddMemoryGrainStorage("ConsensusStore");
            siloBuilder.AddMemoryGrainStorage("RemoteNodeStore");
            siloBuilder.AddMemoryGrainStorage("TaskManagerStore");
            siloBuilder.AddMemoryGrainStorage("TxRouterStore");
            siloBuilder.Services.AddSingleton<IBlockStorageService, InMemoryBlockStorageService>();
            siloBuilder.Services.AddSingleton(new OrleansOptions
            {
                ValidationMode = NeoValidationMode.None,
                ProtocolSettings = TestProtocolSettings.SoleNode,
                NetworkMagic = TestProtocolSettings.SoleNode.Network,
                UseMemoryStorage = true
            });
            siloBuilder.Services.AddSingleton(sp =>
            {
                var options = sp.GetRequiredService<OrleansOptions>();
                var system = new NeoSystem((ProtocolSettings)options.ProtocolSettings!);
                return new NeoSystemAdapter(system) as INeoSystem;
            });
        }
    }
}
