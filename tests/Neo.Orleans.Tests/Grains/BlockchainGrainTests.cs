// Copyright (C) 2015-2025 The Neo Project.
//
// BlockchainGrainTests.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Microsoft.Extensions.DependencyInjection;
using Neo;
using Neo.Core.Interfaces;
using Neo.Cryptography;
using Neo.Network.P2P.Payloads;
using Neo.Orleans.Adapters;
using Neo.Orleans.Grains;
using Neo.Orleans.Hosting;
using Neo.Orleans.Interfaces;
using Neo.Orleans.Options;
using Neo.Orleans.Services;
using Neo.Orleans.States;
using Neo.Orleans.Tests;
using Orleans.Runtime;
using Orleans.TestingHost;
using System.Linq;

namespace Neo.Orleans.Tests.Grains
{
    /// <summary>
    /// Unit tests for BlockchainGrain.
    /// </summary>
    [TestClass]
    public class BlockchainGrainTests
    {
        private TestCluster? _cluster;

        [TestInitialize]
        public async Task Setup()
        {
            var builder = new TestClusterBuilder();
            builder.AddSiloBuilderConfigurator<TestSiloConfigurator>();
            _cluster = builder.Build();
            await _cluster.DeployAsync();
        }

        [TestCleanup]
        public async Task Cleanup()
        {
            if (_cluster != null)
            {
                await _cluster.StopAllSilosAsync();
                await _cluster.DisposeAsync();
            }
        }

        [TestMethod]
        public async Task GetHeightAsync_InitialState_ReturnsZero()
        {
            // Arrange
            var grain = _cluster!.GrainFactory.GetGrain<IBlockchainGrain>(0);

            // Act
            var height = await grain.GetHeightAsync();

            // Assert
            Assert.AreEqual(0u, height);
        }

        [TestMethod]
        public async Task PersistBlockAsync_FirstBlock_Succeeds()
        {
            // Arrange
            var grain = _cluster!.GrainFactory.GetGrain<IBlockchainGrain>(0);
            var block = CreateBlock(index: 1, timestamp: 1000);

            // Act
            var result = await grain.PersistBlockAsync(block);

            // Assert
            Assert.AreEqual(BlockVerifyResult.Succeed, result);
            Assert.AreEqual(1u, await grain.GetHeightAsync());
        }

        [TestMethod]
        public async Task PersistBlockAsync_SequentialBlocks_Succeeds()
        {
            // Arrange
            var grain = _cluster!.GrainFactory.GetGrain<IBlockchainGrain>(0);
            var block1 = CreateBlock(index: 1, timestamp: 1000);
            var block2 = CreateBlock(index: 2, timestamp: 2000);
            var block3 = CreateBlock(index: 3, timestamp: 3000);

            // Act
            var result1 = await grain.PersistBlockAsync(block1);
            var result2 = await grain.PersistBlockAsync(block2);
            var result3 = await grain.PersistBlockAsync(block3);

            // Assert
            Assert.AreEqual(BlockVerifyResult.Succeed, result1);
            Assert.AreEqual(BlockVerifyResult.Succeed, result2);
            Assert.AreEqual(BlockVerifyResult.Succeed, result3);
            Assert.AreEqual(3u, await grain.GetHeightAsync());
        }

        [TestMethod]
        public async Task PersistBlockAsync_NonSequentialBlock_Fails()
        {
            // Arrange
            var grain = _cluster!.GrainFactory.GetGrain<IBlockchainGrain>(0);
            var block1 = CreateBlock(index: 1, timestamp: 1000);
            var block3 = CreateBlock(index: 3, timestamp: 3000); // Skip block 2

            // Act
            await grain.PersistBlockAsync(block1);
            var result = await grain.PersistBlockAsync(block3);

            // Assert
            Assert.AreEqual(BlockVerifyResult.UnableToVerify, result);
            Assert.AreEqual(1u, await grain.GetHeightAsync());
        }

