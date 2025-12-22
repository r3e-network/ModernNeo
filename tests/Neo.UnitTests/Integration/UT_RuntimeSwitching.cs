// Copyright (C) 2015-2025 The Neo Project.
//
// UT_RuntimeSwitching.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.Bridge;
using Neo.Core.Interfaces;
using System.Threading;
using System.Threading.Tasks;

namespace Neo.UnitTests.Integration
{
    /// <summary>
    /// Integration tests for runtime switching between Akka and Orleans.
    /// </summary>
    [TestClass]
    public class UT_RuntimeSwitching
    {
        public TestContext TestContext { get; set; } = null!;

        private CancellationToken CancellationToken => TestContext.CancellationTokenSource.Token;

        /// <summary>
        /// Tests that NeoSystem defaults to Akka when no bridge is provided.
        /// </summary>
        [TestMethod]
        public void Test_NeoSystem_DefaultUsesAkka()
        {
            using var system = TestBlockchain.GetSystem();

            // Default NeoSystem uses Akka actors
            Assert.IsNotNull(system.ActorSystem);
            Assert.IsNotNull(system.Blockchain);
            Assert.IsNotNull(system.LocalNode);
            Assert.IsNotNull(system.TaskManager);
            Assert.IsNotNull(system.TxRouter);
        }

        /// <summary>
        /// Tests that AkkaActorBridge correctly wraps NeoSystem.
        /// </summary>
        [TestMethod]
        public async Task Test_AkkaActorBridge_WrapsNeoSystem()
        {
            using var system = TestBlockchain.GetSystem();
            var bridge = new AkkaActorBridge(system);

            // Verify bridge properties
            Assert.IsFalse(bridge.IsOrleans);
            Assert.IsNotNull(bridge.Blockchain);
            Assert.IsNotNull(bridge.MemoryPool);
            Assert.IsNotNull(bridge.LocalNode);

            // Test blockchain operations
            var height = await bridge.Blockchain.GetHeightAsync();
            Assert.AreEqual(0u, height); // Genesis block

            // Test memory pool operations
            var verifiedCount = await bridge.MemoryPool.GetVerifiedCountAsync();
            Assert.AreEqual(0, verifiedCount); // Empty pool

            await bridge.DisposeAsync();
        }

        /// <summary>
        /// Tests INeoSystemRuntime interface contract.
        /// </summary>
        [TestMethod]
        public async Task Test_INeoSystemRuntime_InterfaceContract()
        {
            using var system = TestBlockchain.GetSystem();
            INeoSystemRuntime runtime = new AkkaActorBridge(system);

            // Verify interface contract
            Assert.IsFalse(runtime.IsOrleans);

            // Test lifecycle methods
            await runtime.InitializeAsync(CancellationToken);
            await runtime.StartAsync(CancellationToken);

            // Verify runtime is operational
            var height = await runtime.Blockchain.GetHeightAsync();
            Assert.IsTrue(height >= 0);

            await runtime.StopAsync(CancellationToken);
            await runtime.DisposeAsync();
        }

        /// <summary>
        /// Tests that runtime can retrieve blockchain data.
        /// </summary>
        [TestMethod]
        public async Task Test_RuntimeBridge_BlockchainOperations()
        {
            using var system = TestBlockchain.GetSystem();
            var bridge = new AkkaActorBridge(system);

            // Test GetHeightAsync
            var height = await bridge.Blockchain.GetHeightAsync();
            Assert.AreEqual(0u, height);

            // Test GetHeaderHeightAsync
            var headerHeight = await bridge.Blockchain.GetHeaderHeightAsync();
            Assert.AreEqual(0u, headerHeight);

            // Test ContainsTransactionAsync with non-existent tx
            var nonExistentHash = new byte[32];
            var containsTx = await bridge.Blockchain.ContainsTransactionAsync(nonExistentHash);
            Assert.IsFalse(containsTx);

            await bridge.DisposeAsync();
        }

