// Copyright (C) 2015-2025 The Neo Project.
//
// UT_StorageCompressor.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.Persistence.Compression;
using System;
using System.IO.Compression;
using System.Linq;

namespace Neo.Storage.Tests.Compression
{
    [TestClass]
    public class UT_StorageCompressor
    {
        [TestMethod]
        public void TestDefaultOptions()
        {
            var compressor = new StorageCompressor();

            // Small data should not be compressed
            var smallData = new byte[] { 1, 2, 3, 4, 5 };
            var result = compressor.Compress(smallData);

            CollectionAssert.AreEqual(smallData, result);
        }

        [TestMethod]
        public void TestBrotliCompression()
        {
            var options = new StorageCompressorOptions
            {
                Algorithm = CompressionAlgorithm.Brotli,
                MinSizeForCompression = 10
            };
            var compressor = new StorageCompressor(options);

            // Create compressible data (repeated pattern)
            var data = new byte[1000];
            for (int i = 0; i < data.Length; i++)
            {
                data[i] = (byte)(i % 10);
            }

            var compressed = compressor.Compress(data);
            var decompressed = compressor.Decompress(compressed);

            CollectionAssert.AreEqual(data, decompressed);
            Assert.IsTrue(compressed.Length < data.Length);
        }

        [TestMethod]
        public void TestGZipCompression()
        {
            var options = new StorageCompressorOptions
            {
                Algorithm = CompressionAlgorithm.GZip,
                MinSizeForCompression = 10
            };
            var compressor = new StorageCompressor(options);

            var data = new byte[1000];
            for (int i = 0; i < data.Length; i++)
            {
                data[i] = (byte)(i % 10);
            }

            var compressed = compressor.Compress(data);
            var decompressed = compressor.Decompress(compressed);

            CollectionAssert.AreEqual(data, decompressed);
            Assert.IsTrue(compressed.Length < data.Length);
        }

        [TestMethod]
        public void TestDeflateCompression()
        {
            var options = new StorageCompressorOptions
            {
                Algorithm = CompressionAlgorithm.Deflate,
                MinSizeForCompression = 10
            };
            var compressor = new StorageCompressor(options);

            var data = new byte[1000];
            for (int i = 0; i < data.Length; i++)
            {
                data[i] = (byte)(i % 10);
            }

            var compressed = compressor.Compress(data);
            var decompressed = compressor.Decompress(compressed);

            CollectionAssert.AreEqual(data, decompressed);
            Assert.IsTrue(compressed.Length < data.Length);
        }

        [TestMethod]
        public void TestNoCompressionAlgorithm()
        {
            var options = new StorageCompressorOptions
            {
                Algorithm = CompressionAlgorithm.None,
                MinSizeForCompression = 10
            };
            var compressor = new StorageCompressor(options);

            var data = new byte[100];
            for (int i = 0; i < data.Length; i++)
            {
                data[i] = (byte)i;
            }

            var result = compressor.Compress(data);

            // Should return original data when algorithm is None
            CollectionAssert.AreEqual(data, result);
        }

        [TestMethod]
        public void TestDecompressUncompressedData()
        {
            var compressor = new StorageCompressor();

            var data = new byte[] { 1, 2, 3, 4, 5 };
            var result = compressor.Decompress(data);

            // Should return original data if not compressed
            CollectionAssert.AreEqual(data, result);
        }

        [TestMethod]
        public void TestDecompressShortData()
        {
            var compressor = new StorageCompressor();

            var data = new byte[] { 1, 2, 3 }; // Less than 5 bytes
            var result = compressor.Decompress(data);

            CollectionAssert.AreEqual(data, result);
        }

        [TestMethod]
        public void TestIsCompressed()
        {
            var options = new StorageCompressorOptions
            {
                Algorithm = CompressionAlgorithm.Brotli,
                MinSizeForCompression = 10
            };
            var compressor = new StorageCompressor(options);

            var data = new byte[1000];
            for (int i = 0; i < data.Length; i++)
            {
                data[i] = (byte)(i % 10);
            }

            var compressed = compressor.Compress(data);

            Assert.IsTrue(StorageCompressor.IsCompressed(compressed));
            Assert.IsFalse(StorageCompressor.IsCompressed(data));
        }

        [TestMethod]
        public void TestIsCompressedShortData()
        {
            var shortData = new byte[] { 1, 2, 3 };
            Assert.IsFalse(StorageCompressor.IsCompressed(shortData));
        }

        [TestMethod]
        public void TestStatistics()
        {
            var options = new StorageCompressorOptions
            {
                Algorithm = CompressionAlgorithm.Brotli,
                MinSizeForCompression = 10
            };
            var compressor = new StorageCompressor(options);

            // Initial statistics
            var stats = compressor.GetStatistics();
            Assert.AreEqual(0, stats.CompressedBytes);
            Assert.AreEqual(0, stats.UncompressedBytes);
            Assert.AreEqual(0, stats.CompressionCount);
            Assert.AreEqual(0, stats.DecompressionCount);
            Assert.AreEqual(1.0, stats.CompressionRatio);

            // Compress some data
            var data = new byte[1000];
            for (int i = 0; i < data.Length; i++)
            {
                data[i] = (byte)(i % 10);
            }

            var compressed = compressor.Compress(data);
            compressor.Decompress(compressed);

            stats = compressor.GetStatistics();
            Assert.IsTrue(stats.CompressedBytes > 0);
            Assert.AreEqual(1000, stats.UncompressedBytes);
            Assert.AreEqual(1, stats.CompressionCount);
            Assert.AreEqual(1, stats.DecompressionCount);
            Assert.IsTrue(stats.BytesSaved > 0);
            Assert.IsTrue(stats.CompressionRatio < 1.0);
        }