        [TestMethod]
        public async Task PersistBlockAsync_DuplicateBlock_Fails()
        {
            // Arrange
            var grain = _cluster!.GrainFactory.GetGrain<IBlockchainGrain>(0);
            var block1 = CreateBlock(index: 1, timestamp: 1000);
            var block1Dup = CreateBlock(index: 1, timestamp: 1001);

            // Act
            await grain.PersistBlockAsync(block1);
            var result = await grain.PersistBlockAsync(block1Dup);

            // Assert
            Assert.AreEqual(BlockVerifyResult.AlreadyExists, result);
        }

        [TestMethod]
        public async Task ImportBlocksAsync_OrderedBlocks_ImportsAll()
        {
            // Arrange
            var grain = _cluster!.GrainFactory.GetGrain<IBlockchainGrain>(0);
            var blocks = new[]
            {
                CreateBlock(index: 1, timestamp: 1000),
                CreateBlock(index: 2, timestamp: 2000),
                CreateBlock(index: 3, timestamp: 3000)
            };

            // Act
            var count = await grain.ImportBlocksAsync(blocks);

            // Assert
            Assert.AreEqual(3, count);
            Assert.AreEqual(3u, await grain.GetHeightAsync());
        }

        [TestMethod]
        public async Task ImportBlocksAsync_UnorderedBlocks_SortsAndImports()
        {
            // Arrange
            var grain = _cluster!.GrainFactory.GetGrain<IBlockchainGrain>(0);
            var blocks = new[]
            {
                CreateBlock(index: 3, timestamp: 3000),
                CreateBlock(index: 1, timestamp: 1000),
                CreateBlock(index: 2, timestamp: 2000)
            };

            // Act
            var count = await grain.ImportBlocksAsync(blocks);

            // Assert
            Assert.AreEqual(3, count);
            Assert.AreEqual(3u, await grain.GetHeightAsync());
        }

        [TestMethod]
        public async Task ImportBlocksAsync_WithGaps_ImportsOnlyValid()
        {
            // Arrange
            var grain = _cluster!.GrainFactory.GetGrain<IBlockchainGrain>(0);
            var blocks = new[]
            {
                CreateBlock(index: 1, timestamp: 1000),
                CreateBlock(index: 2, timestamp: 2000),
                CreateBlock(index: 5, timestamp: 5000) // Gap - should fail
            };

            // Act
            var count = await grain.ImportBlocksAsync(blocks);

            // Assert
            Assert.AreEqual(2, count);
            Assert.AreEqual(2u, await grain.GetHeightAsync());
        }

        [TestMethod]
        public async Task GetCurrentBlockHashAsync_AfterPersist_ReturnsCorrectHash()
        {
            // Arrange
            var grain = _cluster!.GrainFactory.GetGrain<IBlockchainGrain>(0);
            var block = CreateBlock(index: 1, timestamp: 1000);

            // Act
            await grain.PersistBlockAsync(block);
            var hash = await grain.GetCurrentBlockHashAsync();

            // Assert
            Assert.IsNotNull(hash);
            Assert.AreEqual(32, hash.Length);
            CollectionAssert.AreEqual(block.Hash.GetSpan().ToArray(), hash);
        }

        [TestMethod]
        public async Task GetBlockByHashAsync_ExistingBlock_ReturnsBlock()
        {
            // Arrange
            var grain = _cluster!.GrainFactory.GetGrain<IBlockchainGrain>(0);
            var block = CreateBlock(index: 1, timestamp: 1000);
            await grain.PersistBlockAsync(block);

            // Act
            var retrieved = await grain.GetBlockByHashAsync(block.Hash.GetSpan().ToArray());

            // Assert
            Assert.IsNotNull(retrieved);
            Assert.AreEqual(block.Index, retrieved!.Index);
        }

        [TestMethod]
        public async Task GetBlockByIndexAsync_ExistingBlock_ReturnsBlock()
        {
            // Arrange
            var grain = _cluster!.GrainFactory.GetGrain<IBlockchainGrain>(0);
            var block = CreateBlock(index: 1, timestamp: 1000);
            await grain.PersistBlockAsync(block);

            // Act
            var retrieved = await grain.GetBlockByIndexAsync(1);

            // Assert
            Assert.IsNotNull(retrieved);
            Assert.AreEqual(block.Hash, retrieved!.Hash);
        }

