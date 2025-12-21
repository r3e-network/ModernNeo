using Neo;
using Neo.Core.Interfaces;
using Neo.IO;
using Neo.Orleans.Grains;
using Neo.Orleans.Interfaces;
using Neo.Orleans.States;
using Orleans.Runtime;
using Orleans.TestingHost;

namespace Neo.Orleans.Tests.Grains;

/// <summary>
/// Unit tests for BlockchainGrain.
/// </summary>
[TestClass]
public class BlockchainGrainTests
{
    private TestCluster? _cluster;

    [TestInitialize]
    public async Task Setup()
    {
        var builder = new TestClusterBuilder();
        builder.AddSiloBuilderConfigurator<TestSiloConfigurator>();
        _cluster = builder.Build();
        await _cluster.DeployAsync();
    }

    [TestCleanup]
    public async Task Cleanup()
    {
        if (_cluster != null)
        {
            await _cluster.StopAllSilosAsync();
            await _cluster.DisposeAsync();
        }
    }

    [TestMethod]
    public async Task GetHeightAsync_InitialState_ReturnsZero()
    {
        // Arrange
        var grain = _cluster!.GrainFactory.GetGrain<IBlockchainGrain>(0);

        // Act
        var height = await grain.GetHeightAsync();

        // Assert
        Assert.AreEqual(0u, height);
    }

    [TestMethod]
    public async Task PersistBlockAsync_FirstBlock_Succeeds()
    {
        // Arrange
        var grain = _cluster!.GrainFactory.GetGrain<IBlockchainGrain>(0);
        var block = new MockBlockData(index: 1, timestamp: 1000);

        // Act
        var result = await grain.PersistBlockAsync(block);

        // Assert
        Assert.AreEqual(BlockVerifyResult.Succeed, result);
        Assert.AreEqual(1u, await grain.GetHeightAsync());
    }

    [TestMethod]
    public async Task PersistBlockAsync_SequentialBlocks_Succeeds()
    {
        // Arrange
        var grain = _cluster!.GrainFactory.GetGrain<IBlockchainGrain>(0);
        var block1 = new MockBlockData(index: 1, timestamp: 1000);
        var block2 = new MockBlockData(index: 2, timestamp: 2000);
        var block3 = new MockBlockData(index: 3, timestamp: 3000);

        // Act
        var result1 = await grain.PersistBlockAsync(block1);
        var result2 = await grain.PersistBlockAsync(block2);
        var result3 = await grain.PersistBlockAsync(block3);

        // Assert
        Assert.AreEqual(BlockVerifyResult.Succeed, result1);
        Assert.AreEqual(BlockVerifyResult.Succeed, result2);
        Assert.AreEqual(BlockVerifyResult.Succeed, result3);
        Assert.AreEqual(3u, await grain.GetHeightAsync());
    }

    [TestMethod]
    public async Task PersistBlockAsync_NonSequentialBlock_Fails()
    {
        // Arrange
        var grain = _cluster!.GrainFactory.GetGrain<IBlockchainGrain>(0);
        var block1 = new MockBlockData(index: 1, timestamp: 1000);
        var block3 = new MockBlockData(index: 3, timestamp: 3000); // Skip block 2

        // Act
        await grain.PersistBlockAsync(block1);
        var result = await grain.PersistBlockAsync(block3);

        // Assert
        Assert.AreEqual(BlockVerifyResult.UnableToVerify, result);
        Assert.AreEqual(1u, await grain.GetHeightAsync());
    }

    [TestMethod]
    public async Task PersistBlockAsync_DuplicateBlock_Fails()
    {
        // Arrange
        var grain = _cluster!.GrainFactory.GetGrain<IBlockchainGrain>(0);
        var block1 = new MockBlockData(index: 1, timestamp: 1000);
        var block1Dup = new MockBlockData(index: 1, timestamp: 1001);

        // Act
        await grain.PersistBlockAsync(block1);
        var result = await grain.PersistBlockAsync(block1Dup);

        // Assert
        Assert.AreEqual(BlockVerifyResult.AlreadyExists, result);
    }

    [TestMethod]
    public async Task ImportBlocksAsync_OrderedBlocks_ImportsAll()
    {
        // Arrange
        var grain = _cluster!.GrainFactory.GetGrain<IBlockchainGrain>(0);
        var blocks = new[]
        {
            new MockBlockData(index: 1, timestamp: 1000),
            new MockBlockData(index: 2, timestamp: 2000),
            new MockBlockData(index: 3, timestamp: 3000)
        };

        // Act
        var count = await grain.ImportBlocksAsync(blocks);

        // Assert
        Assert.AreEqual(3, count);
        Assert.AreEqual(3u, await grain.GetHeightAsync());
    }

