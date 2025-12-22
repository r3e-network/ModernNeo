// Copyright (C) 2015-2025 The Neo Project.
//
// Benchmarks.Consensus.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

#nullable enable

using BenchmarkDotNet.Attributes;
using Neo.Cryptography;
using Neo.Extensions;
using Neo.IO;
using Neo.Network.P2P.Payloads;

namespace Neo.Benchmarks
{
    /// <summary>
    /// Consensus-related payload serialization benchmarks.
    /// </summary>
    [MemoryDiagnoser]
    [RankColumn]
    public class ConsensusPayloadBenchmarks
    {
        private ExtensiblePayload _payload = null!;
        private byte[] _payloadBytes = null!;

        [GlobalSetup]
        public void Setup()
        {
            _payload = new ExtensiblePayload
            {
                Category = "dBFT",
                ValidBlockStart = 0,
                ValidBlockEnd = 1000000,
                Sender = UInt160.Zero,
                Data = new byte[256],
                Witness = new Witness
                {
                    InvocationScript = new byte[64],
                    VerificationScript = new byte[32]
                }
            };
            _payloadBytes = _payload.ToArray();
        }

        [Benchmark(Baseline = true)]
        public byte[] ExtensiblePayload_Serialize() => _payload.ToArray();

        [Benchmark]
        public ExtensiblePayload ExtensiblePayload_Deserialize() => _payloadBytes.AsSerializable<ExtensiblePayload>();

        [Benchmark]
        public UInt256 ExtensiblePayload_GetHash() => _payload.Hash;
    }

    /// <summary>
    /// Consensus message size variation benchmarks.
    /// </summary>
    [MemoryDiagnoser]
    [RankColumn]
    public class ConsensusMessageSizeBenchmarks
    {
        private ExtensiblePayload _smallPayload = null!;
        private ExtensiblePayload _mediumPayload = null!;
        private ExtensiblePayload _largePayload = null!;

        private byte[] _smallPayloadBytes = null!;
        private byte[] _mediumPayloadBytes = null!;
        private byte[] _largePayloadBytes = null!;

        [GlobalSetup]
        public void Setup()
        {
            _smallPayload = CreatePayload(64);      // PrepareResponse-like
            _mediumPayload = CreatePayload(512);    // PrepareRequest-like
            _largePayload = CreatePayload(4096);    // RecoveryMessage-like

            _smallPayloadBytes = _smallPayload.ToArray();
            _mediumPayloadBytes = _mediumPayload.ToArray();
            _largePayloadBytes = _largePayload.ToArray();
        }

        private static ExtensiblePayload CreatePayload(int dataSize)
        {
            return new ExtensiblePayload
            {
                Category = "dBFT",
                ValidBlockStart = 0,
                ValidBlockEnd = 1000000,
                Sender = UInt160.Zero,
                Data = new byte[dataSize],
                Witness = new Witness
                {
                    InvocationScript = new byte[64],
                    VerificationScript = new byte[32]
                }
            };
        }

        [Benchmark(Baseline = true)]
        public byte[] SmallPayload_Serialize() => _smallPayload.ToArray();

        [Benchmark]
        public byte[] MediumPayload_Serialize() => _mediumPayload.ToArray();

        [Benchmark]
        public byte[] LargePayload_Serialize() => _largePayload.ToArray();

        [Benchmark]
        public ExtensiblePayload SmallPayload_Deserialize() => _smallPayloadBytes.AsSerializable<ExtensiblePayload>();

        [Benchmark]
        public ExtensiblePayload MediumPayload_Deserialize() => _mediumPayloadBytes.AsSerializable<ExtensiblePayload>();

        [Benchmark]
        public ExtensiblePayload LargePayload_Deserialize() => _largePayloadBytes.AsSerializable<ExtensiblePayload>();
    }

    /// <summary>
    /// Consensus hash computation benchmarks.
    /// </summary>
    [MemoryDiagnoser]
    [RankColumn]
    public class ConsensusHashBenchmarks
    {
        private byte[] _smallData = null!;
        private byte[] _mediumData = null!;
        private byte[] _largeData = null!;

        [GlobalSetup]
        public void Setup()
        {
            _smallData = new byte[64];
            _mediumData = new byte[512];
            _largeData = new byte[4096];

            // Fill with some data
            for (int i = 0; i < _smallData.Length; i++)
                _smallData[i] = (byte)i;
            for (int i = 0; i < _mediumData.Length; i++)
                _mediumData[i] = (byte)i;
            for (int i = 0; i < _largeData.Length; i++)
                _largeData[i] = (byte)i;
        }

        [Benchmark(Baseline = true)]
        public byte[] Sha256_Small() => _smallData.Sha256();

        [Benchmark]
        public byte[] Sha256_Medium() => _mediumData.Sha256();

        [Benchmark]
        public byte[] Sha256_Large() => _largeData.Sha256();

        [Benchmark]
        public byte[] Hash256_Small() => Crypto.Hash256(_smallData.AsSpan());

        [Benchmark]
        public byte[] Hash256_Medium() => Crypto.Hash256(_mediumData.AsSpan());

        [Benchmark]
        public byte[] Hash256_Large() => Crypto.Hash256(_largeData.AsSpan());

        [Benchmark]
        public byte[] Hash160_Small() => Crypto.Hash160(_smallData.AsSpan());

        [Benchmark]
        public byte[] Hash160_Medium() => Crypto.Hash160(_mediumData.AsSpan());

        [Benchmark]
        public byte[] Hash160_Large() => Crypto.Hash160(_largeData.AsSpan());
    }

    /// <summary>
    /// Parameterized consensus payload benchmarks.
    /// </summary>
    [MemoryDiagnoser]
    [RankColumn]
    public class ParameterizedConsensusBenchmarks
    {
        [Params(64, 256, 1024, 4096)]
        public int DataSize { get; set; }

        private ExtensiblePayload _payload = null!;
        private byte[] _payloadBytes = null!;

        [GlobalSetup]
        public void Setup()
        {
            _payload = new ExtensiblePayload
            {
                Category = "dBFT",
                ValidBlockStart = 0,
                ValidBlockEnd = 1000000,
                Sender = UInt160.Zero,
                Data = new byte[DataSize],
                Witness = new Witness
                {
                    InvocationScript = new byte[64],
                    VerificationScript = new byte[32]
                }
            };
            _payloadBytes = _payload.ToArray();
        }

        [Benchmark(Baseline = true)]
        public byte[] Serialize() => _payload.ToArray();

        [Benchmark]
        public ExtensiblePayload Deserialize() => _payloadBytes.AsSerializable<ExtensiblePayload>();

        [Benchmark]
        public int GetSize() => _payload.Size;

        [Benchmark]
        public UInt256 GetHash() => _payload.Hash;
    }
}
