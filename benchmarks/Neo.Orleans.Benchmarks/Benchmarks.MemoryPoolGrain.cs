// Copyright (C) 2015-2025 The Neo Project.
//
// Benchmarks.MemoryPoolGrain.cs file belongs to the neo project and is free
// software distributed under the MIT software license.

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Order;
using Neo.Core.Interfaces;
using Neo.IO;
using Neo.Orleans.Grains;
using Neo.Orleans.States;
using Moq;
using Orleans.Runtime;

namespace Neo.Orleans.Benchmarks;

/// <summary>
/// Benchmarks for MemoryPoolGrain operations.
/// Measures transaction add, remove, and query performance.
/// </summary>
[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[RankColumn]
public class MemoryPoolGrainBenchmarks
{
    private MemoryPoolGrain _grain = null!;
    private Mock<IPersistentState<MemoryPoolState>> _stateMock = null!;
    private MemoryPoolState _state = null!;
    private BenchmarkTransactionData[] _transactions = null!;

    [Params(100, 1000, 5000)]
    public int TransactionCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _state = new MemoryPoolState();
        _stateMock = new Mock<IPersistentState<MemoryPoolState>>();
        _stateMock.Setup(x => x.State).Returns(_state);
        _stateMock.Setup(x => x.WriteStateAsync()).Returns(Task.CompletedTask);

        _grain = new MemoryPoolGrain(_stateMock.Object);

        // Pre-generate transactions for benchmarking
        _transactions = new BenchmarkTransactionData[TransactionCount];
        for (int i = 0; i < TransactionCount; i++)
        {
            _transactions[i] = new BenchmarkTransactionData(i);
        }
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _grain = null!;
    }

    [Benchmark(Description = "AddTransaction - Sequential")]
    public async Task AddTransactionSequential()
    {
        // Reset state
        _state = new MemoryPoolState();
        _stateMock.Setup(x => x.State).Returns(_state);
        _grain = new MemoryPoolGrain(_stateMock.Object);

        for (int i = 0; i < TransactionCount; i++)
        {
            await _grain.AddTransactionAsync(_transactions[i]);
        }
    }

    [Benchmark(Description = "GetCount - After Add")]
    public async Task<int> GetCountAfterAdd()
    {
        return await _grain.GetCountAsync();
    }

    [Benchmark(Description = "ContainsTransaction - Existing")]
    public async Task ContainsTransactionExisting()
    {
        // Setup: add transactions first
        _state = new MemoryPoolState();
        _stateMock.Setup(x => x.State).Returns(_state);
        _grain = new MemoryPoolGrain(_stateMock.Object);

        var count = Math.Min(100, TransactionCount);
        for (int i = 0; i < count; i++)
        {
            await _grain.AddTransactionAsync(_transactions[i]);
        }

        // Benchmark: check existence
        for (int i = 0; i < count; i++)
        {
            await _grain.ContainsAsync(_transactions[i].Hash.GetSpan().ToArray());
        }
    }

    [Benchmark(Description = "RemoveTransaction - Sequential")]
    public async Task RemoveTransactionSequential()
    {
        // Setup: add transactions first
        _state = new MemoryPoolState();
        _stateMock.Setup(x => x.State).Returns(_state);
        _grain = new MemoryPoolGrain(_stateMock.Object);

        var count = Math.Min(100, TransactionCount);
        for (int i = 0; i < count; i++)
        {
            await _grain.AddTransactionAsync(_transactions[i]);
        }

        // Benchmark: remove all
        for (int i = 0; i < count; i++)
        {
            await _grain.RemoveTransactionAsync(_transactions[i].Hash.GetSpan().ToArray());
        }
    }

    [Benchmark(Description = "GetTransactions - Batch")]
    public async Task<IEnumerable<ITransactionData>> GetTransactionsBatch()
    {
        // Setup: add transactions first
        _state = new MemoryPoolState();
        _stateMock.Setup(x => x.State).Returns(_state);
        _grain = new MemoryPoolGrain(_stateMock.Object);

        var count = Math.Min(100, TransactionCount);
        for (int i = 0; i < count; i++)
        {
            await _grain.AddTransactionAsync(_transactions[i]);
        }

        // Benchmark: get all
        return await _grain.GetVerifiedTransactionsAsync(count);
    }
}

/// <summary>
/// Lightweight ITransactionData implementation for benchmarking.
/// </summary>
internal class BenchmarkTransactionData : ITransactionData
{
    private readonly byte[] _hashBytes;
    private static readonly byte[] EmptyScript = [];

    public BenchmarkTransactionData(int seed)
    {
        _hashBytes = new byte[32];
        BitConverter.GetBytes(seed).CopyTo(_hashBytes, 0);
        BitConverter.GetBytes(DateTime.UtcNow.Ticks).CopyTo(_hashBytes, 4);
        Hash = new UInt256(_hashBytes);
        Sender = UInt160.Zero;
    }

    public UInt256 Hash { get; }
    public byte Version => 0;
    public uint Nonce => 0;
    public long SystemFee => 1000000;
    public long NetworkFee => 100000;
    public uint ValidUntilBlock => 1000;
    public ReadOnlyMemory<byte> Script => EmptyScript;
    public UInt160 Sender { get; }
    public long FeePerByte => NetworkFee / Size;
    public int SignersCount => 1;
    public int AttributesCount => 0;
    public int Size => 250;

    public void Deserialize(ref MemoryReader reader) { }
    public void DeserializeUnsigned(ref MemoryReader reader) { }
    public void Serialize(BinaryWriter writer) { }
    public void SerializeUnsigned(BinaryWriter writer) { }
}
