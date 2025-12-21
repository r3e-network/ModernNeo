// Copyright (C) 2015-2025 The Neo Project.
//
// Benchmarks.BlockStorage.cs file belongs to the neo project and is free
// software distributed under the MIT software license.

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Order;
using Neo.Core.Interfaces;
using Neo.IO;
using Neo.Orleans.Services;
using Neo.Persistence.Providers;

namespace Neo.Orleans.Benchmarks;

/// <summary>
/// Benchmarks comparing InMemoryBlockStorageService vs StoreBasedBlockStorageService.
/// Measures storage performance across different backends.
/// </summary>
[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[RankColumn]
public class BlockStorageBenchmarks
{
    private InMemoryBlockStorageService _inMemoryStorage = null!;
    private StoreBasedBlockStorageService _storeBasedStorage = null!;
    private MemoryStore _memoryStore = null!;
    private BenchmarkBlockData[] _blocks = null!;

    [Params(100, 1000, 5000)]
    public int BlockCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _inMemoryStorage = new InMemoryBlockStorageService();
        _memoryStore = new MemoryStore();
        _storeBasedStorage = new StoreBasedBlockStorageService(_memoryStore, ownsStore: false);

        // Pre-generate blocks
        _blocks = new BenchmarkBlockData[BlockCount];
        for (int i = 0; i < BlockCount; i++)
        {
            _blocks[i] = new BenchmarkBlockData((uint)i, (ulong)(i * 1000));
        }
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _storeBasedStorage?.Dispose();
        _memoryStore?.Dispose();
    }

    [Benchmark(Baseline = true, Description = "InMemory - Store Sequential")]
    public async Task InMemoryStoreSequential()
    {
        var storage = new InMemoryBlockStorageService();
        for (int i = 0; i < BlockCount; i++)
        {
            await storage.StoreBlockAsync(_blocks[i]);
        }
    }

    [Benchmark(Description = "StoreBased - Store Sequential")]
    public async Task StoreBasedStoreSequential()
    {
        using var store = new MemoryStore();
        using var storage = new StoreBasedBlockStorageService(store, ownsStore: false);
        for (int i = 0; i < BlockCount; i++)
        {
            await storage.StoreBlockAsync(_blocks[i]);
        }
    }

    [Benchmark(Description = "InMemory - GetByIndex Random")]
    public async Task InMemoryGetByIndexRandom()
    {
        // Setup
        var storage = new InMemoryBlockStorageService();
        var count = Math.Min(100, BlockCount);
        for (int i = 0; i < count; i++)
        {
            await storage.StoreBlockAsync(_blocks[i]);
        }

        // Benchmark
        var random = new Random(42);
        for (int i = 0; i < 100; i++)
        {
            await storage.GetBlockByIndexAsync((uint)random.Next(0, count));
        }
    }

    [Benchmark(Description = "StoreBased - GetByIndex Random")]
    public async Task StoreBasedGetByIndexRandom()
    {
        // Setup
        using var store = new MemoryStore();
        using var storage = new StoreBasedBlockStorageService(store, ownsStore: false);
        var count = Math.Min(100, BlockCount);
        for (int i = 0; i < count; i++)
        {
            await storage.StoreBlockAsync(_blocks[i]);
        }

        // Benchmark
        var random = new Random(42);
        for (int i = 0; i < 100; i++)
        {
            await storage.GetBlockByIndexAsync((uint)random.Next(0, count));
        }
    }

    [Benchmark(Description = "InMemory - GetByHash")]
    public async Task InMemoryGetByHash()
    {
        // Setup
        var storage = new InMemoryBlockStorageService();
        var count = Math.Min(100, BlockCount);
        for (int i = 0; i < count; i++)
        {
            await storage.StoreBlockAsync(_blocks[i]);
        }

        // Benchmark
        for (int i = 0; i < count; i++)
        {
            await storage.GetBlockByHashAsync(_blocks[i].Hash.GetSpan().ToArray());
        }
    }

    [Benchmark(Description = "StoreBased - GetByHash")]
    public async Task StoreBasedGetByHash()
    {
        // Setup
        using var store = new MemoryStore();
        using var storage = new StoreBasedBlockStorageService(store, ownsStore: false);
        var count = Math.Min(100, BlockCount);
        for (int i = 0; i < count; i++)
        {
            await storage.StoreBlockAsync(_blocks[i]);
        }

        // Benchmark
        for (int i = 0; i < count; i++)
        {
            await storage.GetBlockByHashAsync(_blocks[i].Hash.GetSpan().ToArray());
        }
    }

    [Benchmark(Description = "InMemory - ContainsBlock")]
    public async Task InMemoryContainsBlock()
    {
        // Setup
        var storage = new InMemoryBlockStorageService();
        var count = Math.Min(100, BlockCount);
        for (int i = 0; i < count; i++)
        {
            await storage.StoreBlockAsync(_blocks[i]);
        }

        // Benchmark
        for (int i = 0; i < count; i++)
        {
            await storage.ContainsBlockAsync(_blocks[i].Hash.GetSpan().ToArray());
        }
    }

    [Benchmark(Description = "StoreBased - ContainsBlock")]
    public async Task StoreBasedContainsBlock()
    {
        // Setup
        using var store = new MemoryStore();
        using var storage = new StoreBasedBlockStorageService(store, ownsStore: false);
        var count = Math.Min(100, BlockCount);
        for (int i = 0; i < count; i++)
        {
            await storage.StoreBlockAsync(_blocks[i]);
        }

        // Benchmark
        for (int i = 0; i < count; i++)
        {
            await storage.ContainsBlockAsync(_blocks[i].Hash.GetSpan().ToArray());
        }
    }
}
