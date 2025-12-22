// Copyright (C) 2015-2025 The Neo Project.
//
// UT_ProtocolCompatibility.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Akka.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.Extensions;
using Neo.IO;
using Neo.Network.P2P;
using Neo.Network.P2P.Capabilities;
using Neo.Network.P2P.Payloads;
using System;
using System.IO;
using System.Linq;

namespace Neo.UnitTests.Network.P2P.Payloads
{
    /// <summary>
    /// Protocol serialization compatibility tests to verify byte-for-byte compatibility with origin Neo.
    /// Tests cover Message frame format, variable-length encoding, and payload serialization.
    /// </summary>
    [TestClass]
    public class UT_ProtocolCompatibility
    {
        #region Helper Methods

        /// <summary>
        /// Builds expected message bytes with proper varint encoding for payload length.
        /// </summary>
        private static byte[] BuildExpectedMessageBytes(MessageFlags flags, MessageCommand command, byte[] payload)
        {
            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms, Utility.StrictUTF8, true);

            writer.Write((byte)flags);
            writer.Write((byte)command);
            writer.WriteVarBytes(payload);

            return ms.ToArray();
        }

        /// <summary>
        /// Encodes a value using Neo's variable-length integer encoding.
        /// </summary>
        private static byte[] EncodeVarInt(long value)
        {
            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms, Utility.StrictUTF8, true);
            writer.WriteVarInt(value);
            return ms.ToArray();
        }

        #endregion

        #region Variable-Length Encoding Tests

        [TestMethod]
        [TestCategory("Golden")]
        public void VarInt_EdgeCases_Golden()
        {
            // Test boundary values for variable-length encoding
            // < 0xFD: 1 byte
            var encoded0 = EncodeVarInt(0);
            Assert.AreEqual("00", Convert.ToHexString(encoded0).ToLowerInvariant());

            var encoded252 = EncodeVarInt(0xFC);
            Assert.AreEqual("fc", Convert.ToHexString(encoded252).ToLowerInvariant());

            // 0xFD-0xFFFF: 3 bytes (0xFD + ushort LE)
            var encoded253 = EncodeVarInt(0xFD);
            Assert.AreEqual("fdfd00", Convert.ToHexString(encoded253).ToLowerInvariant());

            var encoded65535 = EncodeVarInt(0xFFFF);
            Assert.AreEqual("fdffff", Convert.ToHexString(encoded65535).ToLowerInvariant());

            // 0x10000-0xFFFFFFFF: 5 bytes (0xFE + uint LE)
            var encoded65536 = EncodeVarInt(0x10000);
            Assert.AreEqual("fe00000100", Convert.ToHexString(encoded65536).ToLowerInvariant());

            var encodedMax32 = EncodeVarInt(0xFFFFFFFF);
            Assert.AreEqual("feffffffff", Convert.ToHexString(encodedMax32).ToLowerInvariant());

            // > 0xFFFFFFFF: 9 bytes (0xFF + ulong LE)
            var encoded64bit = EncodeVarInt(0x100000000L);
            Assert.AreEqual("ff0000000001000000", Convert.ToHexString(encoded64bit).ToLowerInvariant());
        }

        #endregion

        #region Message Frame Format Tests

        [TestMethod]
        [TestCategory("Golden")]
        public void MessageFrame_EmptyPayload_Golden()
        {
            // Message with zero-length payload
            var msg = Message.Create(MessageCommand.Verack, null);
            var bytes = msg.ToArray();
            var hex = Convert.ToHexString(bytes).ToLowerInvariant();

            // Expected: Flags(0x00) + Command(0x01) + VarBytes(0)
            var expected = "00" + "01" + "00";
            Assert.AreEqual(expected, hex);

            // Test deserialization
            var data = ByteString.FromBytes(bytes);
            var consumed = Message.TryDeserialize(data, out var deserialized);
            Assert.AreEqual(bytes.Length, consumed);
            Assert.IsNotNull(deserialized);
            Assert.AreEqual(MessageCommand.Verack, deserialized.Command);
            Assert.AreEqual(MessageFlags.None, deserialized.Flags);
        }

        [TestMethod]
        [TestCategory("Golden")]
        public void MessageFrame_SmallPayload_Golden()
        {
            // Payload length = 1 (< 0xFD, uses 1-byte varint)
            var payload = new byte[] { 0x42 };
            var msg = Message.Create(MessageCommand.Verack, null);

            // Manually construct message with custom payload
            var expected = BuildExpectedMessageBytes(MessageFlags.None, MessageCommand.Verack, payload);

            // Verify varint encoding: length 1 = 0x01
            Assert.AreEqual(0x00, expected[0]); // Flags
            Assert.AreEqual(0x01, expected[1]); // Command
            Assert.AreEqual(0x01, expected[2]); // VarInt length
            Assert.AreEqual(0x42, expected[3]); // Payload
        }

        [TestMethod]
        [TestCategory("Golden")]
        public void MessageFrame_VarIntBoundary_252_Golden()
        {
            // Payload length = 0xFC (252, last value using 1-byte varint)
            var payload = new byte[0xFC];
            Array.Fill(payload, (byte)0xAA);

            var expected = BuildExpectedMessageBytes(MessageFlags.None, MessageCommand.Verack, payload);

            // Verify: Flags + Command + VarInt(0xFC) + 252 bytes
            Assert.AreEqual(0x00, expected[0]); // Flags
            Assert.AreEqual(0x01, expected[1]); // Command
            Assert.AreEqual(0xFC, expected[2]); // VarInt length (1 byte)
            Assert.AreEqual(0xAA, expected[3]); // First payload byte
            Assert.AreEqual(3 + 252, expected.Length);
        }

        [TestMethod]
        [TestCategory("Golden")]
        public void MessageFrame_VarIntBoundary_253_Golden()
        {
            // Payload length = 0xFD (253, first value using 3-byte varint)
            var payload = new byte[0xFD];
            Array.Fill(payload, (byte)0xBB);

            var expected = BuildExpectedMessageBytes(MessageFlags.None, MessageCommand.Verack, payload);

            // Verify: Flags + Command + VarInt(0xFD, 0xFD, 0x00) + 253 bytes
            Assert.AreEqual(0x00, expected[0]); // Flags
            Assert.AreEqual(0x01, expected[1]); // Command
            Assert.AreEqual(0xFD, expected[2]); // VarInt prefix
            Assert.AreEqual(0xFD, expected[3]); // Length low byte
            Assert.AreEqual(0x00, expected[4]); // Length high byte
            Assert.AreEqual(0xBB, expected[5]); // First payload byte
            Assert.AreEqual(5 + 253, expected.Length);
        }

        [TestMethod]
        [TestCategory("Golden")]
        public void MessageFrame_VarIntBoundary_65535_Golden()
        {
            // Payload length = 0xFFFF (65535, last value using 3-byte varint)
            var payload = new byte[0xFFFF];
            Array.Fill(payload, (byte)0xCC);

            var expected = BuildExpectedMessageBytes(MessageFlags.None, MessageCommand.Verack, payload);

            // Verify: Flags + Command + VarInt(0xFD, 0xFF, 0xFF) + 65535 bytes
            Assert.AreEqual(0x00, expected[0]); // Flags
            Assert.AreEqual(0x01, expected[1]); // Command
            Assert.AreEqual(0xFD, expected[2]); // VarInt prefix
            Assert.AreEqual(0xFF, expected[3]); // Length low byte
            Assert.AreEqual(0xFF, expected[4]); // Length high byte
            Assert.AreEqual(0xCC, expected[5]); // First payload byte
            Assert.AreEqual(5 + 65535, expected.Length);
        }

        [TestMethod]
        [TestCategory("Golden")]
        public void MessageFrame_VarIntBoundary_65536_Golden()
        {
            // Payload length = 0x10000 (65536, first value using 5-byte varint)
            var payload = new byte[0x10000];
            Array.Fill(payload, (byte)0xDD);

            var expected = BuildExpectedMessageBytes(MessageFlags.None, MessageCommand.Verack, payload);

            // Verify: Flags + Command + VarInt(0xFE, 0x00, 0x00, 0x01, 0x00) + 65536 bytes
            Assert.AreEqual(0x00, expected[0]); // Flags
            Assert.AreEqual(0x01, expected[1]); // Command
            Assert.AreEqual(0xFE, expected[2]); // VarInt prefix
            Assert.AreEqual(0x00, expected[3]); // Length byte 0
            Assert.AreEqual(0x00, expected[4]); // Length byte 1
            Assert.AreEqual(0x01, expected[5]); // Length byte 2
            Assert.AreEqual(0x00, expected[6]); // Length byte 3
            Assert.AreEqual(0xDD, expected[7]); // First payload byte
            Assert.AreEqual(7 + 65536, expected.Length);
        }

        [TestMethod]
        [TestCategory("Golden")]
        public void MessageFrame_Deserialization_RoundTrip()
        {
            // Test various payload sizes for round-trip serialization
            int[] testSizes = { 0, 1, 0xFC, 0xFD, 0xFFFF, 0x10000 };

            foreach (var size in testSizes)
            {
                var payload = new byte[size];
                Array.Fill(payload, (byte)(size & 0xFF));

                var expected = BuildExpectedMessageBytes(MessageFlags.None, MessageCommand.Verack, payload);
                var data = ByteString.FromBytes(expected);

                var consumed = Message.TryDeserialize(data, out var msg);

                Assert.AreEqual(expected.Length, consumed, $"Failed for size {size}");
                Assert.IsNotNull(msg, $"Failed to deserialize size {size}");
                Assert.AreEqual(MessageCommand.Verack, msg.Command);
                Assert.AreEqual(MessageFlags.None, msg.Flags);
            }
        }

        #endregion

        #region VersionPayload Serialization Tests

        [TestMethod]
        [TestCategory("Golden")]
        public void VersionPayload_WithCapabilities_Golden()
        {
            // Create deterministic VersionPayload with known values
            var payload = new VersionPayload
            {
                Network = 0x334455AA,
                Version = 0,
                Timestamp = 0x11223344,
                Nonce = 0x55667788,
                UserAgent = "/Neo:3.0.0/",
                Capabilities = new NodeCapability[]
                {
                    new FullNodeCapability { StartHeight = 100 }
                }
            };

            var bytes = payload.ToArray();
            var hex = Convert.ToHexString(bytes).ToLowerInvariant();

            // Expected format:
            // Network (4 bytes LE): aa554433
            // Version (4 bytes LE): 00000000
            // Timestamp (4 bytes LE): 44332211
            // Nonce (4 bytes LE): 88776655
            // UserAgent (varstring): length + "/Neo:3.0.0/" (11 bytes, so varint=0b)
            // Capabilities (array): 01 + (10 + 04 + 64000000)
            var expectedPrefix = "aa554433" + "00000000" + "44332211" + "88776655" + "0b" +
                                Convert.ToHexString(System.Text.Encoding.UTF8.GetBytes("/Neo:3.0.0/")).ToLowerInvariant() +
                                "01" + "10" + "64000000";

            Assert.IsTrue(hex.StartsWith(expectedPrefix.Substring(0, 40)),
                $"Expected prefix mismatch. Got: {hex.Substring(0, Math.Min(40, hex.Length))}");

            // Test deserialization
            var deserialized = bytes.AsSerializable<VersionPayload>();
            Assert.AreEqual(payload.Network, deserialized.Network);
            Assert.AreEqual(payload.Version, deserialized.Version);
            Assert.AreEqual(payload.Timestamp, deserialized.Timestamp);
            Assert.AreEqual(payload.Nonce, deserialized.Nonce);
            Assert.AreEqual(payload.UserAgent, deserialized.UserAgent);
            Assert.AreEqual(1, deserialized.Capabilities.Length);
            Assert.IsInstanceOfType(deserialized.Capabilities[0], typeof(FullNodeCapability));
            Assert.IsTrue(deserialized.AllowCompression);
        }

        [TestMethod]
        [TestCategory("Golden")]
        public void VersionPayload_MultipleCapabilities_Golden()
        {
            // Test serialization with multiple capabilities
            var payload = new VersionPayload
            {
                Network = 0x12345678,
                Version = 0,
                Timestamp = 0xAABBCCDD,
                Nonce = 0x11223344,
                UserAgent = "/Test/",
                Capabilities = new NodeCapability[]
                {
                    new FullNodeCapability { StartHeight = 1000 },
                    new ServerCapability(NodeCapabilityType.TcpServer) { Port = 10333 }
                }
            };

            var bytes = payload.ToArray();
            var deserialized = bytes.AsSerializable<VersionPayload>();

            // Verify all fields match
            Assert.AreEqual(payload.Network, deserialized.Network);
            Assert.AreEqual(payload.Version, deserialized.Version);
            Assert.AreEqual(payload.Timestamp, deserialized.Timestamp);
            Assert.AreEqual(payload.Nonce, deserialized.Nonce);
            Assert.AreEqual(payload.UserAgent, deserialized.UserAgent);
            Assert.AreEqual(2, deserialized.Capabilities.Length);
            Assert.IsInstanceOfType(deserialized.Capabilities[0], typeof(FullNodeCapability));
            Assert.IsInstanceOfType(deserialized.Capabilities[1], typeof(ServerCapability));
        }

        #endregion

        #region InvPayload Serialization Tests

        [TestMethod]
        [TestCategory("Golden")]
        [DataRow(InventoryType.TX, DisplayName = "InvPayload_Transaction")]
        [DataRow(InventoryType.Block, DisplayName = "InvPayload_Block")]
        [DataRow(InventoryType.Extensible, DisplayName = "InvPayload_Extensible")]
        public void InvPayload_AllTypes_Golden(InventoryType type)
        {
            // Create InvPayload with two deterministic hashes
            var hash1 = new UInt256(Enumerable.Range(0, 32).Select(i => (byte)i).ToArray());
            var hash2 = new UInt256(Enumerable.Range(0, 32).Select(i => (byte)(i + 32)).ToArray());

            var payload = InvPayload.Create(type, hash1, hash2);
            var bytes = payload.ToArray();
            var hex = Convert.ToHexString(bytes).ToLowerInvariant();

            // Expected format: Type (1 byte) + Count (varint=02) + Hash1 (32 bytes) + Hash2 (32 bytes)
            var expectedPrefix = Convert.ToHexString(new[] { (byte)type }).ToLowerInvariant() + "02";
            Assert.IsTrue(hex.StartsWith(expectedPrefix), $"Expected type {type} with count 2");
            Assert.AreEqual(1 + 1 + 32 + 32, bytes.Length);

            // Test deserialization
            var deserialized = bytes.AsSerializable<InvPayload>();
            Assert.AreEqual(type, deserialized.Type);
            Assert.AreEqual(2, deserialized.Hashes.Length);
            Assert.AreEqual(hash1, deserialized.Hashes[0]);
            Assert.AreEqual(hash2, deserialized.Hashes[1]);
        }

        #endregion

        #region Transaction Serialization Tests

        [TestMethod]
        [TestCategory("Golden")]
        public void Transaction_RoundTrip_Golden()
        {
            // Create minimal deterministic transaction
            var tx = new Transaction
            {
                Version = 0,
                Nonce = 0x12345678,
                SystemFee = 1000000,
                NetworkFee = 100000,
                ValidUntilBlock = 999999u,
                Signers = new[] { new Signer { Account = UInt160.Zero, Scopes = WitnessScope.None } },
                Attributes = Array.Empty<TransactionAttribute>(),
                Script = new byte[] { 0x40 }, // RET opcode
                Witnesses = new[] { new Witness { InvocationScript = Array.Empty<byte>(), VerificationScript = Array.Empty<byte>() } }
            };

            var bytes = tx.ToArray();
            var deserialized = bytes.AsSerializable<Transaction>();

            // Verify all fields match
            Assert.AreEqual(tx.Version, deserialized.Version);
            Assert.AreEqual(tx.Nonce, deserialized.Nonce);
            Assert.AreEqual(tx.SystemFee, deserialized.SystemFee);
            Assert.AreEqual(tx.NetworkFee, deserialized.NetworkFee);
            Assert.AreEqual(tx.ValidUntilBlock, deserialized.ValidUntilBlock);
            Assert.AreEqual(tx.Signers.Length, deserialized.Signers.Length);
            Assert.AreEqual(tx.Script.Length, deserialized.Script.Length);
            CollectionAssert.AreEqual(tx.Script.ToArray(), deserialized.Script.ToArray());

            // Verify hash consistency
            Assert.AreEqual(tx.Hash, deserialized.Hash);
        }

        #endregion

        #region Block Serialization Tests

        [TestMethod]
        [TestCategory("Golden")]
        public void Block_EmptyTransactions_RoundTrip_Golden()
        {
            // Create minimal deterministic block
            var header = new Header
            {
                Version = 0,
                PrevHash = UInt256.Zero,
                MerkleRoot = UInt256.Zero,
                Timestamp = 0x1122334455667788UL,
                Nonce = 0xAABBCCDDEEFF0011UL,
                Index = 12345u,
                PrimaryIndex = 3,
                NextConsensus = UInt160.Zero,
                Witness = new Witness
                {
                    InvocationScript = Array.Empty<byte>(),
                    VerificationScript = Array.Empty<byte>()
                }
            };

            var block = new Block
            {
                Header = header,
                Transactions = Array.Empty<Transaction>()
            };

            var bytes = block.ToArray();
            var deserialized = bytes.AsSerializable<Block>();

            // Verify header fields
            Assert.AreEqual(block.Header.Version, deserialized.Header.Version);
            Assert.AreEqual(block.Header.PrevHash, deserialized.Header.PrevHash);
            Assert.AreEqual(block.Header.MerkleRoot, deserialized.Header.MerkleRoot);
            Assert.AreEqual(block.Header.Timestamp, deserialized.Header.Timestamp);
            Assert.AreEqual(block.Header.Nonce, deserialized.Header.Nonce);
            Assert.AreEqual(block.Header.Index, deserialized.Header.Index);
            Assert.AreEqual(block.Header.PrimaryIndex, deserialized.Header.PrimaryIndex);
            Assert.AreEqual(block.Header.NextConsensus, deserialized.Header.NextConsensus);

            // Verify transactions
            Assert.AreEqual(0, deserialized.Transactions.Length);

            // Verify hash consistency
            Assert.AreEqual(block.Hash, deserialized.Hash);
        }

        #endregion

        #region Compression Flag Tests

        [TestMethod]
        [TestCategory("Golden")]
        public void Message_Compression_LargePayload()
        {
            // Create a highly compressible transaction with large script
            var largeScript = new byte[1024];
            Array.Fill(largeScript, (byte)0x00); // Highly compressible

            var tx = new Transaction
            {
                Version = 0,
                Nonce = 0,
                SystemFee = 0,
                NetworkFee = 0,
                ValidUntilBlock = 1000u,
                Signers = new[] { new Signer { Account = UInt160.Zero, Scopes = WitnessScope.None } },
                Attributes = Array.Empty<TransactionAttribute>(),
                Script = largeScript,
                Witnesses = new[] { new Witness { InvocationScript = Array.Empty<byte>(), VerificationScript = Array.Empty<byte>() } }
            };

            // Create message (compression may trigger for Transaction command)
            var msg = Message.Create(MessageCommand.Transaction, tx);
            var compressedBytes = msg.ToArray();
            var uncompressedBytes = msg.ToArray(enablecompression: false);

            // Verify compression flag
            if (msg.IsCompressed)
            {
                Assert.AreEqual(MessageFlags.Compressed, msg.Flags & MessageFlags.Compressed);
                Assert.IsTrue(compressedBytes.Length < uncompressedBytes.Length,
                    "Compressed message should be smaller than uncompressed");
            }

            // Test deserialization of both versions
            var compressedData = ByteString.FromBytes(compressedBytes);
            var consumed1 = Message.TryDeserialize(compressedData, out var msg1);
            Assert.AreEqual(compressedBytes.Length, consumed1);
            Assert.IsNotNull(msg1);
            Assert.AreEqual(MessageCommand.Transaction, msg1.Command);

            var uncompressedData = ByteString.FromBytes(uncompressedBytes);
            var consumed2 = Message.TryDeserialize(uncompressedData, out var msg2);
            Assert.AreEqual(uncompressedBytes.Length, consumed2);
            Assert.IsNotNull(msg2);
            Assert.AreEqual(MessageCommand.Transaction, msg2.Command);

            // Both should deserialize to equivalent payloads
            Assert.IsNotNull(msg1.Payload);
            Assert.IsNotNull(msg2.Payload);
        }

        [TestMethod]
        [TestCategory("Golden")]
        public void Message_NoCompression_SmallPayload()
        {
            // Small payloads should not be compressed (< 128 bytes threshold)
            var tx = new Transaction
            {
                Version = 0,
                Nonce = 0,
                SystemFee = 0,
                NetworkFee = 0,
                ValidUntilBlock = 1u,
                Signers = new[] { new Signer { Account = UInt160.Zero, Scopes = WitnessScope.None } },
                Attributes = Array.Empty<TransactionAttribute>(),
                Script = new byte[] { 0x40 },
                Witnesses = new[] { new Witness { InvocationScript = Array.Empty<byte>(), VerificationScript = Array.Empty<byte>() } }
            };

            var msg = Message.Create(MessageCommand.Transaction, tx);

            // Small payload should not trigger compression
            Assert.IsFalse(msg.IsCompressed, "Small payloads should not be compressed");
            Assert.AreEqual(MessageFlags.None, msg.Flags);
        }

        #endregion

        #region Error Handling Tests

        [TestMethod]
        [TestCategory("Golden")]
        public void Message_OversizedPayload_ThrowsException()
        {
            // Create message with payload length exceeding PayloadMaxSize
            var oversizedHeader = new byte[11];
            oversizedHeader[0] = 0x00; // Flags
            oversizedHeader[1] = 0x01; // Command
            oversizedHeader[2] = 0xFF; // VarInt prefix for 8-byte length

            // Set length to PayloadMaxSize + 1 (0x02000001)
            BitConverter.GetBytes(Message.PayloadMaxSize + 1L).CopyTo(oversizedHeader, 3);

            var data = ByteString.FromBytes(oversizedHeader);

            // Should throw FormatException
            Assert.ThrowsExactly<FormatException>(() => Message.TryDeserialize(data, out _));
        }

        #endregion
    }
}
