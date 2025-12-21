// Copyright (C) 2015-2025 The Neo Project.
//
// NeoMessagePackSerializer.cs file belongs to the neo project and is free
// software distributed under the MIT software license.

using MessagePack;
using MessagePack.Resolvers;
using Neo.Serialization.MessagePack.Resolvers;

namespace Neo.Serialization.MessagePack;

/// <summary>
/// Neo-specific MessagePack serializer with pre-configured options.
/// Provides high-performance serialization for Neo blockchain types.
/// </summary>
public static class NeoMessagePackSerializer
{
    /// <summary>
    /// Default MessagePack options for Neo types.
    /// Uses Neo resolver with standard resolver fallback.
    /// </summary>
    public static readonly MessagePackSerializerOptions DefaultOptions;

    /// <summary>
    /// Compressed MessagePack options using LZ4 compression.
    /// Recommended for network transmission and storage.
    /// </summary>
    public static readonly MessagePackSerializerOptions CompressedOptions;

    static NeoMessagePackSerializer()
    {
        // Create composite resolver: Neo types first, then standard types
        var resolver = CompositeResolver.Create(
            NeoResolver.Instance,
            StandardResolver.Instance);

        DefaultOptions = MessagePackSerializerOptions.Standard
            .WithResolver(resolver)
            .WithSecurity(MessagePackSecurity.UntrustedData);

        CompressedOptions = DefaultOptions
            .WithCompression(MessagePackCompression.Lz4BlockArray);
    }

    /// <summary>
    /// Serializes an object to MessagePack binary format.
    /// </summary>
    public static byte[] Serialize<T>(T value)
    {
        return MessagePackSerializer.Serialize(value, DefaultOptions);
    }

    /// <summary>
    /// Serializes an object to MessagePack binary format with compression.
    /// </summary>
    public static byte[] SerializeCompressed<T>(T value)
    {
        return MessagePackSerializer.Serialize(value, CompressedOptions);
    }

    /// <summary>
    /// Deserializes MessagePack binary to an object.
    /// </summary>
    public static T Deserialize<T>(byte[] data)
    {
        return MessagePackSerializer.Deserialize<T>(data, DefaultOptions);
    }

    /// <summary>
    /// Deserializes MessagePack binary to an object.
    /// </summary>
    public static T Deserialize<T>(ReadOnlyMemory<byte> data)
    {
        return MessagePackSerializer.Deserialize<T>(data, DefaultOptions);
    }

    /// <summary>
    /// Deserializes compressed MessagePack binary to an object.
    /// </summary>
    public static T DeserializeCompressed<T>(byte[] data)
    {
        return MessagePackSerializer.Deserialize<T>(data, CompressedOptions);
    }

    /// <summary>
    /// Deserializes compressed MessagePack binary to an object.
    /// </summary>
    public static T DeserializeCompressed<T>(ReadOnlyMemory<byte> data)
    {
        return MessagePackSerializer.Deserialize<T>(data, CompressedOptions);
    }

    /// <summary>
    /// Serializes an object to a stream.
    /// </summary>
    public static void Serialize<T>(Stream stream, T value)
    {
        MessagePackSerializer.Serialize(stream, value, DefaultOptions);
    }

    /// <summary>
    /// Deserializes an object from a stream.
    /// </summary>
    public static T Deserialize<T>(Stream stream)
    {
        return MessagePackSerializer.Deserialize<T>(stream, DefaultOptions);
    }

    /// <summary>
    /// Asynchronously serializes an object to a stream.
    /// </summary>
    public static async Task SerializeAsync<T>(Stream stream, T value, CancellationToken cancellationToken = default)
    {
        await MessagePackSerializer.SerializeAsync(stream, value, DefaultOptions, cancellationToken);
    }

    /// <summary>
    /// Asynchronously deserializes an object from a stream.
    /// </summary>
    public static async ValueTask<T> DeserializeAsync<T>(Stream stream, CancellationToken cancellationToken = default)
    {
        return await MessagePackSerializer.DeserializeAsync<T>(stream, DefaultOptions, cancellationToken);
    }
}