        [TestMethod]
        public void TestBytesSaved()
        {
            var options = new StorageCompressorOptions
            {
                Algorithm = CompressionAlgorithm.Brotli,
                MinSizeForCompression = 10
            };
            var compressor = new StorageCompressor(options);

            var data = new byte[1000];
            for (int i = 0; i < data.Length; i++)
            {
                data[i] = (byte)(i % 10);
            }

            compressor.Compress(data);

            Assert.IsTrue(compressor.BytesSaved > 0);
        }

        [TestMethod]
        public void TestCompressionRatio()
        {
            var options = new StorageCompressorOptions
            {
                Algorithm = CompressionAlgorithm.Brotli,
                MinSizeForCompression = 10
            };
            var compressor = new StorageCompressor(options);

            // Initial ratio should be 1.0
            Assert.AreEqual(1.0, compressor.CompressionRatio);

            var data = new byte[1000];
            for (int i = 0; i < data.Length; i++)
            {
                data[i] = (byte)(i % 10);
            }

            compressor.Compress(data);

            Assert.IsTrue(compressor.CompressionRatio < 1.0);
        }

        [TestMethod]
        public void TestIncompressibleData()
        {
            var options = new StorageCompressorOptions
            {
                Algorithm = CompressionAlgorithm.Brotli,
                MinSizeForCompression = 10
            };
            var compressor = new StorageCompressor(options);

            // Random data is hard to compress
            var random = new Random(42);
            var data = new byte[100];
            random.NextBytes(data);

            var result = compressor.Compress(data);

            // If compression doesn't help, original data is returned
            // Either compressed or original should work
            var decompressed = compressor.Decompress(result);
            CollectionAssert.AreEqual(data, decompressed);
        }

        [TestMethod]
        public void TestCompressionLevelOption()
        {
            var options = new StorageCompressorOptions
            {
                Algorithm = CompressionAlgorithm.Brotli,
                MinSizeForCompression = 10,
                CompressionLevel = CompressionLevel.SmallestSize
            };
            var compressor = new StorageCompressor(options);

            var data = new byte[1000];
            for (int i = 0; i < data.Length; i++)
            {
                data[i] = (byte)(i % 10);
            }

            var compressed = compressor.Compress(data);
            var decompressed = compressor.Decompress(compressed);

            CollectionAssert.AreEqual(data, decompressed);
        }

        [TestMethod]
        public void TestMultipleCompressions()
        {
            var options = new StorageCompressorOptions
            {
                Algorithm = CompressionAlgorithm.Brotli,
                MinSizeForCompression = 10
            };
            var compressor = new StorageCompressor(options);

            for (int j = 0; j < 5; j++)
            {
                var data = new byte[500];
                for (int i = 0; i < data.Length; i++)
                {
                    data[i] = (byte)((i + j) % 10);
                }

                var compressed = compressor.Compress(data);
                var decompressed = compressor.Decompress(compressed);

                CollectionAssert.AreEqual(data, decompressed);
            }

            var stats = compressor.GetStatistics();
            Assert.AreEqual(5, stats.CompressionCount);
        }

        [TestMethod]
        public void TestDecompressWithUnknownAlgorithm()
        {
            // Create fake compressed data with unknown algorithm byte
            var fakeCompressed = new byte[] { 0x4E, 0x45, 0x4F, 0x43, 0xFF, 0x00, 0x00, 0x00, 0x00 };

            var compressor = new StorageCompressor();
            var result = compressor.Decompress(fakeCompressed);

            // Should return original data for unknown algorithm
            CollectionAssert.AreEqual(fakeCompressed, result);
        }

        [TestMethod]
        public void TestStorageCompressorOptionsDefaults()
        {
            var options = new StorageCompressorOptions();

            Assert.AreEqual(256, options.MinSizeForCompression);
            Assert.AreEqual(CompressionAlgorithm.Brotli, options.Algorithm);
            Assert.AreEqual(CompressionLevel.Fastest, options.CompressionLevel);
        }

        [TestMethod]
        public void TestCompressionStatisticsStruct()
        {
            var stats = new CompressionStatistics
            {
                CompressedBytes = 100,
                UncompressedBytes = 200,
                BytesSaved = 100,
                CompressionRatio = 0.5,
                CompressionCount = 10,
                DecompressionCount = 5
            };

            Assert.AreEqual(100, stats.CompressedBytes);
            Assert.AreEqual(200, stats.UncompressedBytes);
            Assert.AreEqual(100, stats.BytesSaved);
            Assert.AreEqual(0.5, stats.CompressionRatio);
            Assert.AreEqual(10, stats.CompressionCount);
            Assert.AreEqual(5, stats.DecompressionCount);
        }
    }
}