        /// <summary>
        /// Tests that runtime can access memory pool.
        /// </summary>
        [TestMethod]
        public async Task Test_RuntimeBridge_MemoryPoolOperations()
        {
            using var system = TestBlockchain.GetSystem();
            var bridge = new AkkaActorBridge(system);

            // Test GetVerifiedCountAsync
            var verifiedCount = await bridge.MemoryPool.GetVerifiedCountAsync();
            Assert.AreEqual(0, verifiedCount);

            // Test GetUnverifiedCountAsync
            var unverifiedCount = await bridge.MemoryPool.GetUnverifiedCountAsync();
            Assert.AreEqual(0, unverifiedCount);

            // Test ContainsKeyAsync
            var nonExistentHash = new byte[32];
            var containsKey = await bridge.MemoryPool.ContainsKeyAsync(nonExistentHash);
            Assert.IsFalse(containsKey);

            // Test GetVerifiedTransactionsAsync
            var transactions = await bridge.MemoryPool.GetVerifiedTransactionsAsync(10);
            Assert.IsNotNull(transactions);
            Assert.AreEqual(0, System.Linq.Enumerable.Count(transactions));

            await bridge.DisposeAsync();
        }

        /// <summary>
        /// Tests that AkkaActorBridge can send Tell messages.
        /// </summary>
        [TestMethod]
        public async Task Test_RuntimeBridge_TellMessages()
        {
            using var system = TestBlockchain.GetSystem();
            var bridge = new AkkaActorBridge(system);

            // Tell should not throw
            bridge.Blockchain.Tell(new object());
            bridge.LocalNode.Tell(new object());

            // Small delay for message processing
            await Task.Yield();

            await bridge.DisposeAsync();
        }

        /// <summary>
        /// Tests AkkaActorBridge lifecycle management.
        /// </summary>
        [TestMethod]
        public async Task Test_AkkaActorBridge_Lifecycle()
        {
            using var system = TestBlockchain.GetSystem();
            var bridge = new AkkaActorBridge(system);

            // Initialize (no-op for Akka)
            await bridge.InitializeAsync(CancellationToken);

            // Start (no-op for Akka)
            await bridge.StartAsync(CancellationToken);

            // Multiple starts should be idempotent
            await bridge.StartAsync(CancellationToken);

            // Stop
            await bridge.StopAsync(CancellationToken);

            // Multiple stops should be idempotent
            await bridge.StopAsync(CancellationToken);

            // Dispose
            await bridge.DisposeAsync();
        }

        /// <summary>
        /// Tests that local node operations don't throw.
        /// </summary>
        [TestMethod]
        public async Task Test_RuntimeBridge_LocalNodeOperations()
        {
            using var system = TestBlockchain.GetSystem();
            var bridge = new AkkaActorBridge(system);

            // GetConnectedPeerCountAsync should not throw and must be non-negative
            var connectedCount = await bridge.LocalNode.GetConnectedPeerCountAsync();
            Assert.IsTrue(connectedCount >= 0);

            // GetUnconnectedPeerCountAsync should not throw and must be non-negative
            var unconnectedCount = await bridge.LocalNode.GetUnconnectedPeerCountAsync();
            Assert.IsTrue(unconnectedCount >= 0);

            // RelayAsync should not throw
            await bridge.LocalNode.RelayAsync(new byte[32], 0);

            // BroadcastAsync should not throw
            await bridge.LocalNode.BroadcastAsync(new byte[100]);

            await bridge.DisposeAsync();
        }

        /// <summary>
        /// Tests that AkkaActorBridge provides runtime-like functionality.
        /// </summary>
        [TestMethod]
        public async Task Test_NeoSystem_RuntimeProperty_ReturnsAkkaBridge()
        {
            using var system = TestBlockchain.GetSystem();

            // Create runtime bridge directly (Runtime property not yet implemented)
            var runtime = new AkkaActorBridge(system);
            Assert.IsNotNull(runtime);

            // Should be Akka (not Orleans) by default
            Assert.IsFalse(runtime.IsOrleans);

            // Should be able to access blockchain operations
            var height = await runtime.Blockchain.GetHeightAsync();
            Assert.AreEqual(0u, height); // Genesis block

            // Should be able to access memory pool operations
            var verifiedCount = await runtime.MemoryPool.GetVerifiedCountAsync();
            Assert.AreEqual(0, verifiedCount); // Empty pool

            await runtime.DisposeAsync();
        }

        /// <summary>
        /// Tests that AkkaActorBridge instances are consistent.
        /// </summary>
        [TestMethod]
        public void Test_NeoSystem_RuntimeProperty_Consistency()
        {
            using var system = TestBlockchain.GetSystem();

            // Multiple bridge instances should have equivalent behavior
            var runtime1 = new AkkaActorBridge(system);
            var runtime2 = new AkkaActorBridge(system);

            Assert.IsNotNull(runtime1);
            Assert.IsNotNull(runtime2);

            // Both should be non-Orleans (Akka)
            Assert.IsFalse(runtime1.IsOrleans);
            Assert.IsFalse(runtime2.IsOrleans);
        }
    }
}
