#nullable enable
// Copyright (C) 2015-2025 The Neo Project.
//
// Benchmarks.NativeAOT.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Reports;
using Neo.Cryptography;
using Neo.Extensions;
using Neo.IO;
using Neo.Json;
using Neo.Persistence;
using Neo.Persistence.Providers;
using System.Text;

namespace Neo.Benchmark
{
    /// <summary>
    /// Benchmarks for operations that are critical for NativeAOT performance.
    /// These benchmarks measure core operations that must work efficiently
    /// in both JIT and AOT compilation modes.
    /// </summary>
    [MemoryDiagnoser]
    [RankColumn]
    public class Benchmarks_NativeAOT
    {
        // Test data
        private static readonly byte[] SmallData = Encoding.UTF8.GetBytes("Hello Neo");
        private static readonly byte[] MediumData = new byte[1024];
        private static readonly byte[] LargeData = new byte[1024 * 1024]; // 1MB

        private MemoryStore _store = null!;
        private byte[][] _keys = null!;
        private byte[][] _values = null!;

        [GlobalSetup]
        public void Setup()
        {
            // Initialize test data
            var random = new Random(42);
            random.NextBytes(MediumData);
            random.NextBytes(LargeData);

            // Setup storage test data
            _store = new MemoryStore();
            _keys = new byte[1000][];
            _values = new byte[1000][];

            for (int i = 0; i < 1000; i++)
            {
                _keys[i] = BitConverter.GetBytes(i);
                _values[i] = new byte[64];
                random.NextBytes(_values[i]);
                _store.Put(_keys[i], _values[i]);
            }
        }

        [GlobalCleanup]
        public void Cleanup()
        {
            _store?.Dispose();
        }

        // ============================================
        // Cryptography Benchmarks
        // ============================================

        [Benchmark]
        [BenchmarkCategory("Cryptography")]
        public byte[] Crypto_SHA256_Small()
        {
            return SmallData.Sha256();
        }

        [Benchmark]
        [BenchmarkCategory("Cryptography")]
        public byte[] Crypto_SHA256_Medium()
        {
            return MediumData.Sha256();
        }

        [Benchmark]
        [BenchmarkCategory("Cryptography")]
        public byte[] Crypto_SHA256_Large()
        {
            return LargeData.Sha256();
        }

        [Benchmark]
        [BenchmarkCategory("Cryptography")]
        public byte[] Crypto_RIPEMD160()
        {
            return SmallData.RIPEMD160();
        }

        [Benchmark]
        [BenchmarkCategory("Cryptography")]
        public byte[] Crypto_Hash256()
        {
            return Crypto.Hash256(SmallData);
        }

        [Benchmark]
        [BenchmarkCategory("Cryptography")]
        public byte[] Crypto_Hash160()
        {
            return Crypto.Hash160(SmallData);
        }

        [Benchmark]
        [BenchmarkCategory("Cryptography")]
        public uint Crypto_Murmur32()
        {
            return MediumData.Murmur32(0);
        }

        // ============================================
        // JSON Benchmarks
        // ============================================

        private static readonly string JsonString = """{"name":"Neo","version":3,"active":true,"data":[1,2,3,4,5]}""";
        private static readonly JObject JsonObject = new()
        {
            ["name"] = "Neo",
            ["version"] = 3,
            ["active"] = true,
            ["data"] = new JArray { 1, 2, 3, 4, 5 }
        };

        [Benchmark]
        [BenchmarkCategory("JSON")]
        public JToken? JSON_Parse()
        {
            return JToken.Parse(JsonString);
        }

        [Benchmark]
        [BenchmarkCategory("JSON")]
        public string JSON_Serialize()
        {
            return JsonObject.ToString();
        }

        [Benchmark]
        [BenchmarkCategory("JSON")]
        public JToken? JSON_ParseAndAccess()
        {
            var parsed = (JObject)JToken.Parse(JsonString)!;
            _ = parsed["name"]!.AsString();
            _ = parsed["version"]!.AsNumber();
            return parsed;
        }

        // ============================================
        // Storage Benchmarks
        // ============================================

        [Benchmark]
        [BenchmarkCategory("Storage")]
        public void Storage_Put()
        {
            using var store = new MemoryStore();
            for (int i = 0; i < 100; i++)
            {
                store.Put(BitConverter.GetBytes(i), _values[i % _values.Length]);
            }
        }

        [Benchmark]
        [BenchmarkCategory("Storage")]
        public byte[]? Storage_TryGet()
        {
            byte[]? result = null;
            for (int i = 0; i < 100; i++)
            {
                _store.TryGet(_keys[i], out result);
            }
            return result;
        }

        [Benchmark]
        [BenchmarkCategory("Storage")]
        public int Storage_Find()
        {
            var count = 0;
            foreach (var _ in _store.Find(null, SeekDirection.Forward))
            {
                count++;
                if (count >= 100) break;
            }
            return count;
        }

