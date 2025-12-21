// Copyright (C) 2015-2025 The Neo Project.
//
// UT_StorageCompressor.cs file belongs to the neo project and is free
// software distributed under the MIT software license.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.Persistence.Compression;
using System;
using System.IO.Compression;
using System.Text;

namespace Neo.UnitTests.Storage.Caching
{
    [TestClass]
    public class UT_StorageCompressor
    {
        [TestMethod]
        public void TestStorageCompressorOptions_DefaultValues()
        {
            var options = new StorageCompressorOptions();

            Assert.AreEqual(256, options.MinSizeForCompression);
            Assert.AreEqual(CompressionAlgorithm.Brotli, options.Algorithm);
            Assert.AreEqual(CompressionLevel.Fastest, options.CompressionLevel);
        }

        [TestMethod]
        public void TestCompress_SmallData_ReturnsUncompressed()
        {
            var compressor = new StorageCompressor();
            var smallData = new byte[100]; // Below threshold

            var result = compressor.Compress(smallData);

            Assert.AreSame(smallData, result);
        }

        [TestMethod]
        public void TestCompress_LargeData_Brotli_CompressesData()
        {
            var options = new StorageCompressorOptions
            {
                Algorithm = CompressionAlgorithm.Brotli,
                MinSizeForCompression = 100
            };
            var compressor = new StorageCompressor(options);

            // Create compressible data (repeated pattern)
            var largeData = new byte[1000];
            for (var i = 0; i < largeData.Length; i++)
            {
                largeData[i] = (byte)(i % 10);
            }

            var compressed = compressor.Compress(largeData);

            Assert.IsTrue(compressed.Length < largeData.Length);
            Assert.IsTrue(StorageCompressor.IsCompressed(compressed));
        }

        [TestMethod]
        public void TestCompress_LargeData_GZip_CompressesData()
        {
            var options = new StorageCompressorOptions
            {
                Algorithm = CompressionAlgorithm.GZip,
                MinSizeForCompression = 100
            };
            var compressor = new StorageCompressor(options);

            var largeData = new byte[1000];
            for (var i = 0; i < largeData.Length; i++)
            {
                largeData[i] = (byte)(i % 10);
            }

            var compressed = compressor.Compress(largeData);

            Assert.IsTrue(compressed.Length < largeData.Length);
            Assert.IsTrue(StorageCompressor.IsCompressed(compressed));
        }

        [TestMethod]
        public void TestCompress_LargeData_Deflate_CompressesData()
        {
            var options = new StorageCompressorOptions
            {
                Algorithm = CompressionAlgorithm.Deflate,
                MinSizeForCompression = 100
            };
            var compressor = new StorageCompressor(options);

            var largeData = new byte[1000];
            for (var i = 0; i < largeData.Length; i++)
            {
                largeData[i] = (byte)(i % 10);
            }

            var compressed = compressor.Compress(largeData);

            Assert.IsTrue(compressed.Length < largeData.Length);
            Assert.IsTrue(StorageCompressor.IsCompressed(compressed));
        }

        [TestMethod]
        public void TestDecompress_UncompressedData_ReturnsOriginal()
        {
            var compressor = new StorageCompressor();
            var data = new byte[] { 1, 2, 3, 4, 5 };

            var result = compressor.Decompress(data);

            Assert.AreSame(data, result);
        }

        [TestMethod]
        public void TestCompressDecompress_Brotli_RoundTrip()
        {
            var options = new StorageCompressorOptions
            {
                Algorithm = CompressionAlgorithm.Brotli,
                MinSizeForCompression = 100
            };
            var compressor = new StorageCompressor(options);

            var original = Encoding.UTF8.GetBytes(new string('A', 500) + new string('B', 500));

            var compressed = compressor.Compress(original);
            var decompressed = compressor.Decompress(compressed);

            CollectionAssert.AreEqual(original, decompressed);
        }

        [TestMethod]
        public void TestCompressDecompress_GZip_RoundTrip()
        {
            var options = new StorageCompressorOptions
            {
                Algorithm = CompressionAlgorithm.GZip,
                MinSizeForCompression = 100
            };
            var compressor = new StorageCompressor(options);

            var original = Encoding.UTF8.GetBytes(new string('X', 1000));

            var compressed = compressor.Compress(original);
            var decompressed = compressor.Decompress(compressed);

            CollectionAssert.AreEqual(original, decompressed);
        }

        [TestMethod]
        public void TestCompressDecompress_Deflate_RoundTrip()
        {
            var options = new StorageCompressorOptions
            {
                Algorithm = CompressionAlgorithm.Deflate,
                MinSizeForCompression = 100
            };
            var compressor = new StorageCompressor(options);

            var original = Encoding.UTF8.GetBytes(new string('Z', 1000));

            var compressed = compressor.Compress(original);
            var decompressed = compressor.Decompress(compressed);

            CollectionAssert.AreEqual(original, decompressed);
        }

        [TestMethod]
        public void TestIsCompressed_CompressedData_ReturnsTrue()
        {
            var options = new StorageCompressorOptions { MinSizeForCompression = 100 };
            var compressor = new StorageCompressor(options);

            var data = new byte[500];
            for (var i = 0; i < data.Length; i++) data[i] = (byte)(i % 5);

            var compressed = compressor.Compress(data);

            Assert.IsTrue(StorageCompressor.IsCompressed(compressed));
        }

        [TestMethod]
        public void TestIsCompressed_UncompressedData_ReturnsFalse()
        {
            var data = new byte[] { 1, 2, 3, 4, 5 };

            Assert.IsFalse(StorageCompressor.IsCompressed(data));
        }

        [TestMethod]
        public void TestIsCompressed_EmptyData_ReturnsFalse()
        {
            Assert.IsFalse(StorageCompressor.IsCompressed(Array.Empty<byte>()));
        }

        [TestMethod]
        public void TestGetStatistics_ReturnsValidData()
        {
            var options = new StorageCompressorOptions { MinSizeForCompression = 100 };
            var compressor = new StorageCompressor(options);

            var data = new byte[500];
            for (var i = 0; i < data.Length; i++) data[i] = (byte)(i % 5);

            compressor.Compress(data);
            var compressed = compressor.Compress(data);
            compressor.Decompress(compressed);

            var stats = compressor.GetStatistics();

            Assert.IsTrue(stats.CompressionCount >= 1);
            Assert.IsTrue(stats.DecompressionCount >= 1);
            Assert.IsTrue(stats.BytesSaved > 0);
            Assert.IsTrue(stats.CompressionRatio < 1.0);
        }

        [TestMethod]
        public void TestCompressionAlgorithm_Values()
        {
            Assert.AreEqual(0, (int)CompressionAlgorithm.None);
            Assert.AreEqual(1, (int)CompressionAlgorithm.Brotli);
            Assert.AreEqual(2, (int)CompressionAlgorithm.GZip);
            Assert.AreEqual(3, (int)CompressionAlgorithm.Deflate);
        }

        [TestMethod]
        public void TestCompress_IncompressibleData_ReturnsOriginal()
        {
            var options = new StorageCompressorOptions { MinSizeForCompression = 100 };
            var compressor = new StorageCompressor(options);

            // Random data is hard to compress
            var random = new Random(42);
            var randomData = new byte[300];
            random.NextBytes(randomData);

            var result = compressor.Compress(randomData);

            // If compression doesn't help, original is returned
            if (result.Length >= randomData.Length)
            {
                Assert.AreSame(randomData, result);
            }
        }
    }
}
