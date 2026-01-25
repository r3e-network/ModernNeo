// Copyright (C) 2015-2025 The Neo Project.
//
// MessageBuffer.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using System;

namespace Neo.IO
{
    /// <summary>
    /// A simple immutable byte sequence wrapper for P2P messaging.
    /// </summary>
    public readonly struct MessageBuffer
    {
        private readonly ReadOnlyMemory<byte> _data;

        public MessageBuffer(ReadOnlyMemory<byte> data)
        {
            _data = data;
        }

        public MessageBuffer(byte[] data)
        {
            _data = data;
        }

        /// <summary>
        /// Gets the number of bytes in this buffer.
        /// </summary>
        public int Count => _data.Length;

        /// <summary>
        /// Gets the underlying span.
        /// </summary>
        public ReadOnlySpan<byte> Span => _data.Span;

        /// <summary>
        /// Creates a MessageBuffer from a byte array.
        /// </summary>
        public static MessageBuffer FromBytes(byte[] bytes) => new(bytes);

        /// <summary>
        /// Creates a MessageBuffer from a ReadOnlyMemory.
        /// </summary>
        public static MessageBuffer FromMemory(ReadOnlyMemory<byte> memory) => new(memory);

        /// <summary>
        /// Returns a slice of this buffer.
        /// </summary>
        public MessageBuffer Slice(int start, int length) => new(_data.Slice(start, length));

        /// <summary>
        /// Returns a slice of this buffer from start to end.
        /// </summary>
        public MessageBuffer Slice(int start) => new(_data.Slice(start));

        /// <summary>
        /// Converts this buffer to a byte array.
        /// </summary>
        public byte[] ToArray() => _data.ToArray();

        /// <summary>
        /// Concatenates two buffers.
        /// </summary>
        public static MessageBuffer operator +(MessageBuffer left, MessageBuffer right)
        {
            var result = new byte[left.Count + right.Count];
            left._data.Span.CopyTo(result);
            right._data.Span.CopyTo(result.AsSpan(left.Count));
            return new MessageBuffer(result);
        }

        /// <summary>
        /// Creates an empty buffer.
        /// </summary>
        public static MessageBuffer Empty => new(ReadOnlyMemory<byte>.Empty);

        /// <summary>
        /// Implicit conversion from byte array.
        /// </summary>
        public static implicit operator MessageBuffer(byte[] bytes) => new(bytes);

        /// <summary>
        /// Implicit conversion from ReadOnlyMemory.
        /// </summary>
        public static implicit operator MessageBuffer(ReadOnlyMemory<byte> memory) => new(memory);
    }
}