        [Benchmark]
        [BenchmarkCategory("Storage")]
        public void Storage_Snapshot()
        {
            using var snapshot = _store.GetSnapshot();
            snapshot.Put(new byte[] { 0xFF, 0xFF }, new byte[] { 0x01 });
            snapshot.Commit();
        }

        // ============================================
        // Core Types Benchmarks
        // ============================================

        private static readonly string UInt160String = "0x0000000000000000000000000000000000000001";
        private static readonly string UInt256String = "0x0000000000000000000000000000000000000000000000000000000000000001";

        [Benchmark]
        [BenchmarkCategory("CoreTypes")]
        public UInt160 CoreTypes_UInt160_Parse()
        {
            return UInt160.Parse(UInt160String);
        }

        [Benchmark]
        [BenchmarkCategory("CoreTypes")]
        public string CoreTypes_UInt160_ToString()
        {
            return UInt160.Zero.ToString();
        }

        [Benchmark]
        [BenchmarkCategory("CoreTypes")]
        public UInt256 CoreTypes_UInt256_Parse()
        {
            return UInt256.Parse(UInt256String);
        }

        [Benchmark]
        [BenchmarkCategory("CoreTypes")]
        public string CoreTypes_UInt256_ToString()
        {
            return UInt256.Zero.ToString();
        }

        [Benchmark]
        [BenchmarkCategory("CoreTypes")]
        public int CoreTypes_UInt256_Compare()
        {
            var a = UInt256.Zero;
            var b = UInt256.Parse(UInt256String);
            return a.CompareTo(b);
        }

        // ============================================
        // IO Benchmarks
        // ============================================

        [Benchmark]
        [BenchmarkCategory("IO")]
        public byte[] IO_VarInt_Write()
        {
            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms);
            writer.WriteVarInt(12345678L);
            return ms.ToArray();
        }

        [Benchmark]
        [BenchmarkCategory("IO")]
        public ulong IO_VarInt_Read()
        {
            var data = new byte[] { 0xFE, 0x4E, 0x61, 0xBC, 0x00 };
            using var ms = new MemoryStream(data);
            using var reader = new BinaryReader(ms);
            return reader.ReadVarInt();
        }

        [Benchmark]
        [BenchmarkCategory("IO")]
        public string IO_HexConvert()
        {
            return Convert.ToHexString(MediumData);
        }

        [Benchmark]
        [BenchmarkCategory("IO")]
        public byte[] IO_HexParse()
        {
            return Convert.FromHexString("DEADBEEFCAFEBABE");
        }
    }

    /// <summary>
    /// Startup time comparison benchmark.
    /// This measures the overhead of different initialization patterns.
    /// </summary>
    [MemoryDiagnoser]
    public class Benchmarks_NativeAOT_Startup
    {
        [Benchmark(Baseline = true)]
        [BenchmarkCategory("Startup")]
        public MemoryStore Startup_CreateMemoryStore()
        {
            var store = new MemoryStore();
            return store;
        }

        [Benchmark]
        [BenchmarkCategory("Startup")]
        public (MemoryStore, IStoreSnapshot) Startup_CreateStoreAndSnapshot()
        {
            var store = new MemoryStore();
            var snapshot = store.GetSnapshot();
            return (store, snapshot);
        }

        [Benchmark]
        [BenchmarkCategory("Startup")]
        public JObject Startup_CreateJsonObject()
        {
            return new JObject
            {
                ["name"] = "Neo",
                ["version"] = 3,
                ["network"] = 860833102
            };
        }
    }

    /// <summary>
    /// Memory allocation benchmarks for AOT optimization validation.
    /// </summary>
    [MemoryDiagnoser]
    public class Benchmarks_NativeAOT_Memory
    {
        private static readonly byte[] TestData = new byte[1024];

        [GlobalSetup]
        public void Setup()
        {
            new Random(42).NextBytes(TestData);
        }

        [Benchmark]
        [BenchmarkCategory("Memory")]
        public byte[] Memory_ArrayCopy()
        {
            var copy = new byte[TestData.Length];
            Array.Copy(TestData, copy, TestData.Length);
            return copy;
        }

        [Benchmark]
        [BenchmarkCategory("Memory")]
        public byte[] Memory_SpanCopy()
        {
            var copy = new byte[TestData.Length];
            TestData.AsSpan().CopyTo(copy);
            return copy;
        }

        [Benchmark]
        [BenchmarkCategory("Memory")]
        public byte[] Memory_SliceOperator()
        {
            return TestData[..];
        }

        [Benchmark]
        [BenchmarkCategory("Memory")]
        public ReadOnlyMemory<byte> Memory_AsMemory()
        {
            return TestData.AsMemory();
        }
    }
}
