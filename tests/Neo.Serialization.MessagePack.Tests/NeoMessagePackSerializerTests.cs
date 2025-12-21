// Copyright (C) 2015-2025 The Neo Project.
//
// NeoMessagePackSerializerTests.cs file belongs to the neo project and is free
// software distributed under the MIT software license.

using Neo.Serialization.MessagePack;

namespace Neo.Serialization.MessagePack.Tests;

[TestClass]
public sealed class NeoMessagePackSerializerTests
{
    public TestContext TestContext { get; set; } = null!;

    #region UInt160 Tests

    [TestMethod]
    public void UInt160_SerializeDeserialize_RoundTrip()
    {
        // Arrange
        var original = UInt160.Parse("0x0000000000000000000000000000000000000001");

        // Act
        var bytes = NeoMessagePackSerializer.Serialize(original);
        var deserialized = NeoMessagePackSerializer.Deserialize<UInt160>(bytes);

        // Assert
        Assert.AreEqual(original, deserialized);
    }

    [TestMethod]
    public void UInt160_SerializeDeserialize_Zero()
    {
        // Arrange
        var original = UInt160.Zero;

        // Act
        var bytes = NeoMessagePackSerializer.Serialize(original);
        var deserialized = NeoMessagePackSerializer.Deserialize<UInt160>(bytes);

        // Assert
        Assert.AreEqual(original, deserialized);
    }

    [TestMethod]
    public void UInt160_Nullable_Null_RoundTrip()
    {
        // Arrange
        UInt160? original = null;

        // Act
        var bytes = NeoMessagePackSerializer.Serialize(original);
        var deserialized = NeoMessagePackSerializer.Deserialize<UInt160?>(bytes);

        // Assert
        Assert.IsNull(deserialized);
    }

    [TestMethod]
    public void UInt160_Nullable_Value_RoundTrip()
    {
        // Arrange
        UInt160? original = UInt160.Parse("0xabcdef0123456789abcdef0123456789abcdef01");

        // Act
        var bytes = NeoMessagePackSerializer.Serialize(original);
        var deserialized = NeoMessagePackSerializer.Deserialize<UInt160?>(bytes);

        // Assert
        Assert.IsNotNull(deserialized);
        Assert.AreEqual(original, deserialized);
    }

    #endregion

    #region UInt256 Tests

    [TestMethod]
    public void UInt256_SerializeDeserialize_RoundTrip()
    {
        // Arrange
        var original = UInt256.Parse("0x0000000000000000000000000000000000000000000000000000000000000001");

        // Act
        var bytes = NeoMessagePackSerializer.Serialize(original);
        var deserialized = NeoMessagePackSerializer.Deserialize<UInt256>(bytes);

        // Assert
        Assert.AreEqual(original, deserialized);
    }

    [TestMethod]
    public void UInt256_SerializeDeserialize_Zero()
    {
        // Arrange
        var original = UInt256.Zero;

        // Act
        var bytes = NeoMessagePackSerializer.Serialize(original);
        var deserialized = NeoMessagePackSerializer.Deserialize<UInt256>(bytes);

        // Assert
        Assert.AreEqual(original, deserialized);
    }

    [TestMethod]
    public void UInt256_Nullable_Null_RoundTrip()
    {
        // Arrange
        UInt256? original = null;

        // Act
        var bytes = NeoMessagePackSerializer.Serialize(original);
        var deserialized = NeoMessagePackSerializer.Deserialize<UInt256?>(bytes);

        // Assert
        Assert.IsNull(deserialized);
    }

    [TestMethod]
    public void UInt256_Nullable_Value_RoundTrip()
    {
        // Arrange
        UInt256? original = UInt256.Parse("0xfedcba9876543210fedcba9876543210fedcba9876543210fedcba9876543210");

        // Act
        var bytes = NeoMessagePackSerializer.Serialize(original);
        var deserialized = NeoMessagePackSerializer.Deserialize<UInt256?>(bytes);

        // Assert
        Assert.IsNotNull(deserialized);
        Assert.AreEqual(original, deserialized);
    }

    #endregion

    #region Compression Tests

