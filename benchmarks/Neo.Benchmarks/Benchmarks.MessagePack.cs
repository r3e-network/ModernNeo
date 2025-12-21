// Copyright (C) 2015-2025 The Neo Project.
//
// Benchmarks.MessagePack.cs file belongs to the neo project and is free
// software distributed under the MIT software license.

#nullable enable

using BenchmarkDotNet.Attributes;
using Neo.Extensions;
using Neo.IO;
using Neo.Network.P2P.Payloads;
using Neo.Serialization.MessagePack;

namespace Neo.Benchmarks;

/// <summary>
/// UInt256 serialization benchmarks comparing MessagePack vs Native.
/// </summary>
[MemoryDiagnoser]
[RankColumn]
public class UInt256SerializationBenchmarks
{
    private UInt256 _uint256 = null!;
    private byte[] _uint256MsgPack = null!;
    private byte[] _uint256Native = null!;

    [GlobalSetup]
    public void Setup()
    {
        _uint256 = UInt256.Parse("0x1234567890abcdef1234567890abcdef1234567890abcdef1234567890abcdef");
        _uint256MsgPack = NeoMessagePackSerializer.Serialize(_uint256);
        _uint256Native = _uint256.ToArray();
    }

    [Benchmark(Baseline = true)]
    public byte[] Native_Serialize() => _uint256.ToArray();

    [Benchmark]
    public byte[] MessagePack_Serialize() => NeoMessagePackSerializer.Serialize(_uint256);

    [Benchmark]
    public UInt256 Native_Deserialize() => new UInt256(_uint256Native);

    [Benchmark]
    public UInt256 MessagePack_Deserialize() => NeoMessagePackSerializer.Deserialize<UInt256>(_uint256MsgPack);
}

/// <summary>
/// UInt160 serialization benchmarks comparing MessagePack vs Native.
/// </summary>
[MemoryDiagnoser]
[RankColumn]
public class UInt160SerializationBenchmarks
{
    private UInt160 _uint160 = null!;
    private byte[] _uint160MsgPack = null!;
    private byte[] _uint160Native = null!;

    [GlobalSetup]
    public void Setup()
    {
        _uint160 = UInt160.Parse("0xabcdef0123456789abcdef0123456789abcdef01");
        _uint160MsgPack = NeoMessagePackSerializer.Serialize(_uint160);
        _uint160Native = _uint160.ToArray();
    }

    [Benchmark(Baseline = true)]
    public byte[] Native_Serialize() => _uint160.ToArray();

    [Benchmark]
    public byte[] MessagePack_Serialize() => NeoMessagePackSerializer.Serialize(_uint160);

    [Benchmark]
    public UInt160 Native_Deserialize() => new UInt160(_uint160Native);

    [Benchmark]
    public UInt160 MessagePack_Deserialize() => NeoMessagePackSerializer.Deserialize<UInt160>(_uint160MsgPack);
}

/// <summary>
/// Witness serialization benchmarks comparing MessagePack vs Native.
/// </summary>
[MemoryDiagnoser]
[RankColumn]
public class WitnessSerializationBenchmarks
{
    private Witness _witness = null!;
    private byte[] _witnessMsgPack = null!;
    private byte[] _witnessNative = null!;

    [GlobalSetup]
    public void Setup()
    {
        _witness = new Witness
        {
            InvocationScript = new byte[64],
            VerificationScript = new byte[32]
        };
        _witnessMsgPack = NeoMessagePackSerializer.Serialize(_witness);
        _witnessNative = _witness.ToArray();
    }

    [Benchmark(Baseline = true)]
    public byte[] Native_Serialize() => _witness.ToArray();

    [Benchmark]
    public byte[] MessagePack_Serialize() => NeoMessagePackSerializer.Serialize(_witness);

    [Benchmark]
    public Witness Native_Deserialize() => _witnessNative.AsSerializable<Witness>();

    [Benchmark]
    public Witness? MessagePack_Deserialize() => NeoMessagePackSerializer.Deserialize<Witness>(_witnessMsgPack);
}

/// <summary>
/// Signer serialization benchmarks comparing MessagePack vs Native.
/// </summary>
[MemoryDiagnoser]
[RankColumn]
public class SignerSerializationBenchmarks
{
    private Signer _signer = null!;
    private byte[] _signerMsgPack = null!;
    private byte[] _signerNative = null!;

    [GlobalSetup]
    public void Setup()
    {
        _signer = new Signer
        {
            Account = UInt160.Parse("0xabcdef0123456789abcdef0123456789abcdef01"),
            Scopes = WitnessScope.CalledByEntry
        };
        _signerMsgPack = NeoMessagePackSerializer.Serialize(_signer);
        _signerNative = _signer.ToArray();
    }

