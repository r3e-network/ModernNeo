// Copyright (C) 2015-2025 The Neo Project.
//
// PayloadFormatterTests.cs file belongs to the neo project and is free
// software distributed under the MIT software license.

using Neo.Cryptography;
using Neo.Cryptography.ECC;
using Neo.Network.P2P.Payloads;
using Neo.Serialization.MessagePack;
using Neo.SmartContract;
using Neo.Wallets;

namespace Neo.Serialization.MessagePack.Tests;

/// <summary>
/// Unit tests for Payload MessagePack formatters.
/// Tests Block, Header, Transaction, Witness, and Signer serialization.
/// </summary>
[TestClass]
public sealed class PayloadFormatterTests
{
    public TestContext TestContext { get; set; } = null!;

    #region Witness Tests

    [TestMethod]
    public void Witness_SerializeDeserialize_RoundTrip()
    {
        // Arrange
        var original = new Witness
        {
            InvocationScript = new byte[] { 0x01, 0x02, 0x03 },
            VerificationScript = new byte[] { 0x04, 0x05, 0x06 }
        };

        // Act
        var bytes = NeoMessagePackSerializer.Serialize(original);
        var deserialized = NeoMessagePackSerializer.Deserialize<Witness>(bytes);

        // Assert
        Assert.IsNotNull(deserialized);
        CollectionAssert.AreEqual(original.InvocationScript.ToArray(), deserialized.InvocationScript.ToArray());
        CollectionAssert.AreEqual(original.VerificationScript.ToArray(), deserialized.VerificationScript.ToArray());
    }

    [TestMethod]
    public void Witness_Nullable_Null_RoundTrip()
    {
        // Arrange
        Witness? original = null;

        // Act
        var bytes = NeoMessagePackSerializer.Serialize(original);
        var deserialized = NeoMessagePackSerializer.Deserialize<Witness?>(bytes);

        // Assert
        Assert.IsNull(deserialized);
    }

    [TestMethod]
    public void Witness_EmptyScripts_RoundTrip()
    {
        // Arrange
        var original = new Witness
        {
            InvocationScript = Array.Empty<byte>(),
            VerificationScript = Array.Empty<byte>()
        };

        // Act
        var bytes = NeoMessagePackSerializer.Serialize(original);
        var deserialized = NeoMessagePackSerializer.Deserialize<Witness>(bytes);

        // Assert
        Assert.IsNotNull(deserialized);
        Assert.AreEqual(0, deserialized.InvocationScript.Length);
        Assert.AreEqual(0, deserialized.VerificationScript.Length);
    }

    [TestMethod]
    public void Witness_Compressed_RoundTrip()
    {
        // Arrange
        var original = new Witness
        {
            InvocationScript = new byte[100], // Larger data benefits from compression
            VerificationScript = new byte[50]
        };

        // Act
        var bytes = NeoMessagePackSerializer.SerializeCompressed(original);
        var deserialized = NeoMessagePackSerializer.DeserializeCompressed<Witness>(bytes);

        // Assert
        Assert.IsNotNull(deserialized);
        Assert.AreEqual(original.InvocationScript.Length, deserialized.InvocationScript.Length);
        Assert.AreEqual(original.VerificationScript.Length, deserialized.VerificationScript.Length);
    }

    #endregion

    #region Signer Tests

    [TestMethod]
    public void Signer_SerializeDeserialize_RoundTrip()
    {
        // Arrange
        var original = new Signer
        {
            Account = UInt160.Parse("0x0000000000000000000000000000000000000001"),
            Scopes = WitnessScope.CalledByEntry
        };

        // Act
        var bytes = NeoMessagePackSerializer.Serialize(original);
        var deserialized = NeoMessagePackSerializer.Deserialize<Signer>(bytes);

        // Assert
        Assert.IsNotNull(deserialized);
        Assert.AreEqual(original.Account, deserialized.Account);
        Assert.AreEqual(original.Scopes, deserialized.Scopes);
    }

    [TestMethod]
    public void Signer_Nullable_Null_RoundTrip()
    {
        // Arrange
        Signer? original = null;

        // Act
        var bytes = NeoMessagePackSerializer.Serialize(original);
        var deserialized = NeoMessagePackSerializer.Deserialize<Signer?>(bytes);

        // Assert
        Assert.IsNull(deserialized);
    }