    [TestMethod]
    public async Task ImportBlocksAsync_UnorderedBlocks_SortsAndImports()
    {
        // Arrange
        var grain = _cluster!.GrainFactory.GetGrain<IBlockchainGrain>(0);
        var blocks = new[]
        {
            new MockBlockData(index: 3, timestamp: 3000),
            new MockBlockData(index: 1, timestamp: 1000),
            new MockBlockData(index: 2, timestamp: 2000)
        };

        // Act
        var count = await grain.ImportBlocksAsync(blocks);

        // Assert
        Assert.AreEqual(3, count);
        Assert.AreEqual(3u, await grain.GetHeightAsync());
    }

    [TestMethod]
    public async Task ImportBlocksAsync_WithGaps_ImportsOnlyValid()
    {
        // Arrange
        var grain = _cluster!.GrainFactory.GetGrain<IBlockchainGrain>(0);
        var blocks = new[]
        {
            new MockBlockData(index: 1, timestamp: 1000),
            new MockBlockData(index: 2, timestamp: 2000),
            new MockBlockData(index: 5, timestamp: 5000) // Gap - should fail
        };

        // Act
        var count = await grain.ImportBlocksAsync(blocks);

        // Assert
        Assert.AreEqual(2, count);
        Assert.AreEqual(2u, await grain.GetHeightAsync());
    }

    [TestMethod]
    public async Task GetCurrentBlockHashAsync_AfterPersist_ReturnsCorrectHash()
    {
        // Arrange
        var grain = _cluster!.GrainFactory.GetGrain<IBlockchainGrain>(0);
        var block = new MockBlockData(index: 1, timestamp: 1000);

        // Act
        await grain.PersistBlockAsync(block);
        var hash = await grain.GetCurrentBlockHashAsync();

        // Assert
        Assert.IsNotNull(hash);
        Assert.AreEqual(32, hash.Length);
        CollectionAssert.AreEqual(block.Hash.GetSpan().ToArray(), hash);
    }

    [TestMethod]
    public async Task GetBlockByHashAsync_NotImplemented_ReturnsNull()
    {
        // Arrange
        var grain = _cluster!.GrainFactory.GetGrain<IBlockchainGrain>(0);

        // Act
        var block = await grain.GetBlockByHashAsync(new byte[32]);

        // Assert
        Assert.IsNull(block);
    }

    [TestMethod]
    public async Task GetBlockByIndexAsync_NotImplemented_ReturnsNull()
    {
        // Arrange
        var grain = _cluster!.GrainFactory.GetGrain<IBlockchainGrain>(0);

        // Act
        var block = await grain.GetBlockByIndexAsync(1);

        // Assert
        Assert.IsNull(block);
    }
}

/// <summary>
/// Test silo configurator for Orleans TestCluster.
/// </summary>
public class TestSiloConfigurator : ISiloConfigurator
{
    public void Configure(ISiloBuilder siloBuilder)
    {
        siloBuilder.AddMemoryGrainStorage("BlockchainStore");
        siloBuilder.AddMemoryGrainStorage("MemoryPoolStore");
        siloBuilder.AddMemoryGrainStorage("LocalNodeStore");
        siloBuilder.AddMemoryGrainStorage("ConsensusStore");
        siloBuilder.AddMemoryGrainStorage("RemoteNodeStore");
    }
}

/// <summary>
/// Mock implementation of IBlockData for testing.
/// </summary>
[GenerateSerializer]
[Alias("Neo.Orleans.Tests.MockBlockData")]
internal class MockBlockData : IBlockData
{
    [Id(0)] private readonly byte[] _hashBytes;

    public MockBlockData() : this(0, 0) { }

    public MockBlockData(uint index, ulong timestamp)
    {
        Index = index;
        Timestamp = timestamp;
        _hashBytes = new byte[32];
        BitConverter.GetBytes(index).CopyTo(_hashBytes, 0);
        BitConverter.GetBytes(timestamp).CopyTo(_hashBytes, 4);
        Hash = new UInt256(_hashBytes);
        PrevHash = UInt256.Zero;
        MerkleRoot = UInt256.Zero;
        NextConsensus = UInt160.Zero;
    }

    [Id(1)] public UInt256 Hash { get; private set; }
    public uint Version => 0;
    [Id(2)] public UInt256 PrevHash { get; private set; }
    [Id(3)] public UInt256 MerkleRoot { get; private set; }
    [Id(4)] public ulong Timestamp { get; private set; }
    public ulong Nonce => 0;
    [Id(5)] public uint Index { get; private set; }
    public byte PrimaryIndex => 0;
    [Id(6)] public UInt160 NextConsensus { get; private set; }
    public int TransactionsCount => 0;
    public int Size => 0;

    public void Deserialize(ref MemoryReader reader) { }
    public void DeserializeUnsigned(ref MemoryReader reader) { }
    public void Serialize(System.IO.BinaryWriter writer) { }
    public void SerializeUnsigned(System.IO.BinaryWriter writer) { }
}
