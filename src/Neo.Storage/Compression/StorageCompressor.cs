// Copyright (C) 2015-2025 The Neo Project.
//
// StorageCompressor.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using System;
using System.Buffers;
using System.IO;
using System.IO.Compression;
using System.Runtime.CompilerServices;

namespace Neo.Persistence.Compression
{
    /// <summary>
    /// Provides compression and decompression for storage values.
    /// Supports multiple compression algorithms with automatic selection.
    /// </summary>
    public sealed class StorageCompressor
    {
        private readonly StorageCompressorOptions _options;
        private long _compressedBytes;
        private long _uncompressedBytes;
        private long _compressionCount;
        private long _decompressionCount;

        /// <summary>
        /// Magic bytes to identify compressed data.
        /// </summary>
        private static ReadOnlySpan<byte> CompressedMagic => [0x4E, 0x45, 0x4F, 0x43]; // "NEOC"

        /// <summary>
        /// Gets the total bytes saved by compression.
        /// </summary>
        public long BytesSaved => _uncompressedBytes - _compressedBytes;

        /// <summary>
        /// Gets the compression ratio (compressed / uncompressed).
        /// </summary>
        public double CompressionRatio =>
            _uncompressedBytes == 0 ? 1.0 : (double)_compressedBytes / _uncompressedBytes;

        /// <summary>
        /// Creates a new storage compressor with the specified options.
        /// </summary>
        public StorageCompressor(StorageCompressorOptions? options = null)
        {
            _options = options ?? new StorageCompressorOptions();
        }

        /// <summary>
        /// Compresses data if it exceeds the minimum size threshold.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte[] Compress(byte[] data)
        {
            if (data.Length < _options.MinSizeForCompression)
            {
                return data;
            }

            return _options.Algorithm switch
            {
                CompressionAlgorithm.Brotli => CompressBrotli(data),
                CompressionAlgorithm.GZip => CompressGZip(data),
                CompressionAlgorithm.Deflate => CompressDeflate(data),
                _ => data
            };
        }

        /// <summary>
        /// Decompresses data if it was compressed.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte[] Decompress(byte[] data)
        {
            if (data.Length < 5 || !data.AsSpan(0, 4).SequenceEqual(CompressedMagic))
            {
                return data;
            }

            var algorithm = (CompressionAlgorithm)data[4];
            return algorithm switch
            {
                CompressionAlgorithm.Brotli => DecompressBrotli(data),
                CompressionAlgorithm.GZip => DecompressGZip(data),
                CompressionAlgorithm.Deflate => DecompressDeflate(data),
                _ => data
            };
        }

        /// <summary>
        /// Checks if data is compressed.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsCompressed(ReadOnlySpan<byte> data)
        {
            return data.Length >= 5 && data[..4].SequenceEqual(CompressedMagic);
        }

        private byte[] CompressBrotli(byte[] data)
        {
            using var output = new MemoryStream();

            // Write magic header
            output.Write(CompressedMagic);
            output.WriteByte((byte)CompressionAlgorithm.Brotli);

            // Write original length (for pre-allocation during decompression)
            Span<byte> lengthBytes = stackalloc byte[4];
            BitConverter.TryWriteBytes(lengthBytes, data.Length);
            output.Write(lengthBytes);

            using (var brotli = new BrotliStream(output, _options.CompressionLevel, leaveOpen: true))
            {
                brotli.Write(data);
            }

            var compressed = output.ToArray();

            // Only use compressed if it's actually smaller
            if (compressed.Length < data.Length)
            {
                _compressedBytes += compressed.Length;
                _uncompressedBytes += data.Length;
                _compressionCount++;
                return compressed;
            }

            return data;
        }

        private byte[] DecompressBrotli(byte[] data)
        {
            var originalLength = BitConverter.ToInt32(data, 5);
            var result = new byte[originalLength];

            using var input = new MemoryStream(data, 9, data.Length - 9);
            using var brotli = new BrotliStream(input, CompressionMode.Decompress);

            var totalRead = 0;
            while (totalRead < originalLength)
            {
                var read = brotli.Read(result, totalRead, originalLength - totalRead);
                if (read == 0) break;
                totalRead += read;
            }

            _decompressionCount++;
            return result;
        }

