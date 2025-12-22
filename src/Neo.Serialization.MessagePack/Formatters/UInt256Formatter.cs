// Copyright (C) 2015-2025 The Neo Project.
//
// UInt256Formatter.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using MessagePack;
using MessagePack.Formatters;
using System.Buffers;

namespace Neo.Serialization.MessagePack.Formatters
{
    /// <summary>
    /// MessagePack formatter for UInt256 (32-byte hash).
    /// Provides zero-copy serialization for Neo block/transaction hashes.
    /// </summary>
    public sealed class UInt256Formatter : IMessagePackFormatter<UInt256?>
    {
        public static readonly UInt256Formatter Instance = new();

        private UInt256Formatter() { }

        public void Serialize(ref MessagePackWriter writer, UInt256? value, MessagePackSerializerOptions options)
        {
            if (value is null)
            {
                writer.WriteNil();
                return;
            }

            // Write as 32-byte binary
            writer.WriteBinHeader(32);
            var span = writer.GetSpan(32);
            value.GetSpan().CopyTo(span);
            writer.Advance(32);
        }

        public UInt256? Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil())
                return null;

            var bytes = reader.ReadBytes();
            if (bytes is null)
                return null;

            // Convert ReadOnlySequence to array
            var data = bytes.Value.ToArray();
            if (data.Length != 32)
                throw new MessagePackSerializationException("Invalid UInt256 data length");

            return new UInt256(data);
        }
    }
}