    [TestMethod]
    public void Signer_GlobalScope_RoundTrip()
    {
        // Arrange
        var original = new Signer
        {
            Account = UInt160.Parse("0xabcdef0123456789abcdef0123456789abcdef01"),
            Scopes = WitnessScope.Global
        };

        // Act
        var bytes = NeoMessagePackSerializer.Serialize(original);
        var deserialized = NeoMessagePackSerializer.Deserialize<Signer>(bytes);

        // Assert
        Assert.IsNotNull(deserialized);
        Assert.AreEqual(original.Account, deserialized.Account);
        Assert.AreEqual(WitnessScope.Global, deserialized.Scopes);
    }

    [TestMethod]
    public void Signer_WithAllowedContracts_RoundTrip()
    {
        // Arrange
        var original = new Signer
        {
            Account = UInt160.Parse("0x0000000000000000000000000000000000000001"),
            Scopes = WitnessScope.CustomContracts,
            AllowedContracts = new[]
            {
                UInt160.Parse("0x0000000000000000000000000000000000000002"),
                UInt160.Parse("0x0000000000000000000000000000000000000003")
            }
        };

        // Act
        var bytes = NeoMessagePackSerializer.Serialize(original);
        var deserialized = NeoMessagePackSerializer.Deserialize<Signer>(bytes);

        // Assert
        Assert.IsNotNull(deserialized);
        Assert.AreEqual(original.Account, deserialized.Account);
        Assert.AreEqual(WitnessScope.CustomContracts, deserialized.Scopes);
        Assert.AreEqual(2, deserialized.AllowedContracts?.Length);
    }

    #endregion

    #region Header Tests

    [TestMethod]
    public void Header_SerializeDeserialize_RoundTrip()
    {
        // Arrange
        var original = CreateTestHeader(index: 100, timestamp: 1700000000000);

        // Act
        var bytes = NeoMessagePackSerializer.Serialize(original);
        var deserialized = NeoMessagePackSerializer.Deserialize<Header>(bytes);

        // Assert
        Assert.IsNotNull(deserialized);
        Assert.AreEqual(original.Index, deserialized.Index);
        Assert.AreEqual(original.Timestamp, deserialized.Timestamp);
        Assert.AreEqual(original.Hash, deserialized.Hash);
    }

    [TestMethod]
    public void Header_Nullable_Null_RoundTrip()
    {
        // Arrange
        Header? original = null;

        // Act
        var bytes = NeoMessagePackSerializer.Serialize(original);
        var deserialized = NeoMessagePackSerializer.Deserialize<Header?>(bytes);

        // Assert
        Assert.IsNull(deserialized);
    }

    [TestMethod]
    public void Header_Compressed_RoundTrip()
    {
        // Arrange
        var original = CreateTestHeader(index: 500, timestamp: 1700000000000);

        // Act
        var bytes = NeoMessagePackSerializer.SerializeCompressed(original);
        var deserialized = NeoMessagePackSerializer.DeserializeCompressed<Header>(bytes);

        // Assert
        Assert.IsNotNull(deserialized);
        Assert.AreEqual(original.Index, deserialized.Index);
        Assert.AreEqual(original.Hash, deserialized.Hash);
    }

    #endregion

    #region Block Tests

    [TestMethod]
    public void Block_SerializeDeserialize_RoundTrip()
    {
        // Arrange
        var original = CreateTestBlock(index: 100, txCount: 0);

        // Act
        var bytes = NeoMessagePackSerializer.Serialize(original);
        var deserialized = NeoMessagePackSerializer.Deserialize<Block>(bytes);

        // Assert
        Assert.IsNotNull(deserialized);
        Assert.AreEqual(original.Index, deserialized.Index);
        Assert.AreEqual(original.Hash, deserialized.Hash);
    }

    [TestMethod]
    public void Block_Nullable_Null_RoundTrip()
    {
        // Arrange
        Block? original = null;

        // Act
        var bytes = NeoMessagePackSerializer.Serialize(original);
        var deserialized = NeoMessagePackSerializer.Deserialize<Block?>(bytes);

        // Assert
        Assert.IsNull(deserialized);
    }

    [TestMethod]
    public void Block_Compressed_RoundTrip()
    {
        // Arrange
        var original = CreateTestBlock(index: 200, txCount: 0);

        // Act
        var bytes = NeoMessagePackSerializer.SerializeCompressed(original);
        var deserialized = NeoMessagePackSerializer.DeserializeCompressed<Block>(bytes);

        // Assert
        Assert.IsNotNull(deserialized);
        Assert.AreEqual(original.Index, deserialized.Index);
        Assert.AreEqual(original.Hash, deserialized.Hash);
    }

