// Copyright (C) 2015-2025 The Neo Project.
//
// MemoryPoolGrainTests.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo;
using Neo.Core.Interfaces;
using Neo.IO;
using Neo.Network.P2P.Payloads;
using Neo.Orleans.Grains;
using Neo.Orleans.Interfaces;
using Orleans.TestingHost;

namespace Neo.Orleans.Tests.Grains
{
    /// <summary>
    /// Unit tests for MemoryPoolGrain.
    /// </summary>
    [TestClass]
    public class MemoryPoolGrainTests
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
        public async Task GetCountAsync_EmptyPool_ReturnsZero()
        {
            var grain = _cluster!.GrainFactory.GetGrain<IMemoryPoolGrain>(0);
            var count = await grain.GetCountAsync();
            Assert.AreEqual(0, count);
        }

        [TestMethod]
        public async Task AddTransactionAsync_ValidTransaction_ReturnsSucceed()
        {
            var grain = _cluster!.GrainFactory.GetGrain<IMemoryPoolGrain>(0);
            var tx = new MockTransactionData(nonce: 1, feePerByte: 1000);

            var result = await grain.AddTransactionAsync(tx);

            Assert.AreEqual(MemoryPoolAddResult.Succeed, result);
            Assert.AreEqual(1, await grain.GetCountAsync());
        }

        [TestMethod]
        public async Task AddTransactionAsync_DuplicateTransaction_ReturnsFalse()
        {
            var grain = _cluster!.GrainFactory.GetGrain<IMemoryPoolGrain>(0);
            var tx = new MockTransactionData(nonce: 1, feePerByte: 1000);

            await grain.AddTransactionAsync(tx);
            var result = await grain.AddTransactionAsync(tx);

            Assert.AreEqual(MemoryPoolAddResult.AlreadyInPool, result);
            Assert.AreEqual(1, await grain.GetCountAsync());
        }

        [TestMethod]
        public async Task AddTransactionAsync_MultipleTransactions_AllAdded()
        {
            var grain = _cluster!.GrainFactory.GetGrain<IMemoryPoolGrain>(0);
            var tx1 = new MockTransactionData(nonce: 1, feePerByte: 1000);
            var tx2 = new MockTransactionData(nonce: 2, feePerByte: 2000);
            var tx3 = new MockTransactionData(nonce: 3, feePerByte: 3000);

            await grain.AddTransactionAsync(tx1);
            await grain.AddTransactionAsync(tx2);
            await grain.AddTransactionAsync(tx3);

            Assert.AreEqual(3, await grain.GetCountAsync());
        }

        [TestMethod]
        public async Task ContainsAsync_ExistingTransaction_ReturnsTrue()
        {
            var grain = _cluster!.GrainFactory.GetGrain<IMemoryPoolGrain>(0);
            var tx = new MockTransactionData(nonce: 1, feePerByte: 1000);

            await grain.AddTransactionAsync(tx);
            var result = await grain.ContainsAsync(tx.Hash.GetSpan().ToArray());

            Assert.IsTrue(result);
        }

        [TestMethod]
        public async Task ContainsAsync_NonExistingTransaction_ReturnsFalse()
        {
            var grain = _cluster!.GrainFactory.GetGrain<IMemoryPoolGrain>(0);
            var result = await grain.ContainsAsync(new byte[32]);
            Assert.IsFalse(result);
        }

        [TestMethod]
        public async Task RemoveTransactionAsync_ExistingTransaction_ReturnsTrue()
        {
            var grain = _cluster!.GrainFactory.GetGrain<IMemoryPoolGrain>(0);
            var tx = new MockTransactionData(nonce: 1, feePerByte: 1000);

            await grain.AddTransactionAsync(tx);
            var result = await grain.RemoveTransactionAsync(tx.Hash.GetSpan().ToArray());

            Assert.IsTrue(result);
            Assert.AreEqual(0, await grain.GetCountAsync());
        }

        [TestMethod]
        public async Task RemoveTransactionAsync_NonExistingTransaction_ReturnsFalse()
        {
            var grain = _cluster!.GrainFactory.GetGrain<IMemoryPoolGrain>(0);
            var result = await grain.RemoveTransactionAsync(new byte[32]);
            Assert.IsFalse(result);
        }

