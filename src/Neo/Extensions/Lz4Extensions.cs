// Copyright (C) 2015-2025 The Neo Project.
//
// Lz4Extensions.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using K4os.Compression.LZ4;
using System;

namespace Neo.Extensions
{
    /// <summary>
    /// LZ4 compression and decompression extension methods.
    /// </summary>
    public static class Lz4Extensions
    {
        /// <summary>
        /// Compresses the specified data using LZ4 algorithm.
        /// </summary>
        /// <param name="data">The data to compress.</param>
        /// <returns>The compressed data.</returns>
        public static byte[] CompressLz4(this ReadOnlySpan<byte> data)
        {
            int maxLength = LZ4Codec.MaximumOutputSize(data.Length);
            byte[] buffer = new byte[maxLength];
            int length = LZ4Codec.Encode(data, buffer);
            byte[] result = new byte[length];
            buffer.AsSpan(0, length).CopyTo(result);
            return result;
        }

        /// <summary>
        /// Decompresses the specified LZ4 compressed data.
        /// </summary>
        /// <param name="data">The compressed data.</param>
        /// <param name="maxOutput">The maximum expected output size.</param>
        /// <returns>The decompressed data.</returns>
        public static byte[] DecompressLz4(this ReadOnlySpan<byte> data, int maxOutput)
        {
            if (maxOutput < 0)
                throw new FormatException("Max output size cannot be negative.");
            byte[] buffer = new byte[maxOutput];
            int length = LZ4Codec.Decode(data, buffer);
            if (length < 0)
                throw new FormatException("LZ4 decompression failed.");
            if (length > maxOutput)
                throw new FormatException($"Decompressed data exceeds max output size ({length} > {maxOutput}).");
            byte[] result = new byte[length];
            buffer.AsSpan(0, length).CopyTo(result);
            return result;
        }
    }
}
