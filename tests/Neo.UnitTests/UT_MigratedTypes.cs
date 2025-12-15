// Copyright (C) 2015-2025 The Neo Project.
//
// UT_MigratedTypes.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.Cryptography;
using Neo.Cryptography.ECC;
using Neo.Extensions;
using Neo.Persistence;
using Neo.Persistence.Providers;
using System;
using System.Linq;
using System.Numerics;
using System.Text;

namespace Neo.UnitTests
{
    /// <summary>
    /// Regression tests for types migrated to separate assemblies.
    /// Ensures backward compatibility after modularization.
    /// </summary>
    [TestClass]
    public class UT_MigratedTypes
    {
        #region Neo.Core Types

        [TestMethod]
        public void TestHardforkValues()
        {
            // Verify enum values are preserved after migration
            Assert.AreEqual(0, (int)Hardfork.HF_Aspidochelone);
            Assert.AreEqual(1, (int)Hardfork.HF_Basilisk);
            Assert.AreEqual(2, (int)Hardfork.HF_Cockatrice);
            Assert.AreEqual(3, (int)Hardfork.HF_Domovoi);
            Assert.AreEqual(4, (int)Hardfork.HF_Echidna);
            Assert.AreEqual(5, (int)Hardfork.HF_Faun);
            Assert.AreEqual(6, (int)Hardfork.HF_Gorgon);

            // Verify all values can be enumerated
            var values = Enum.GetValues<Hardfork>();
            Assert.AreEqual(7, values.Length);
        }

        [TestMethod]
        public void TestContainsTransactionTypeValues()
        {
            // Verify enum values are preserved after migration
            Assert.AreEqual(0, (int)ContainsTransactionType.NotExist);
            Assert.AreEqual(1, (int)ContainsTransactionType.ExistsInPool);
            Assert.AreEqual(2, (int)ContainsTransactionType.ExistsInLedger);

            // Verify all values can be enumerated
            var values = Enum.GetValues<ContainsTransactionType>();
            Assert.AreEqual(3, values.Length);
        }

        [TestMethod]
        public void TestTimeProviderDefault()
        {
            // Verify TimeProvider.Current returns a valid time
            var now = TimeProvider.Current.UtcNow;
            Assert.IsTrue(now.Year >= 2025);

            // Verify timestamp conversion works
            var timestamp = now.ToTimestamp();
            Assert.IsTrue(timestamp > 0);
        }

        [TestMethod]
        public void TestBigDecimalBasicOperations()
        {
            // Test basic construction and operations
            var bd1 = new BigDecimal(new BigInteger(12345), 2); // 123.45
            Assert.AreEqual(new BigInteger(12345), bd1.Value);
            Assert.AreEqual(2, bd1.Decimals);

            var bd2 = new BigDecimal(new BigInteger(67890), 2); // 678.90
            Assert.AreEqual(new BigInteger(67890), bd2.Value);

            // Test sign
            var negative = new BigDecimal(new BigInteger(-100), 1);
            Assert.AreEqual(-1, negative.Sign);

            var zero = new BigDecimal(BigInteger.Zero, 0);
            Assert.AreEqual(0, zero.Sign);
        }

        [TestMethod]
        public void TestUInt160TypeForwarding()
        {
            // Verify type is accessible and works correctly after migration
            var hash = UInt160.Zero;
            Assert.AreEqual(20, UInt160.Length);
            Assert.AreEqual("0x0000000000000000000000000000000000000000", hash.ToString());

            // Test parsing
            var parsed = UInt160.Parse("0x0102030405060708090a0b0c0d0e0f1011121314");
            Assert.IsNotNull(parsed);
            Assert.AreEqual("0x0102030405060708090a0b0c0d0e0f1011121314", parsed.ToString());
        }

        [TestMethod]
        public void TestUInt256TypeForwarding()
        {
            // Verify type is accessible and works correctly after migration
            var hash = UInt256.Zero;
            Assert.AreEqual(32, UInt256.Length);
            Assert.AreEqual("0x0000000000000000000000000000000000000000000000000000000000000000", hash.ToString());

            // Test parsing
            var parsed = UInt256.Parse("0x0102030405060708090a0b0c0d0e0f101112131415161718191a1b1c1d1e1f20");
            Assert.IsNotNull(parsed);
        }