    [TestMethod]
    public void UInt256_Compressed_RoundTrip()
    {
        // Arrange
        var original = UInt256.Parse("0x1234567890abcdef1234567890abcdef1234567890abcdef1234567890abcdef");

        // Act
        var bytes = NeoMessagePackSerializer.SerializeCompressed(original);
        var deserialized = NeoMessagePackSerializer.DeserializeCompressed<UInt256>(bytes);

        // Assert
        Assert.AreEqual(original, deserialized);
    }

    [TestMethod]
    public void UInt160_Compressed_RoundTrip()
    {
        // Arrange
        var original = UInt160.Parse("0x1234567890abcdef1234567890abcdef12345678");

        // Act
        var bytes = NeoMessagePackSerializer.SerializeCompressed(original);
        var deserialized = NeoMessagePackSerializer.DeserializeCompressed<UInt160>(bytes);

        // Assert
        Assert.AreEqual(original, deserialized);
    }

    #endregion

    #region Stream Tests

    [TestMethod]
    public void UInt256_Stream_RoundTrip()
    {
        // Arrange
        var original = UInt256.Parse("0xaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");

        // Act
        using var stream = new MemoryStream();
        NeoMessagePackSerializer.Serialize(stream, original);
        stream.Position = 0;
        var deserialized = NeoMessagePackSerializer.Deserialize<UInt256>(stream);

        // Assert
        Assert.AreEqual(original, deserialized);
    }

    [TestMethod]
    public async Task UInt256_StreamAsync_RoundTrip()
    {
        // Arrange
        var original = UInt256.Parse("0xbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb");

        // Act
        using var stream = new MemoryStream();
        await NeoMessagePackSerializer.SerializeAsync(stream, original, TestContext.CancellationTokenSource.Token);
        stream.Position = 0;
        var deserialized = await NeoMessagePackSerializer.DeserializeAsync<UInt256>(stream, TestContext.CancellationTokenSource.Token);

        // Assert
        Assert.AreEqual(original, deserialized);
    }

    #endregion

    #region ReadOnlyMemory Tests

    [TestMethod]
    public void UInt256_ReadOnlyMemory_Deserialize()
    {
        // Arrange
        var original = UInt256.Parse("0xcccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc");
        var bytes = NeoMessagePackSerializer.Serialize(original);
        ReadOnlyMemory<byte> memory = bytes;

        // Act
        var deserialized = NeoMessagePackSerializer.Deserialize<UInt256>(memory);

        // Assert
        Assert.AreEqual(original, deserialized);
    }

    [TestMethod]
    public void UInt160_ReadOnlyMemory_Deserialize()
    {
        // Arrange
        var original = UInt160.Parse("0xdddddddddddddddddddddddddddddddddddddddd");
        var bytes = NeoMessagePackSerializer.Serialize(original);
        ReadOnlyMemory<byte> memory = bytes;

        // Act
        var deserialized = NeoMessagePackSerializer.Deserialize<UInt160>(memory);

        // Assert
        Assert.AreEqual(original, deserialized);
    }

    #endregion

    #region Binary Size Tests

    [TestMethod]
    public void UInt160_BinarySize_Is21Bytes()
    {
        // Arrange - MessagePack binary header (1 byte) + 20 bytes data
        var value = UInt160.Zero;

        // Act
        var bytes = NeoMessagePackSerializer.Serialize(value);

        // Assert - bin8 header (2 bytes: 0xC4 + length) + 20 bytes = 22 bytes
        // Or fixbin (1 byte header if length < 32) + 20 bytes = 21 bytes
        Assert.IsTrue(bytes.Length >= 21 && bytes.Length <= 22,
            $"Expected 21-22 bytes, got {bytes.Length}");
    }

    [TestMethod]
    public void UInt256_BinarySize_Is34Bytes()
    {
        // Arrange - MessagePack binary header + 32 bytes data
        var value = UInt256.Zero;

        // Act
        var bytes = NeoMessagePackSerializer.Serialize(value);

        // Assert - bin8 header (2 bytes: 0xC4 + length) + 32 bytes = 34 bytes
        Assert.AreEqual(34, bytes.Length);
    }

    #endregion
}
