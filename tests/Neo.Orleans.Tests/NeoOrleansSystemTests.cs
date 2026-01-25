// Copyright (C) 2015-2025 The Neo Project.
//
// NeoOrleansSystemTests.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo;
using Neo.Network.P2P.Payloads;
using Neo.Orleans.Hosting;
using Neo.Orleans.Interfaces;
using System.Linq;

namespace Neo.Orleans.Tests
{
    [TestClass]
    public class NeoOrleansSystemTests
    {
        public TestContext TestContext { get; set; } = null!;

        [TestMethod]
        public async Task CreateDevelopment_ReturnsValidSystem()
        {
            // Arrange & Act
            await using var system = NeoOrleansSystem.CreateDevelopment();

            // Assert
            Assert.IsNotNull(system);
            Assert.IsNotNull(system.ActorBridge);
            Assert.IsFalse(system.IsRunning);
        }

        [TestMethod]
        public async Task StartAsync_StartsSystem()
        {
            // Arrange
            await using var system = NeoOrleansSystem.CreateDevelopment();

            // Act
            await system.StartAsync(TestContext.CancellationTokenSource.Token);

            // Assert
            Assert.IsTrue(system.IsRunning);
        }

        [TestMethod]
        public async Task StopAsync_StopsSystem()
        {
            // Arrange
            await using var system = NeoOrleansSystem.CreateDevelopment();
            await system.StartAsync(TestContext.CancellationTokenSource.Token);
            Assert.IsTrue(system.IsRunning);

            // Act
            await system.StopAsync(TestContext.CancellationTokenSource.Token);

            // Assert
            Assert.IsFalse(system.IsRunning);
        }

        [TestMethod]
        public async Task GetBlockchainHeightAsync_ReturnsZeroInitially()
        {
            // Arrange
            await using var system = NeoOrleansSystem.CreateDevelopment();
            await system.StartAsync(TestContext.CancellationTokenSource.Token);

            // Act
            var height = await system.GetBlockchainHeightAsync();

            // Assert
            Assert.AreEqual(0u, height);
        }

        [TestMethod]
        public async Task GetMemoryPoolCountAsync_ReturnsZeroInitially()
        {
            // Arrange
            await using var system = NeoOrleansSystem.CreateDevelopment();
            await system.StartAsync(TestContext.CancellationTokenSource.Token);

            // Act
            var count = await system.GetMemoryPoolCountAsync();

            // Assert
            Assert.AreEqual(0, count);
        }

        [TestMethod]
        public async Task GetBlockchainStateAsync_ReturnsValidState()
        {
            // Arrange
            await using var system = NeoOrleansSystem.CreateDevelopment();
            await system.StartAsync(TestContext.CancellationTokenSource.Token);

            // Act
            var state = await system.GetBlockchainStateAsync();

            // Assert
            Assert.IsNotNull(state);
            Assert.AreEqual(0u, state.Height);
            Assert.IsTrue(state.IsInitialized);
        }

        [TestMethod]
        public async Task Blockchain_ReturnsValidGrain()
        {
            // Arrange
            await using var system = NeoOrleansSystem.CreateDevelopment();
            await system.StartAsync(TestContext.CancellationTokenSource.Token);

            // Act
            var blockchain = system.Blockchain;

            // Assert
            Assert.IsNotNull(blockchain);
        }

        [TestMethod]
        public async Task MemoryPool_ReturnsValidGrain()
        {
            // Arrange
            await using var system = NeoOrleansSystem.CreateDevelopment();
            await system.StartAsync(TestContext.CancellationTokenSource.Token);

            // Act
            var memPool = system.MemoryPool;

            // Assert
            Assert.IsNotNull(memPool);
        }

        [TestMethod]
        public async Task LocalNode_ReturnsValidGrain()
        {
            // Arrange
            await using var system = NeoOrleansSystem.CreateDevelopment();
            await system.StartAsync(TestContext.CancellationTokenSource.Token);

            // Act
            var localNode = system.LocalNode;

            // Assert
            Assert.IsNotNull(localNode);
        }

        [TestMethod]
        public async Task StartAsync_ThrowsWhenAlreadyStarted()
        {
            // Arrange
            await using var system = NeoOrleansSystem.CreateDevelopment();
            await system.StartAsync(TestContext.CancellationTokenSource.Token);

            // Act & Assert
            await Assert.ThrowsExactlyAsync<InvalidOperationException>(
                () => system.StartAsync(TestContext.CancellationTokenSource.Token));
        }

        [TestMethod]
        public async Task DisposeAsync_CanBeCalledMultipleTimes()
        {
            // Arrange
            var system = NeoOrleansSystem.CreateDevelopment();
            await system.StartAsync(TestContext.CancellationTokenSource.Token);

            // Act - should not throw
            await system.DisposeAsync();
            await system.DisposeAsync();

            // Assert - no exception thrown
            Assert.IsFalse(system.IsRunning);
        }

        [TestMethod]
        public async Task Create_WithCustomOptions_Works()
        {
            // Arrange & Act
            await using var system = NeoOrleansSystem.Create(opts =>
            {
                opts.UseMemoryStorage = true;
                opts.ClusterId = "test-cluster";
                opts.ServiceId = "test-service";
            });

            // Assert
            Assert.IsNotNull(system);
        }

        [TestMethod]
        public async Task ActorBridge_IsOrleans_ReturnsTrue()
        {
            // Arrange
            await using var system = NeoOrleansSystem.CreateDevelopment();

            // Act & Assert
            Assert.IsTrue(system.ActorBridge.IsOrleans);
        }

        [TestMethod]
        public async Task ActorBridge_MemoryPool_GetVerifiedTransactionsAsync_ReturnsDeserializedTransactions()
        {
            // Arrange
            await using var system = NeoOrleansSystem.Create(options =>
            {
                options.ValidationMode = NeoValidationMode.None;
                options.ProtocolSettings = TestProtocolSettings.SoleNode;
                options.UseMemoryStorage = true;
            });
            await system.StartAsync(TestContext.CancellationTokenSource.Token);

            var tx = new Transaction
            {
                Version = 0,
                Nonce = 42,
                SystemFee = 0,
                NetworkFee = 100_000,
                ValidUntilBlock = 100,
                Signers = [new Signer { Account = UInt160.Zero, Scopes = WitnessScope.None }],
                Attributes = [],
                Script = new byte[] { 0x51 }, // PUSH1
                Witnesses = [Witness.Empty]
            };

            // Act
            var added = await system.ActorBridge.MemoryPool.AddTransactionAsync(tx);
            var verified = (await system.ActorBridge.MemoryPool.GetVerifiedTransactionsAsync(10)).ToList();

            // Assert
            Assert.IsTrue(added);
            Assert.AreEqual(1, verified.Count);
            Assert.AreEqual(tx.Hash, verified[0].Hash);
            Assert.AreEqual(tx.Nonce, verified[0].Nonce);
        }
    }
}
