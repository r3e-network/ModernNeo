// Copyright (C) 2015-2025 The Neo Project.
//
// InMemoryTransactionPoolTests.cs file belongs to the neo project and is free
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
    public class InMemoryTransactionPoolTests
    {
        private InMemoryTransactionPool _pool = null!;

        [TestInitialize]
        public void Setup()
        {
            _pool = new InMemoryTransactionPool(capacity: 100);
        }

        [TestMethod]
        public void TryAdd_NewTransaction_ReturnsTrue()
        {
            var tx = new TestTransactionData(1);
            var result = _pool.TryAdd(tx);
            Assert.IsTrue(result);
            Assert.AreEqual(1, _pool.Count);
        }

        [TestMethod]
        public void TryAdd_DuplicateTransaction_ReturnsFalse()
        {
            var tx = new TestTransactionData(1);
            _pool.TryAdd(tx);
            var result = _pool.TryAdd(tx);
            Assert.IsFalse(result);
            Assert.AreEqual(1, _pool.Count);
        }

        [TestMethod]
        public void Contains_ExistingTransaction_ReturnsTrue()
        {
            var tx = new TestTransactionData(1);
            _pool.TryAdd(tx);
            var result = _pool.Contains(tx.Hash.GetSpan().ToArray());
            Assert.IsTrue(result);
        }

        [TestMethod]
        public void Contains_NonExistingTransaction_ReturnsFalse()
        {
            var hash = new byte[32];
            var result = _pool.Contains(hash);
            Assert.IsFalse(result);
        }

        [TestMethod]
        public void TryGet_ExistingTransaction_ReturnsTransaction()
        {
            var tx = new TestTransactionData(1);
            _pool.TryAdd(tx);
            var result = _pool.TryGet(tx.Hash.GetSpan().ToArray(), out var retrieved);
            Assert.IsTrue(result);
            Assert.IsNotNull(retrieved);
            Assert.AreEqual(tx.Hash, retrieved.Hash);
        }

        [TestMethod]
        public void TryGet_NonExistingTransaction_ReturnsFalse()
        {
            var hash = new byte[32];
            var result = _pool.TryGet(hash, out var retrieved);
            Assert.IsFalse(result);
            Assert.IsNull(retrieved);
        }

        [TestMethod]
        public void TryRemove_ExistingTransaction_ReturnsTrue()
        {
            var tx = new TestTransactionData(1);
            _pool.TryAdd(tx);
            var result = _pool.TryRemove(tx.Hash.GetSpan().ToArray());
            Assert.IsTrue(result);
            Assert.AreEqual(0, _pool.Count);
        }

        [TestMethod]
        public void TryRemove_NonExistingTransaction_ReturnsFalse()
        {
            var hash = new byte[32];
            var result = _pool.TryRemove(hash);
            Assert.IsFalse(result);
        }

        [TestMethod]
        public void GetVerifiedTransactions_ReturnsAllVerified()
        {
            for (int i = 0; i < 10; i++)
            {
                _pool.TryAdd(new TestTransactionData(i));
            }

            var verified = _pool.GetVerifiedTransactions().ToList();
            Assert.AreEqual(10, verified.Count);
        }

        [TestMethod]
        public void GetVerifiedTransactions_WithMaxCount_ReturnsLimitedCount()
        {
            for (int i = 0; i < 10; i++)
            {
                _pool.TryAdd(new TestTransactionData(i));
            }

            var verified = _pool.GetVerifiedTransactions(5).ToList();
            Assert.AreEqual(5, verified.Count);
        }

        [TestMethod]
        public void GetVerifiedTransactions_OrderedByFee()
        {
            // Add transactions with different fees
            _pool.TryAdd(new TestTransactionData(1, networkFee: 100));
            _pool.TryAdd(new TestTransactionData(2, networkFee: 300));
            _pool.TryAdd(new TestTransactionData(3, networkFee: 200));

            var verified = _pool.GetVerifiedTransactions().ToList();

            // Should be ordered by fee descending
            Assert.AreEqual(300, verified[0].NetworkFee);
            Assert.AreEqual(200, verified[1].NetworkFee);
            Assert.AreEqual(100, verified[2].NetworkFee);
        }

        [TestMethod]
        public void Clear_RemovesAllTransactions()
        {
            for (int i = 0; i < 10; i++)
            {
                _pool.TryAdd(new TestTransactionData(i));
            }

            _pool.Clear();
            Assert.AreEqual(0, _pool.Count);
        }

        [TestMethod]
        public void Capacity_EnforcesLimit()
        {
            var smallPool = new InMemoryTransactionPool(capacity: 5);

            for (int i = 0; i < 10; i++)
            {
                smallPool.TryAdd(new TestTransactionData(i, networkFee: i * 100));
            }

            // Should only have 5 transactions (highest fees kept)
            Assert.AreEqual(5, smallPool.Count);
        }

        [TestMethod]
        public void TransactionAdded_EventFired()
        {
            var eventFired = false;
            _pool.TransactionAdded += (sender, tx) => eventFired = true;

            _pool.TryAdd(new TestTransactionData(1));
            Assert.IsTrue(eventFired);
        }

        [TestMethod]
        public void TransactionRemoved_EventFired()
        {
            var eventFired = false;
            TransactionRemovalReason? reason = null;
            _pool.TransactionRemoved += (sender, args) =>
            {
                eventFired = true;
                reason = args.Reason;
            };

            var tx = new TestTransactionData(1);
            _pool.TryAdd(tx);
            _pool.TryRemove(tx.Hash.GetSpan().ToArray());

            Assert.IsTrue(eventFired);
            Assert.AreEqual(TransactionRemovalReason.Explicit, reason);
        }

        [TestMethod]
        public void VerifiedCount_ReturnsCorrectCount()
        {
            for (int i = 0; i < 5; i++)
            {
                _pool.TryAdd(new TestTransactionData(i));
            }

            Assert.AreEqual(5, _pool.VerifiedCount);
        }
    }

    /// <summary>
    /// Test implementation of ITransactionData.
    /// </summary>
    internal class TestTransactionData : ITransactionData
    {
        private readonly byte[] _hashBytes;
        private static readonly byte[] EmptyScript = [];

        public TestTransactionData(int seed, long networkFee = 100000)
        {
            _hashBytes = new byte[32];
            BitConverter.GetBytes(seed).CopyTo(_hashBytes, 0);
            BitConverter.GetBytes(DateTime.UtcNow.Ticks).CopyTo(_hashBytes, 4);
            Hash = new UInt256(_hashBytes);
            Sender = UInt160.Zero;
            NetworkFee = networkFee;
        }

        public UInt256 Hash { get; }
        public byte Version => 0;
        public uint Nonce => 0;
        public long SystemFee => 1000000;
        public long NetworkFee { get; }
        public uint ValidUntilBlock => 1000;
        public ReadOnlyMemory<byte> Script => EmptyScript;
        public UInt160 Sender { get; }
        public long FeePerByte => NetworkFee / Size;
        public int SignersCount => 1;
        public int AttributesCount => 0;
        public int Size => 250;

        public void Deserialize(ref MemoryReader reader) { }
        public void DeserializeUnsigned(ref MemoryReader reader) { }
        public void Serialize(BinaryWriter writer) { }
        public void SerializeUnsigned(BinaryWriter writer) { }
    }
}
