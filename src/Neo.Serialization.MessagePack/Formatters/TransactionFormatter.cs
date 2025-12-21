// Copyright (C) 2015-2025 The Neo Project.
//
// TransactionFormatter.cs file belongs to the neo project and is free
// software distributed under the MIT software license.

using System.Buffers;
using MessagePack;
using MessagePack.Formatters;
using Neo.Extensions;
using Neo.IO;
using Neo.Network.P2P.Payloads;

namespace Neo.Serialization.MessagePack.Formatters;

/// <summary>
/// MessagePack formatter for Neo Transaction type.
/// Provides efficient serialization using Neo's native binary format.
/// </summary>
/// <remarks>
/// This formatter wraps Neo's ISerializable binary format in MessagePack binary type.
/// For Orleans grain state persistence and cross-node communication.
/// </remarks>
public sealed class TransactionFormatter : IMessagePackFormatter<Transaction?>
{
    public static readonly TransactionFormatter Instance = new();

    private TransactionFormatter() { }

    public void Serialize(ref MessagePackWriter writer, Transaction? value, MessagePackSerializerOptions options)
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

    public Transaction? Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
    {
        if (reader.TryReadNil())
            return null;

        var bytes = reader.ReadBytes();
        if (bytes is null)
            return null;

        var data = SequenceToArray(bytes.Value);
        return data.AsSerializable<Transaction>();
    }

    private static byte[] SequenceToArray(ReadOnlySequence<byte> sequence)
    {
        if (sequence.IsSingleSegment)
            return sequence.First.ToArray();
        return sequence.ToArray();
    }
}

/// <summary>
/// MessagePack formatter for Neo Witness type.
/// </summary>
public sealed class WitnessFormatter : IMessagePackFormatter<Witness?>
{
    public static readonly WitnessFormatter Instance = new();

    private WitnessFormatter() { }

    public void Serialize(ref MessagePackWriter writer, Witness? value, MessagePackSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNil();
            return;
        }

        var data = value.ToArray();
        writer.Write(data);
    }

    public Witness? Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
    {
        if (reader.TryReadNil())
            return null;

        var bytes = reader.ReadBytes();
        if (bytes is null)
            return null;

        var data = SequenceToArray(bytes.Value);
        return data.AsSerializable<Witness>();
    }

    private static byte[] SequenceToArray(ReadOnlySequence<byte> sequence)
    {
        if (sequence.IsSingleSegment)
            return sequence.First.ToArray();
        return sequence.ToArray();
    }
}

/// <summary>
/// MessagePack formatter for Neo Signer type.
/// </summary>
public sealed class SignerFormatter : IMessagePackFormatter<Signer?>
{
    public static readonly SignerFormatter Instance = new();

    private SignerFormatter() { }

    public void Serialize(ref MessagePackWriter writer, Signer? value, MessagePackSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNil();
            return;
        }

        var data = value.ToArray();
        writer.Write(data);
    }

    public Signer? Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
    {
        if (reader.TryReadNil())
            return null;

        var bytes = reader.ReadBytes();
        if (bytes is null)
            return null;

        var data = SequenceToArray(bytes.Value);
        return data.AsSerializable<Signer>();
    }

    private static byte[] SequenceToArray(ReadOnlySequence<byte> sequence)
    {
        if (sequence.IsSingleSegment)
            return sequence.First.ToArray();
        return sequence.ToArray();
    }
}