        private byte[] CompressGZip(byte[] data)
        {
            using var output = new MemoryStream();

            // Write magic header
            output.Write(CompressedMagic);
            output.WriteByte((byte)CompressionAlgorithm.GZip);

            // Write original length
            Span<byte> lengthBytes = stackalloc byte[4];
            BitConverter.TryWriteBytes(lengthBytes, data.Length);
            output.Write(lengthBytes);

            using (var gzip = new GZipStream(output, _options.CompressionLevel, leaveOpen: true))
            {
                gzip.Write(data);
            }

            var compressed = output.ToArray();

            if (compressed.Length < data.Length)
            {
                _compressedBytes += compressed.Length;
                _uncompressedBytes += data.Length;
                _compressionCount++;
                return compressed;
            }

            return data;
        }

        private byte[] DecompressGZip(byte[] data)
        {
            var originalLength = BitConverter.ToInt32(data, 5);
            var result = new byte[originalLength];

            using var input = new MemoryStream(data, 9, data.Length - 9);
            using var gzip = new GZipStream(input, CompressionMode.Decompress);

            var totalRead = 0;
            while (totalRead < originalLength)
            {
                var read = gzip.Read(result, totalRead, originalLength - totalRead);
                if (read == 0) break;
                totalRead += read;
            }

            _decompressionCount++;
            return result;
        }

        private byte[] CompressDeflate(byte[] data)
        {
            using var output = new MemoryStream();

            // Write magic header
            output.Write(CompressedMagic);
            output.WriteByte((byte)CompressionAlgorithm.Deflate);

            // Write original length
            Span<byte> lengthBytes = stackalloc byte[4];
            BitConverter.TryWriteBytes(lengthBytes, data.Length);
            output.Write(lengthBytes);

            using (var deflate = new DeflateStream(output, _options.CompressionLevel, leaveOpen: true))
            {
                deflate.Write(data);
            }

            var compressed = output.ToArray();

            if (compressed.Length < data.Length)
            {
                _compressedBytes += compressed.Length;
                _uncompressedBytes += data.Length;
                _compressionCount++;
                return compressed;
            }

            return data;
        }

        private byte[] DecompressDeflate(byte[] data)
        {
            var originalLength = BitConverter.ToInt32(data, 5);
            var result = new byte[originalLength];

            using var input = new MemoryStream(data, 9, data.Length - 9);
            using var deflate = new DeflateStream(input, CompressionMode.Decompress);

            var totalRead = 0;
            while (totalRead < originalLength)
            {
                var read = deflate.Read(result, totalRead, originalLength - totalRead);
                if (read == 0) break;
                totalRead += read;
            }

            _decompressionCount++;
            return result;
        }

        /// <summary>
        /// Gets compression statistics.
        /// </summary>
        public CompressionStatistics GetStatistics()
        {
            return new CompressionStatistics
            {
                CompressedBytes = _compressedBytes,
                UncompressedBytes = _uncompressedBytes,
                BytesSaved = BytesSaved,
                CompressionRatio = CompressionRatio,
                CompressionCount = _compressionCount,
                DecompressionCount = _decompressionCount
            };
        }
    }

    /// <summary>
    /// Supported compression algorithms.
    /// </summary>
    public enum CompressionAlgorithm : byte
    {
        None = 0,
        Brotli = 1,
        GZip = 2,
        Deflate = 3
    }

    /// <summary>
    /// Configuration options for storage compressor.
    /// </summary>
    public sealed class StorageCompressorOptions
    {
        /// <summary>
        /// Minimum data size to trigger compression. Default: 256 bytes.
        /// </summary>
        public int MinSizeForCompression { get; set; } = 256;

        /// <summary>
        /// Compression algorithm to use. Default: Brotli.
        /// </summary>
        public CompressionAlgorithm Algorithm { get; set; } = CompressionAlgorithm.Brotli;

        /// <summary>
        /// Compression level. Default: Fastest.
        /// </summary>
        public CompressionLevel CompressionLevel { get; set; } = CompressionLevel.Fastest;
    }

    /// <summary>
    /// Compression statistics for monitoring.
    /// </summary>
    public readonly struct CompressionStatistics
    {
        public long CompressedBytes { get; init; }
        public long UncompressedBytes { get; init; }
        public long BytesSaved { get; init; }
        public double CompressionRatio { get; init; }
        public long CompressionCount { get; init; }
        public long DecompressionCount { get; init; }
    }
}