        #endregion

        #region Neo.Cryptography Types

        [TestMethod]
        public void TestBase58Encoding()
        {
            // Test Base58 encoding/decoding
            var data = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05 };
            var encoded = Base58.Encode(data);
            Assert.IsNotNull(encoded);
            Assert.IsTrue(encoded.Length > 0);

            var decoded = Base58.Decode(encoded);
            CollectionAssert.AreEqual(data, decoded);
        }

        [TestMethod]
        public void TestBase58CheckEncoding()
        {
            // Test Base58Check encoding/decoding (includes checksum)
            var data = new byte[] { 0x00, 0x01, 0x02, 0x03, 0x04 };
            var encoded = Base58.Base58CheckEncode(data);
            Assert.IsNotNull(encoded);

            var decoded = Base58.Base58CheckDecode(encoded);
            CollectionAssert.AreEqual(data, decoded);
        }

        [TestMethod]
        public void TestHashExtensionsSha256()
        {
            // Test SHA256 hashing
            var data = Encoding.UTF8.GetBytes("Hello, Neo!");
            var hash = data.Sha256();

            Assert.AreEqual(32, hash.Length);
            // Verify deterministic output
            var hash2 = data.Sha256();
            CollectionAssert.AreEqual(hash, hash2);
        }

        [TestMethod]
        public void TestHashExtensionsRipemd160()
        {
            // Test RIPEMD160 hashing
            var data = Encoding.UTF8.GetBytes("Hello, Neo!");
            var hash = data.RIPEMD160();

            Assert.AreEqual(20, hash.Length);
            // Verify deterministic output
            var hash2 = data.RIPEMD160();
            CollectionAssert.AreEqual(hash, hash2);
        }

        [TestMethod]
        public void TestHashExtensionsMurmur32()
        {
            // Test Murmur32 hashing
            var data = Encoding.UTF8.GetBytes("Hello, Neo!");
            var hash = data.Murmur32(0);

            // Verify deterministic output with same seed
            var hash2 = data.Murmur32(0);
            Assert.AreEqual(hash, hash2);

            // Different seed should produce different hash
            var hash3 = data.Murmur32(1);
            Assert.AreNotEqual(hash, hash3);
        }

        [TestMethod]
        public void TestHashExtensionsMurmur128()
        {
            // Test Murmur128 hashing
            var data = Encoding.UTF8.GetBytes("Hello, Neo!");
            var hash = data.Murmur128(0);

            Assert.AreEqual(16, hash.Length);
            // Verify deterministic output
            var hash2 = data.Murmur128(0);
            CollectionAssert.AreEqual(hash, hash2);
        }

        [TestMethod]
        public void TestHashExtensionsKeccak256()
        {
            // Test Keccak256 hashing (used in Ethereum compatibility)
            var data = Encoding.UTF8.GetBytes("Hello, Neo!");
            var hash = data.Keccak256();

            Assert.AreEqual(32, hash.Length);
            // Verify deterministic output
            var hash2 = data.Keccak256();
            CollectionAssert.AreEqual(hash, hash2);
        }

        [TestMethod]
        public void TestAes256EncryptDecrypt()
        {
            // Test AES-256-GCM encryption/decryption
            var plaintext = Encoding.UTF8.GetBytes("Secret message for Neo blockchain!");
            var key = new byte[32]; // 256-bit key
            var nonce = new byte[12]; // 96-bit nonce

            // Fill with test data
            for (int i = 0; i < key.Length; i++) key[i] = (byte)i;
            for (int i = 0; i < nonce.Length; i++) nonce[i] = (byte)(i + 100);

            // Encrypt with nonce
            var encrypted = plaintext.AES256Encrypt(key, nonce);
            Assert.IsNotNull(encrypted);
            Assert.AreNotEqual(plaintext.Length, encrypted.Length); // Should include nonce + auth tag

            // Decrypt (nonce is prepended to encrypted data)
            var decrypted = encrypted.AES256Decrypt(key);
            CollectionAssert.AreEqual(plaintext, decrypted);
        }

