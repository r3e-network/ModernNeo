// Copyright (C) 2015-2025 The Neo Project.
//
// ISerializableFormatter.cs file belongs to the neo project and is free
// software distributed under the MIT software license.

using System.Buffers;
using MessagePack;
using MessagePack.Formatters;
using Neo.IO;

namespace Neo.Serialization.MessagePack.Formatters;

/// <summary>
/// Generic MessagePack formatter for Neo ISerializable types.
/// Bridges Neo's binary serialization with MessagePack format.
/// </summary>
/// <typeparam name="T">The ISerializable type to format.</typeparam>
public sealed class ISerializableFormatter<T> : IMessagePackFormatter<T?>
    where T : ISerializable, new()
{
    public static readonly ISerializableFormatter<T> Instance = new();

    private ISerializableFormatter() { }

    public void Serialize(ref MessagePackWriter writer, T? value, MessagePackSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNil();
            return;
        }

        // Serialize using Neo's binary format, then wrap in MessagePack binary
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);
        value.Serialize(bw);
        var data = ms.ToArray();
        writer.Write(data);
    }

    public T? Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
    {
        if (reader.TryReadNil())
            return default;

        var bytes = reader.ReadBytes();
        if (bytes is null)
            return default;

        var data = bytes.Value.ToArray();
        var value = new T();
        var memReader = new MemoryReader(data);
        value.Deserialize(ref memReader);
        return value;
    }
}