        [TestMethod]
        public async Task FillMemoryPoolAsync_WithTransactions_AddsToMemoryPool()
        {
            // Arrange
            var blockchain = _cluster!.GrainFactory.GetGrain<IBlockchainGrain>(0);
            var memPool = _cluster.GrainFactory.GetGrain<IMemoryPoolGrain>(0);
            await memPool.ClearAsync();

            var transactions = new ITransactionData[]
            {
                new MockTransactionData(nonce: 1, feePerByte: 1000),
                new MockTransactionData(nonce: 2, feePerByte: 2000)
            };

            // Act
            await blockchain.FillMemoryPoolAsync(transactions);
            var count = await memPool.GetCountAsync();

            // Assert
            Assert.AreEqual(2, count);
        }

        [TestMethod]
        public async Task FillMemoryPoolAsync_WithTransactionHashes_AddsToMemoryPool()
        {
            // Arrange
            var blockchain = _cluster!.GrainFactory.GetGrain<IBlockchainGrain>(0);
            var memPool = _cluster.GrainFactory.GetGrain<IMemoryPoolGrain>(0);
            await memPool.ClearAsync();

            var tx1 = CreateTestTransaction(1);
            var tx2 = CreateTestTransaction(2);
            var block = CreateTestBlock(1, tx1, tx2);

            await blockchain.PersistBlockAsync(block);

            // Act
            await blockchain.FillMemoryPoolAsync(new[]
            {
                tx1.Hash.GetSpan().ToArray(),
                tx2.Hash.GetSpan().ToArray()
            });
            var count = await memPool.GetCountAsync();

            // Assert
            Assert.AreEqual(2, count);
        }

        private static Block CreateTestBlock(uint index, params Transaction[] transactions)
        {
            var hashes = transactions.Select(tx => tx.Hash).ToArray();
            var merkleRoot = hashes.Length == 0 ? UInt256.Zero : MerkleTree.ComputeRoot(hashes);

            var header = new Header
            {
                Version = 0,
                PrevHash = UInt256.Zero,
                MerkleRoot = merkleRoot,
                Timestamp = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                Nonce = 0,
                Index = index,
                PrimaryIndex = 0,
                NextConsensus = UInt160.Zero,
                Witness = new Witness
                {
                    InvocationScript = Array.Empty<byte>(),
                    VerificationScript = Array.Empty<byte>()
                }
            };

            return new Block
            {
                Header = header,
                Transactions = transactions
            };
        }

        private static Block CreateBlock(uint index, ulong timestamp)
        {
            var header = new Header
            {
                Version = 0,
                PrevHash = UInt256.Zero,
                MerkleRoot = UInt256.Zero,
                Timestamp = timestamp,
                Nonce = 0,
                Index = index,
                PrimaryIndex = 0,
                NextConsensus = UInt160.Zero,
                Witness = Witness.Empty
            };

            return new Block
            {
                Header = header,
                Transactions = Array.Empty<Transaction>()
            };
        }

        private static Transaction CreateTestTransaction(uint nonce)
        {
            return new Transaction
            {
                Version = 0,
                Nonce = nonce,
                SystemFee = 0,
                NetworkFee = 0,
                ValidUntilBlock = 1_000_000,
                Signers = [new Signer { Account = UInt160.Zero, Scopes = WitnessScope.None }],
                Attributes = Array.Empty<TransactionAttribute>(),
                Script = new byte[] { 0x01 },
                Witnesses = [new Witness { InvocationScript = Array.Empty<byte>(), VerificationScript = Array.Empty<byte>() }]
            };
        }
    }

    /// <summary>
    /// Test silo configurator for Orleans TestCluster.
    /// </summary>
    public class TestSiloConfigurator : ISiloConfigurator
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
