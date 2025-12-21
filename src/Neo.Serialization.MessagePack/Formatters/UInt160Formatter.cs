// Copyright (C) 2015-2025 The Neo Project.
//
// UInt160Formatter.cs file belongs to the neo project and is free
// software distributed under the MIT software license.

using System.Buffers;
using MessagePack;
using MessagePack.Formatters;

namespace Neo.Serialization.MessagePack.Formatters;

/// <summary>
/// MessagePack formatter for UInt160 (20-byte hash).
/// Provides zero-copy serialization for Neo addresses and script hashes.
/// </summary>
public sealed class UInt160Formatter : IMessagePackFormatter<UInt160?>
{
    public static readonly UInt160Formatter Instance = new();

    private UInt160Formatter() { }

    public void Serialize(ref MessagePackWriter writer, UInt160? value, MessagePackSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNil();
            return;
        }

        // Write as 20-byte binary
        writer.WriteBinHeader(20);
        var span = writer.GetSpan(20);
        value.GetSpan().CopyTo(span);
        writer.Advance(20);
    }

    public UInt160? Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
    {
        if (reader.TryReadNil())
            return null;

        var bytes = reader.ReadBytes();
        if (bytes is null)
            return null;

        // Convert ReadOnlySequence to array
        var data = bytes.Value.ToArray();
        if (data.Length != 20)
            throw new MessagePackSerializationException("Invalid UInt160 data length");

        return new UInt160(data);
    }
}