    [TestMethod]
    public void Block_Stream_RoundTrip()
    {
        // Arrange
        var original = CreateTestBlock(index: 300, txCount: 0);

        // Act
        using var stream = new MemoryStream();
        NeoMessagePackSerializer.Serialize(stream, original);
        stream.Position = 0;
        var deserialized = NeoMessagePackSerializer.Deserialize<Block>(stream);

        // Assert
        Assert.IsNotNull(deserialized);
        Assert.AreEqual(original.Index, deserialized.Index);
        Assert.AreEqual(original.Hash, deserialized.Hash);
    }

    #endregion

    #region Transaction Tests

    [TestMethod]
    public void Transaction_Nullable_Null_RoundTrip()
    {
        // Arrange
        Transaction? original = null;

        // Act
        var bytes = NeoMessagePackSerializer.Serialize(original);
        var deserialized = NeoMessagePackSerializer.Deserialize<Transaction?>(bytes);

        // Assert
        Assert.IsNull(deserialized);
    }

    #endregion

    #region ISerializableFormatter Tests

    [TestMethod]
    public void ISerializableFormatter_RegisterAndUse()
    {
        // Arrange - Register a custom ISerializable type
        Neo.Serialization.MessagePack.Resolvers.NeoResolver.RegisterSerializable<Witness>();

        var original = new Witness
        {
            InvocationScript = new byte[] { 0x11, 0x22, 0x33 },
            VerificationScript = new byte[] { 0x44, 0x55, 0x66 }
        };

        // Act
        var bytes = NeoMessagePackSerializer.Serialize(original);
        var deserialized = NeoMessagePackSerializer.Deserialize<Witness>(bytes);

        // Assert
        Assert.IsNotNull(deserialized);
        CollectionAssert.AreEqual(original.InvocationScript.ToArray(), deserialized.InvocationScript.ToArray());
    }

    #endregion

    #region Helper Methods

    private static Header CreateTestHeader(uint index, ulong timestamp)
    {
        // Create a minimal valid header for testing
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
            Witness = new Witness
            {
                InvocationScript = Array.Empty<byte>(),
                VerificationScript = Array.Empty<byte>()
            }
        };
        return header;
    }

    private static Block CreateTestBlock(uint index, int txCount)
    {
        var header = CreateTestHeader(index, 1700000000000);
        var block = new Block
        {
            Header = header,
            Transactions = Array.Empty<Transaction>()
        };
        return block;
    }

    #endregion
}

/// <summary>
/// Performance comparison tests for MessagePack vs ISerializable.
/// </summary>
[TestClass]
public sealed class SerializationPerformanceTests
{
    [TestMethod]
    public void UInt256_MessagePack_SmallerThanJson()
    {
        // Arrange
        var value = UInt256.Parse("0x1234567890abcdef1234567890abcdef1234567890abcdef1234567890abcdef");

        // Act
        var msgpackBytes = NeoMessagePackSerializer.Serialize(value);
        var jsonBytes = System.Text.Encoding.UTF8.GetBytes(value.ToString());

        // Assert - MessagePack should be more compact
        Assert.IsTrue(msgpackBytes.Length < jsonBytes.Length,
            $"MessagePack ({msgpackBytes.Length}) should be smaller than JSON ({jsonBytes.Length})");
    }

    [TestMethod]
    public void UInt160_MessagePack_SmallerThanJson()
    {
        // Arrange
        var value = UInt160.Parse("0x1234567890abcdef1234567890abcdef12345678");

        // Act
        var msgpackBytes = NeoMessagePackSerializer.Serialize(value);
        var jsonBytes = System.Text.Encoding.UTF8.GetBytes(value.ToString());

        // Assert - MessagePack should be more compact
        Assert.IsTrue(msgpackBytes.Length < jsonBytes.Length,
            $"MessagePack ({msgpackBytes.Length}) should be smaller than JSON ({jsonBytes.Length})");
    }

    [TestMethod]
    public void Witness_Compressed_SmallerThanUncompressed()
    {
        // Arrange - Create a witness with repetitive data (compresses well)
        var witness = new Witness
        {
            InvocationScript = new byte[200],
            VerificationScript = new byte[100]
        };

        // Act
        var uncompressed = NeoMessagePackSerializer.Serialize(witness);
        var compressed = NeoMessagePackSerializer.SerializeCompressed(witness);

        // Assert - Compressed should be smaller for repetitive data
        Assert.IsTrue(compressed.Length < uncompressed.Length,
            $"Compressed ({compressed.Length}) should be smaller than uncompressed ({uncompressed.Length})");
    }
}
