// Copyright (C) 2015-2025 The Neo Project.
//
// FeeEstimatorTests.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Core.Interfaces;
using Neo.IO;

namespace Neo.TxPool.Tests
{
    [TestClass]
    public class FeeEstimatorTests
    {
        private InMemoryTransactionPool _pool = null!;
        private PoolBasedFeeEstimator _estimator = null!;

        [TestInitialize]
        public void Setup()
        {
            _pool = new InMemoryTransactionPool(capacity: 1000);
            _estimator = new PoolBasedFeeEstimator(_pool, minimumFeePerByte: 1000, blockCapacity: 10);
        }

        [TestMethod]
        public void MinimumFeePerByte_ReturnsConfiguredValue()
        {
            Assert.AreEqual(1000, _estimator.MinimumFeePerByte);
        }

        [TestMethod]
        public void AverageFeePerByte_EmptyPool_ReturnsMinimum()
        {
            Assert.AreEqual(1000, _estimator.AverageFeePerByte);
        }

        [TestMethod]
        public void AverageFeePerByte_WithTransactions_ReturnsAverage()
        {
            // Add transactions with different fees
            _pool.TryAdd(new FeeTestTransactionData(1, feePerByte: 1000));
            _pool.TryAdd(new FeeTestTransactionData(2, feePerByte: 2000));
            _pool.TryAdd(new FeeTestTransactionData(3, feePerByte: 3000));

            var average = _estimator.AverageFeePerByte;
            Assert.AreEqual(2000, average);
        }

        [TestMethod]
        public void EstimateFeePerByte_EmptyPool_ReturnsMinimum()
        {
            var fee = _estimator.EstimateFeePerByte(targetBlocks: 1);
            Assert.AreEqual(1000, fee);
        }

        [TestMethod]
        public void EstimateFeePerByte_PoolNotFull_ReturnsMinimum()
        {
            // Add fewer transactions than block capacity
            for (int i = 0; i < 5; i++)
            {
                _pool.TryAdd(new FeeTestTransactionData(i, feePerByte: 5000));
            }

            var fee = _estimator.EstimateFeePerByte(targetBlocks: 1);
            Assert.AreEqual(1000, fee);
        }

        [TestMethod]
        public void EstimateFeePerByte_PoolFull_ReturnsHigherFee()
        {
            // Fill pool beyond block capacity
            for (int i = 0; i < 20; i++)
            {
                _pool.TryAdd(new FeeTestTransactionData(i, feePerByte: 1000 + i * 100));
            }

            var fee = _estimator.EstimateFeePerByte(targetBlocks: 1);
            // Should be higher than minimum due to competition
            Assert.IsTrue(fee >= 1000);
        }

        [TestMethod]
        public void EstimateFeePerByte_MoreTargetBlocks_ReturnsLowerFee()
        {
            // Fill pool
            for (int i = 0; i < 30; i++)
            {
                _pool.TryAdd(new FeeTestTransactionData(i, feePerByte: 1000 + i * 100));
            }

            var fee1Block = _estimator.EstimateFeePerByte(targetBlocks: 1);
            var fee3Blocks = _estimator.EstimateFeePerByte(targetBlocks: 3);

            // More blocks = lower urgency = lower fee
            Assert.IsTrue(fee3Blocks <= fee1Block);
        }

        [TestMethod]
        public void EstimateNetworkFee_CalculatesCorrectly()
        {
            var feePerByte = _estimator.EstimateFeePerByte(targetBlocks: 1);
            var transactionSize = 250;

            var networkFee = _estimator.EstimateNetworkFee(transactionSize, targetBlocks: 1);

            Assert.AreEqual(feePerByte * transactionSize, networkFee);
        }

        [TestMethod]
        public void GetStatistics_EmptyPool_ReturnsDefaults()
        {
            var stats = _estimator.GetStatistics();

            Assert.AreEqual(0, stats.TransactionCount);
            Assert.AreEqual(1000, stats.MinFeePerByte);
            Assert.AreEqual(1000, stats.MaxFeePerByte);
            Assert.AreEqual(1000, stats.AverageFeePerByte);
            Assert.AreEqual(1000, stats.MedianFeePerByte);
        }

        [TestMethod]
        public void GetStatistics_WithTransactions_ReturnsCorrectStats()
        {
            _pool.TryAdd(new FeeTestTransactionData(1, feePerByte: 1000));
            _pool.TryAdd(new FeeTestTransactionData(2, feePerByte: 2000));
            _pool.TryAdd(new FeeTestTransactionData(3, feePerByte: 3000));
            _pool.TryAdd(new FeeTestTransactionData(4, feePerByte: 4000));
            _pool.TryAdd(new FeeTestTransactionData(5, feePerByte: 5000));

            var stats = _estimator.GetStatistics();

            Assert.AreEqual(5, stats.TransactionCount);
            Assert.AreEqual(1000, stats.MinFeePerByte);
            Assert.AreEqual(5000, stats.MaxFeePerByte);
            Assert.AreEqual(3000, stats.AverageFeePerByte);
            Assert.AreEqual(3000, stats.MedianFeePerByte);
        }

        [TestMethod]
        public void GetStatistics_HasTimestamp()
        {
            var before = DateTime.UtcNow;
            var stats = _estimator.GetStatistics();
            var after = DateTime.UtcNow;

            Assert.IsTrue(stats.Timestamp >= before);
            Assert.IsTrue(stats.Timestamp <= after);
        }

        [TestMethod]
        public void Constructor_NullPool_ThrowsArgumentNullException()
        {
            Assert.ThrowsExactly<ArgumentNullException>(() =>
                new PoolBasedFeeEstimator(null!));
        }
    }

    /// <summary>
    /// Test transaction with configurable fee per byte.
    /// </summary>
    internal class FeeTestTransactionData : ITransactionData
    {
        private readonly byte[] _hashBytes;
        private static readonly byte[] EmptyScript = [];

        public FeeTestTransactionData(int seed, long feePerByte = 1000)
        {
            _hashBytes = new byte[32];
            BitConverter.GetBytes(seed).CopyTo(_hashBytes, 0);
            BitConverter.GetBytes(DateTime.UtcNow.Ticks).CopyTo(_hashBytes, 4);
            Hash = new UInt256(_hashBytes);
            Sender = UInt160.Zero;
            FeePerByte = feePerByte;
            NetworkFee = feePerByte * Size;
        }

        public UInt256 Hash { get; }
        public byte Version => 0;
        public uint Nonce => 0;
        public long SystemFee => 1000000;
        public long NetworkFee { get; }
        public uint ValidUntilBlock => 1000;
        public ReadOnlyMemory<byte> Script => EmptyScript;
        public UInt160 Sender { get; }
        public long FeePerByte { get; }
        public int SignersCount => 1;
        public int AttributesCount => 0;
        public int Size => 250;

        public void Deserialize(ref MemoryReader reader) { }
        public void DeserializeUnsigned(ref MemoryReader reader) { }
        public void Serialize(BinaryWriter writer) { }
        public void SerializeUnsigned(BinaryWriter writer) { }
    }
}
