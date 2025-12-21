// Copyright (C) 2015-2025 The Neo Project.
//
// Benchmarks.BlockchainGrain.cs file belongs to the neo project and is free
// software distributed under the MIT software license.

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Order;
using Neo.Core.Interfaces;
using Neo.IO;
using Neo.Orleans.Grains;
using Neo.Orleans.Interfaces;
using Neo.Orleans.Services;
using Neo.Orleans.States;
using Moq;
using Orleans.Runtime;

namespace Neo.Orleans.Benchmarks;

/// <summary>
/// Benchmarks for BlockchainGrain operations.
/// Measures block persistence, retrieval, and state query performance.
/// </summary>
[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[RankColumn]
public class BlockchainGrainBenchmarks
{
    private BlockchainGrain _grain = null!;
    private InMemoryBlockStorageService _storage = null!;
    private Mock<IPersistentState<BlockchainState>> _stateMock = null!;
    private BlockchainState _state = null!;
    private BenchmarkBlockData[] _blocks = null!;

    [Params(100, 1000, 10000)]
    public int BlockCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _storage = new InMemoryBlockStorageService();
        _state = new BlockchainState();
        _stateMock = new Mock<IPersistentState<BlockchainState>>();
        _stateMock.Setup(x => x.State).Returns(_state);
        _stateMock.Setup(x => x.WriteStateAsync()).Returns(Task.CompletedTask);

        _grain = new BlockchainGrain(_stateMock.Object, _storage);

        // Pre-generate blocks for benchmarking
        _blocks = new BenchmarkBlockData[BlockCount];
        for (int i = 0; i < BlockCount; i++)
        {
            _blocks[i] = new BenchmarkBlockData((uint)i, (ulong)(i * 1000));
        }
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _storage = null!;
        _grain = null!;
    }

    [Benchmark(Description = "PersistBlock - Sequential")]
    public async Task PersistBlockSequential()
    {
        // Reset state for each iteration
        _state.Height = 0;
        _state.IsInitialized = false;
        _storage = new InMemoryBlockStorageService();
        _grain = new BlockchainGrain(_stateMock.Object, _storage);

        for (int i = 0; i < BlockCount; i++)
        {
            await _grain.PersistBlockAsync(_blocks[i]);
        }
    }

    [Benchmark(Description = "GetHeight - After Persist")]
    public async Task<uint> GetHeightAfterPersist()
    {
        return await _grain.GetHeightAsync();
    }

    [Benchmark(Description = "GetBlockByIndex - Random Access")]
    public async Task GetBlockByIndexRandom()
    {
        // Setup: persist blocks first
        _state.Height = 0;
        _state.IsInitialized = false;
        _storage = new InMemoryBlockStorageService();
        _grain = new BlockchainGrain(_stateMock.Object, _storage);

        for (int i = 0; i < Math.Min(100, BlockCount); i++)
        {
            await _grain.PersistBlockAsync(_blocks[i]);
        }

        // Benchmark: random access
        var random = new Random(42);
        for (int i = 0; i < 100; i++)
        {
            var index = (uint)random.Next(0, Math.Min(100, BlockCount));
            await _grain.GetBlockByIndexAsync(index);
        }
    }

    [Benchmark(Description = "ImportBlocks - Batch")]
    public async Task<int> ImportBlocksBatch()
    {
        _state.Height = 0;
        _state.IsInitialized = false;
        _storage = new InMemoryBlockStorageService();
        _grain = new BlockchainGrain(_stateMock.Object, _storage);

        return await _grain.ImportBlocksAsync(_blocks.Take(Math.Min(100, BlockCount)));
    }
}

/// <summary>
/// Lightweight IBlockData implementation for benchmarking.
/// </summary>
internal class BenchmarkBlockData : IBlockData
{
    private readonly byte[] _hashBytes;

    public BenchmarkBlockData(uint index, ulong timestamp)
    {
        Index = index;
        Timestamp = timestamp;
        _hashBytes = new byte[32];
        BitConverter.GetBytes(index).CopyTo(_hashBytes, 0);
        BitConverter.GetBytes(timestamp).CopyTo(_hashBytes, 4);
        Hash = new UInt256(_hashBytes);
        PrevHash = index > 0 ? CreatePrevHash(index - 1) : UInt256.Zero;
        MerkleRoot = UInt256.Zero;
        NextConsensus = UInt160.Zero;
    }

    private static UInt256 CreatePrevHash(uint prevIndex)
    {
        var bytes = new byte[32];
        BitConverter.GetBytes(prevIndex).CopyTo(bytes, 0);
        BitConverter.GetBytes((ulong)(prevIndex * 1000)).CopyTo(bytes, 4);
        return new UInt256(bytes);
    }

    public UInt256 Hash { get; }
    public uint Version => 0;
    public UInt256 PrevHash { get; }
    public UInt256 MerkleRoot { get; }
    public ulong Timestamp { get; }
    public ulong Nonce => 0;
    public uint Index { get; }
    public byte PrimaryIndex => 0;
    public UInt160 NextConsensus { get; }
    public int TransactionsCount => 0;
    public int Size => 141;

    public void Deserialize(ref MemoryReader reader) { }
    public void DeserializeUnsigned(ref MemoryReader reader) { }
    public void Serialize(BinaryWriter writer) { }
    public void SerializeUnsigned(BinaryWriter writer) { }
}
