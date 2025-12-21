// Copyright (C) 2015-2025 The Neo Project.
//
// Benchmarks.BlockExecution.cs file belongs to the neo project and is free
// software distributed under the MIT software license.

#nullable enable

using BenchmarkDotNet.Attributes;
using Neo.Cryptography;
using Neo.Extensions;
using Neo.IO;
using Neo.Network.P2P.Payloads;
using Neo.SmartContract;
using Neo.VM;
using Neo.Wallets;

namespace Neo.Benchmarks;

/// <summary>
/// Block serialization benchmarks covering 1-100 tx/block scenarios.
/// </summary>
[MemoryDiagnoser]
[RankColumn]
public class BlockSerializationBenchmarks
{
    private Block[] _blocks = null!;
    private byte[][] _serializedBlocks = null!;

    [Params(1, 10, 50, 100)]
    public int TransactionCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _blocks = new Block[4];
        _serializedBlocks = new byte[4][];

        var txCounts = new[] { 1, 10, 50, 100 };
        for (int i = 0; i < txCounts.Length; i++)
        {
            _blocks[i] = CreateBlock(txCounts[i]);
            _serializedBlocks[i] = _blocks[i].ToArray();
        }
    }

    private Block CreateBlock(int txCount)
    {
        var transactions = new Transaction[txCount];
        for (int i = 0; i < txCount; i++)
        {
            transactions[i] = new Transaction
            {
                Version = 0,
                Nonce = (uint)i,
                SystemFee = 1000000,
                NetworkFee = 100000,
                ValidUntilBlock = 1000,
                Signers = new[]
                {
                    new Signer
                    {
                        Account = UInt160.Zero,
                        Scopes = WitnessScope.CalledByEntry
                    }
                },
                Attributes = Array.Empty<TransactionAttribute>(),
                Script = new byte[] { (byte)OpCode.PUSH1, (byte)OpCode.RET },
                Witnesses = new[]
                {
                    new Witness
                    {
                        InvocationScript = new byte[64],
                        VerificationScript = new byte[32]
                    }
                }
            };
        }

        return new Block
        {
            Header = new Header
            {
                Version = 0,
                PrevHash = UInt256.Zero,
                MerkleRoot = MerkleTree.ComputeRoot(transactions.Select(t => t.Hash).ToArray()),
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
            },
            Transactions = transactions
        };
    }

    [Benchmark(Baseline = true)]
    public byte[] Serialize_Block()
    {
        var block = _blocks[Array.IndexOf(new[] { 1, 10, 50, 100 }, TransactionCount)];
        return block.ToArray();
    }

    [Benchmark]
    public Block Deserialize_Block()
    {
        var data = _serializedBlocks[Array.IndexOf(new[] { 1, 10, 50, 100 }, TransactionCount)];
        return data.AsSerializable<Block>();
    }
}

/// <summary>
/// Block hash computation benchmarks.
/// </summary>
[MemoryDiagnoser]
[RankColumn]
public class BlockHashBenchmarks
{
    private Block[] _blocks = null!;

    [Params(1, 10, 50, 100)]
    public int TransactionCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _blocks = new Block[4];
        var txCounts = new[] { 1, 10, 50, 100 };

        for (int i = 0; i < txCounts.Length; i++)
        {
            var transactions = new Transaction[txCounts[i]];
            for (int j = 0; j < txCounts[i]; j++)
            {
                transactions[j] = new Transaction
                {
                    Version = 0,
                    Nonce = (uint)j,
                    SystemFee = 1000000,
                    NetworkFee = 100000,
                    ValidUntilBlock = 1000,
                    Signers = new[]
                    {
                        new Signer
                        {
                            Account = UInt160.Zero,
                            Scopes = WitnessScope.CalledByEntry
                        }
                    },
                    Attributes = Array.Empty<TransactionAttribute>(),
                    Script = new byte[] { (byte)OpCode.PUSH1, (byte)OpCode.RET },
                    Witnesses = new[]
                    {
                        new Witness
                        {
                            InvocationScript = new byte[64],
                            VerificationScript = new byte[32]
                        }
                    }
                };
            }

            _blocks[i] = new Block
            {
                Header = new Header
                {
                    Version = 0,
                    PrevHash = UInt256.Zero,
                    MerkleRoot = MerkleTree.ComputeRoot(transactions.Select(t => t.Hash).ToArray()),
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
                },
                Transactions = transactions
            };
        }
    }

    [Benchmark(Baseline = true)]
    public UInt256 ComputeBlockHash()
    {
        var block = _blocks[Array.IndexOf(new[] { 1, 10, 50, 100 }, TransactionCount)];
        return block.Hash;
    }

    [Benchmark]
    public UInt256 ComputeMerkleRoot()
    {
        var block = _blocks[Array.IndexOf(new[] { 1, 10, 50, 100 }, TransactionCount)];
        var hashes = block.Transactions.Select(t => t.Hash).ToArray();
        return MerkleTree.ComputeRoot(hashes);
    }
}

/// <summary>
/// Block size calculation benchmarks.
/// </summary>
[MemoryDiagnoser]
[RankColumn]
public class BlockSizeBenchmarks
{
    private Block[] _blocks = null!;

    [Params(1, 10, 50, 100)]
    public int TransactionCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _blocks = new Block[4];
        var txCounts = new[] { 1, 10, 50, 100 };

        for (int i = 0; i < txCounts.Length; i++)
        {
            var transactions = new Transaction[txCounts[i]];
            for (int j = 0; j < txCounts[i]; j++)
            {
                transactions[j] = new Transaction
                {
                    Version = 0,
                    Nonce = (uint)j,
                    SystemFee = 1000000,
                    NetworkFee = 100000,
                    ValidUntilBlock = 1000,
                    Signers = new[]
                    {
                        new Signer
                        {
                            Account = UInt160.Zero,
                            Scopes = WitnessScope.CalledByEntry
                        }
                    },
                    Attributes = Array.Empty<TransactionAttribute>(),
                    Script = new byte[] { (byte)OpCode.PUSH1, (byte)OpCode.RET },
                    Witnesses = new[]
                    {
                        new Witness
                        {
                            InvocationScript = new byte[64],
                            VerificationScript = new byte[32]
                        }
                    }
                };
            }

            _blocks[i] = new Block
            {
                Header = new Header
                {
                    Version = 0,
                    PrevHash = UInt256.Zero,
                    MerkleRoot = MerkleTree.ComputeRoot(transactions.Select(t => t.Hash).ToArray()),
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
                },
                Transactions = transactions
            };
        }
    }

    [Benchmark(Baseline = true)]
    public int GetBlockSize()
    {
        var block = _blocks[Array.IndexOf(new[] { 1, 10, 50, 100 }, TransactionCount)];
        return block.Size;
    }

    [Benchmark]
    public int GetHeaderSize()
    {
        var block = _blocks[Array.IndexOf(new[] { 1, 10, 50, 100 }, TransactionCount)];
        return block.Header.Size;
    }
}
