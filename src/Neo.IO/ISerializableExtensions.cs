// Copyright (C) 2015-2025 The Neo Project.
//
// ISerializableExtensions.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.IO;
using System;
using System.Buffers;
using System.IO;
using System.Runtime.CompilerServices;

namespace Neo.Extensions
{
    public static class ISerializableExtensions
    {
        /// <summary>
        /// Converts an <see cref="ISerializable"/> object to a byte array.
        /// </summary>
        /// <param name="value">The <see cref="ISerializable"/> object to be converted.</param>
        /// <returns>The converted byte array.</returns>
        public static byte[] ToArray(this ISerializable value)
        {
            using MemoryStream ms = new();
            using BinaryWriter writer = new(ms, Utility.StrictUTF8, true);
            value.Serialize(writer);
            writer.Flush();
            return ms.ToArray();
        }

        /// <summary>
        /// Serializes an <see cref="ISerializable"/> object to a span using pooled memory.
        /// Returns the number of bytes written.
        /// </summary>
        /// <param name="value">The <see cref="ISerializable"/> object to be serialized.</param>
        /// <param name="destination">The destination span to write to.</param>
        /// <returns>The number of bytes written.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int SerializeTo(this ISerializable value, Span<byte> destination)
        {
            using var ms = new MemoryStream(destination.Length);
            using var writer = new BinaryWriter(ms, Utility.StrictUTF8, true);
            value.Serialize(writer);
            writer.Flush();
            var written = (int)ms.Position;
            ms.GetBuffer().AsSpan(0, written).CopyTo(destination);
            return written;
        }

        /// <summary>
        /// Converts an <see cref="ISerializable"/> object to a byte array using pooled memory.
        /// More efficient for temporary serialization operations.
        /// </summary>
        /// <param name="value">The <see cref="ISerializable"/> object to be converted.</param>
        /// <param name="rentedBuffer">The rented buffer from ArrayPool. Caller must return it.</param>
        /// <returns>The number of bytes written to the buffer.</returns>
        public static int ToArrayPooled(this ISerializable value, out byte[] rentedBuffer)
        {
            // Estimate size based on the Size property if available
            int estimatedSize = value.Size;
            rentedBuffer = ArrayPool<byte>.Shared.Rent(Math.Max(estimatedSize, 256));

            try
            {
                using var ms = new MemoryStream(rentedBuffer, 0, rentedBuffer.Length, writable: true);
                using var writer = new BinaryWriter(ms, Utility.StrictUTF8, true);
                value.Serialize(writer);
                writer.Flush();
                return (int)ms.Position;
            }
            catch
            {
                ArrayPool<byte>.Shared.Return(rentedBuffer);
                throw;
            }
        }
    }
}