    [Benchmark(Baseline = true)]
    public byte[] Native_Serialize() => _signer.ToArray();

    [Benchmark]
    public byte[] MessagePack_Serialize() => NeoMessagePackSerializer.Serialize(_signer);

    [Benchmark]
    public Signer Native_Deserialize() => _signerNative.AsSerializable<Signer>();

    [Benchmark]
    public Signer? MessagePack_Deserialize() => NeoMessagePackSerializer.Deserialize<Signer>(_signerMsgPack);
}

/// <summary>
/// Header serialization benchmarks comparing MessagePack vs Native.
/// </summary>
[MemoryDiagnoser]
[RankColumn]
public class HeaderSerializationBenchmarks
{
    private Header _header = null!;
    private byte[] _headerMsgPack = null!;
    private byte[] _headerNative = null!;

    [GlobalSetup]
    public void Setup()
    {
        _header = new Header
        {
            Version = 0,
            PrevHash = UInt256.Zero,
            MerkleRoot = UInt256.Zero,
            Timestamp = 1700000000000,
            Nonce = 12345,
            Index = 100,
            PrimaryIndex = 0,
            NextConsensus = UInt160.Zero,
            Witness = new Witness
            {
                InvocationScript = new byte[64],
                VerificationScript = new byte[32]
            }
        };
        _headerMsgPack = NeoMessagePackSerializer.Serialize(_header);
        _headerNative = _header.ToArray();
    }

    [Benchmark(Baseline = true)]
    public byte[] Native_Serialize() => _header.ToArray();

    [Benchmark]
    public byte[] MessagePack_Serialize() => NeoMessagePackSerializer.Serialize(_header);

    [Benchmark]
    public Header Native_Deserialize() => _headerNative.AsSerializable<Header>();

    [Benchmark]
    public Header? MessagePack_Deserialize() => NeoMessagePackSerializer.Deserialize<Header>(_headerMsgPack);
}

/// <summary>
/// Benchmarks for MessagePack compression performance.
/// </summary>
[MemoryDiagnoser]
[RankColumn]
public class MessagePackCompressionBenchmarks
{
    private Witness _largeWitness = null!;
    private byte[] _uncompressedData = null!;
    private byte[] _compressedData = null!;

    [GlobalSetup]
    public void Setup()
    {
        // Create a larger witness for compression benchmarks
        _largeWitness = new Witness
        {
            InvocationScript = new byte[512],
            VerificationScript = new byte[256]
        };

        _uncompressedData = NeoMessagePackSerializer.Serialize(_largeWitness);
        _compressedData = NeoMessagePackSerializer.SerializeCompressed(_largeWitness);
    }

    [Benchmark(Baseline = true)]
    public byte[] Serialize_Uncompressed() => NeoMessagePackSerializer.Serialize(_largeWitness);

    [Benchmark]
    public byte[] Serialize_Compressed() => NeoMessagePackSerializer.SerializeCompressed(_largeWitness);

    [Benchmark]
    public Witness? Deserialize_Uncompressed() => NeoMessagePackSerializer.Deserialize<Witness>(_uncompressedData);

    [Benchmark]
    public Witness? Deserialize_Compressed() => NeoMessagePackSerializer.DeserializeCompressed<Witness>(_compressedData);
}

/// <summary>
/// Benchmarks for batch serialization operations.
/// </summary>
[MemoryDiagnoser]
[RankColumn]
public class MessagePackBatchBenchmarks
{
    private UInt256[] _hashes = null!;
    private const int BatchSize = 100;

    [GlobalSetup]
    public void Setup()
    {
        _hashes = new UInt256[BatchSize];
        for (int i = 0; i < BatchSize; i++)
        {
            var bytes = new byte[32];
            bytes[0] = (byte)i;
            bytes[1] = (byte)(i >> 8);
            _hashes[i] = new UInt256(bytes);
        }
    }

    [Benchmark(Baseline = true)]
    public byte[][] Batch_Native_Serialize()
    {
        var results = new byte[BatchSize][];
        for (int i = 0; i < BatchSize; i++)
        {
            results[i] = _hashes[i].ToArray();
        }
        return results;
    }

    [Benchmark]
    public byte[][] Batch_MessagePack_Serialize()
    {
        var results = new byte[BatchSize][];
        for (int i = 0; i < BatchSize; i++)
        {
            results[i] = NeoMessagePackSerializer.Serialize(_hashes[i]);
        }
        return results;
    }

    [Benchmark]
    public byte[] Batch_MessagePack_SerializeArray() => NeoMessagePackSerializer.Serialize(_hashes);
}
