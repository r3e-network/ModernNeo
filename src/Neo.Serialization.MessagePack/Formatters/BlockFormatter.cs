// Copyright (C) 2015-2025 The Neo Project.
//
// BlockFormatter.cs file belongs to the neo project and is free
// software distributed under the MIT software license.

using System.Buffers;
using MessagePack;
using MessagePack.Formatters;
using Neo.Extensions;
using Neo.IO;
using Neo.Network.P2P.Payloads;

namespace Neo.Serialization.MessagePack.Formatters;

/// <summary>
/// MessagePack formatter for Neo Block type.
/// Provides efficient serialization using Neo's native binary format.
/// </summary>
/// <remarks>
/// This formatter wraps Neo's ISerializable binary format in MessagePack binary type.
/// For Orleans grain state persistence and cross-node communication.
/// </remarks>
public sealed class BlockFormatter : IMessagePackFormatter<Block?>
{
    public static readonly BlockFormatter Instance = new();

    private BlockFormatter() { }

    public void Serialize(ref MessagePackWriter writer, Block? value, MessagePackSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNil();
            return;
        }

        // Use Neo's native serialization for maximum compatibility
        var data = value.ToArray();
        writer.Write(data);
    }

    public Block? Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
    {
        if (reader.TryReadNil())
            return null;

        var bytes = reader.ReadBytes();
        if (bytes is null)
            return null;

        var data = SequenceToArray(bytes.Value);
        return data.AsSerializable<Block>();
    }

    private static byte[] SequenceToArray(ReadOnlySequence<byte> sequence)
    {
        if (sequence.IsSingleSegment)
            return sequence.First.ToArray();
        return sequence.ToArray();
    }
}

/// <summary>
/// MessagePack formatter for Neo Header type.
/// </summary>
public sealed class HeaderFormatter : IMessagePackFormatter<Header?>
{
    public static readonly HeaderFormatter Instance = new();

    private HeaderFormatter() { }

    public void Serialize(ref MessagePackWriter writer, Header? value, MessagePackSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNil();
            return;
        }

        var data = value.ToArray();
        writer.Write(data);
    }

    public Header? Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
    {
        if (reader.TryReadNil())
            return null;

        var bytes = reader.ReadBytes();
        if (bytes is null)
            return null;

        var data = SequenceToArray(bytes.Value);
        return data.AsSerializable<Header>();
    }

    private static byte[] SequenceToArray(ReadOnlySequence<byte> sequence)
    {
        if (sequence.IsSingleSegment)
            return sequence.First.ToArray();
        return sequence.ToArray();
    }
}