        [TestMethod]
        public async Task ClearAsync_WithTransactions_RemovesAll()
        {
            var grain = _cluster!.GrainFactory.GetGrain<IMemoryPoolGrain>(0);
            var tx1 = new MockTransactionData(nonce: 1, feePerByte: 1000);
            var tx2 = new MockTransactionData(nonce: 2, feePerByte: 2000);

            await grain.AddTransactionAsync(tx1);
            await grain.AddTransactionAsync(tx2);
            await grain.ClearAsync();

            Assert.AreEqual(0, await grain.GetCountAsync());
        }

        [TestMethod]
        public async Task GetVerifiedTransactionsAsync_AfterAdd_ReturnsTransaction()
        {
            var grain = _cluster!.GrainFactory.GetGrain<IMemoryPoolGrain>(0);
            var tx = new MockTransactionData(nonce: 1, feePerByte: 1000);

            await grain.AddTransactionAsync(tx);
            var result = await grain.GetVerifiedTransactionsAsync(10);

            Assert.IsNotNull(result);
            Assert.AreEqual(1, result.Count());
        }

        [TestMethod]
        public async Task AddRemoveAdd_SameTransaction_WorksCorrectly()
        {
            var grain = _cluster!.GrainFactory.GetGrain<IMemoryPoolGrain>(0);
            var tx = new MockTransactionData(nonce: 1, feePerByte: 1000);

            await grain.AddTransactionAsync(tx);
            await grain.RemoveTransactionAsync(tx.Hash.GetSpan().ToArray());
            var result = await grain.AddTransactionAsync(tx);

            Assert.AreEqual(MemoryPoolAddResult.Succeed, result);
            Assert.AreEqual(1, await grain.GetCountAsync());
        }

        [TestMethod]
        public async Task GetTransactionAsync_AfterAdd_ReturnsTransaction()
        {
            var grain = _cluster!.GrainFactory.GetGrain<IMemoryPoolGrain>(0);
            var tx = CreateTestTransaction(1);

            await grain.AddTransactionAsync(tx);
            var retrieved = await grain.GetTransactionAsync(tx.Hash.GetSpan().ToArray());

            Assert.IsNotNull(retrieved);
            Assert.AreEqual(tx.Hash, retrieved.Hash);
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
    /// Mock implementation of ITransactionData for testing.
    /// </summary>
    [GenerateSerializer]
    [Alias("Neo.Orleans.Tests.MockTransactionData")]
    internal class MockTransactionData : ITransactionData
    {
        public MockTransactionData() : this(0, 0) { }

        public MockTransactionData(uint nonce, long feePerByte)
        {
            Nonce = nonce;
            FeePerByte = feePerByte;
            NetworkFee = feePerByte * 100;
            SystemFee = 1000000;
            ValidUntilBlock = 1000000;

            var hashBytes = new byte[32];
            BitConverter.GetBytes(nonce).CopyTo(hashBytes, 0);
            BitConverter.GetBytes(feePerByte).CopyTo(hashBytes, 4);
            Hash = new UInt256(hashBytes);
            Sender = UInt160.Zero;
        }

        [Id(0)] public UInt256 Hash { get; private set; }
        public byte Version => 0;
        [Id(1)] public uint Nonce { get; private set; }
        [Id(2)] public long SystemFee { get; private set; }
        [Id(3)] public long NetworkFee { get; private set; }
        [Id(4)] public uint ValidUntilBlock { get; private set; }
        public ReadOnlyMemory<byte> Script => Array.Empty<byte>();
        [Id(5)] public UInt160 Sender { get; private set; }
        [Id(6)] public long FeePerByte { get; private set; }
        public int SignersCount => 1;
        public int AttributesCount => 0;
        public int Size => 100;

        public void Deserialize(ref MemoryReader reader) { }
        public void DeserializeUnsigned(ref MemoryReader reader) { }
        public void Serialize(System.IO.BinaryWriter writer) { }
        public void SerializeUnsigned(System.IO.BinaryWriter writer) { }
    }
}