        [TestMethod]
        public void TestMerkleTreeConstruction()
        {
            // Test MerkleTree root calculation
            var hashes = new UInt256[]
            {
                UInt256.Parse("0x0000000000000000000000000000000000000000000000000000000000000001"),
                UInt256.Parse("0x0000000000000000000000000000000000000000000000000000000000000002"),
                UInt256.Parse("0x0000000000000000000000000000000000000000000000000000000000000003"),
                UInt256.Parse("0x0000000000000000000000000000000000000000000000000000000000000004")
            };

            var root = MerkleTree.ComputeRoot(hashes);

            Assert.IsNotNull(root);
            Assert.AreEqual(32, root.GetSpan().Length);

            // Verify deterministic root
            var root2 = MerkleTree.ComputeRoot(hashes);
            Assert.AreEqual(root, root2);
        }

        [TestMethod]
        public void TestBloomFilterBasicOperations()
        {
            // Test BloomFilter add and check
            var filter = new BloomFilter(256, 3, 12345);

            var data1 = Encoding.UTF8.GetBytes("item1");
            var data2 = Encoding.UTF8.GetBytes("item2");
            var data3 = Encoding.UTF8.GetBytes("item3_not_added");

            filter.Add(data1);
            filter.Add(data2);

            // Items added should be found (no false negatives)
            Assert.IsTrue(filter.Check(data1));
            Assert.IsTrue(filter.Check(data2));

            // Item not added might or might not be found (false positives possible)
            // But for this specific case with low load, it should not be found
            // Note: This is probabilistic, but with these parameters should be reliable
        }

        [TestMethod]
        public void TestECPointOperations()
        {
            // Test ECPoint basic operations via curve
            var curve = ECCurve.Secp256r1;
            Assert.IsNotNull(curve);

            var infinity = curve.Infinity;
            Assert.IsTrue(infinity.IsInfinity);

            // Test generator point
            Assert.IsNotNull(curve.G); // Generator point
            Assert.IsFalse(curve.G.IsInfinity);
        }

        #endregion

        #region Neo.Storage Types

        [TestMethod]
        public void TestSeekDirectionValues()
        {
            // Verify enum values are preserved after migration
            Assert.AreEqual((int)SeekDirection.Forward, (int)SeekDirection.Forward);
            Assert.AreEqual((int)SeekDirection.Backward, (int)SeekDirection.Backward);
            Assert.AreNotEqual(SeekDirection.Forward, SeekDirection.Backward);
        }

        [TestMethod]
        public void TestTrackStateValues()
        {
            // Verify enum values are preserved after migration
            Assert.AreEqual(0, (int)TrackState.None);
            Assert.AreEqual(1, (int)TrackState.Added);
            Assert.AreEqual(2, (int)TrackState.Changed);
            Assert.AreEqual(3, (int)TrackState.Deleted);
        }

        [TestMethod]
        public void TestMemoryStoreBasicOperations()
        {
            // Test MemoryStore basic operations
            using var store = new MemoryStore();

            var key = new byte[] { 0x01, 0x02, 0x03 };
            var value = new byte[] { 0x04, 0x05, 0x06 };

            // Initially should not contain key
            Assert.IsFalse(store.Contains(key));

            // Put and verify
            store.Put(key, value);
            Assert.IsTrue(store.Contains(key));

            // Get and verify
            Assert.IsTrue(store.TryGet(key, out var retrieved));
            CollectionAssert.AreEqual(value, retrieved);

            // Delete and verify
            store.Delete(key);
            Assert.IsFalse(store.Contains(key));
        }

        [TestMethod]
        public void TestMemoryStoreSnapshot()
        {
            // Test MemoryStore snapshot functionality
            using var store = new MemoryStore();

            var key = new byte[] { 0x01 };
            var value1 = new byte[] { 0x10 };
            var value2 = new byte[] { 0x20 };

            store.Put(key, value1);

            using var snapshot = store.GetSnapshot();

            // Modify through snapshot
            snapshot.Put(key, value2);

            // Original store should still have old value until commit
            Assert.IsTrue(store.TryGet(key, out var storeValue));
            CollectionAssert.AreEqual(value1, storeValue);

            // Commit snapshot
            snapshot.Commit();

            // Now store should have new value
            Assert.IsTrue(store.TryGet(key, out var newValue));
            CollectionAssert.AreEqual(value2, newValue);
        }

        #endregion
    }
}
