using Neo;
using Neo.Core.Interfaces;
using Neo.IO;
using Neo.Orleans.Interfaces;
using Orleans.TestingHost;

namespace Neo.Orleans.Tests.Integration;

/// <summary>
/// Integration tests for Consensus and Blockchain grain collaboration.
/// Tests: ConsensusGrain -> BlockchainGrain height synchronization.
/// </summary>
[TestClass]
public class ConsensusBlockchainIntegrationTests
{
    private TestCluster? _cluster;

    [TestInitialize]
    public async Task Setup()
    {
        var builder = new TestClusterBuilder();
        builder.AddSiloBuilderConfigurator<IntegrationTestSiloConfigurator>();
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

    /// <summary>
    /// Scenario 5: Consensus start should use blockchain height + 1 as block index.
    /// Tests: ConsensusGrain.StartAsync -> BlockchainGrain.GetHeightAsync
    /// </summary>
    [TestMethod]
    public async Task Consensus_StartAsync_UsesBlockchainHeightPlusOneAsBlockIndex()
    {
        // Arrange - Persist some blocks to blockchain
        var blockchain = _cluster!.GrainFactory.GetGrain<IBlockchainGrain>(0);
        var consensus = _cluster.GrainFactory.GetGrain<IConsensusGrain>(0);

        // Persist blocks to reach height 5
        for (uint i = 1; i <= 5; i++)
        {
            var block = new MockBlockData(index: i, timestamp: i * 1000);
            await blockchain.PersistBlockAsync(block);
        }

        var blockchainHeight = await blockchain.GetHeightAsync();
        Assert.AreEqual(5u, blockchainHeight, "Blockchain should be at height 5");

        // Act - Initialize and start consensus
        // Validator at index 0 with 7 total validators
        await consensus.InitializeAsync(myIndex: 0, validatorCount: 7);
        await consensus.StartAsync();

        // Assert - Consensus should target block index = height + 1 = 6
        var state = await consensus.GetStateAsync();
        Assert.AreEqual(6u, state.BlockIndex, "Consensus should target block index 6 (height + 1)");
    }

    /// <summary>
    /// Scenario: Consensus with empty blockchain starts at block 1.
    /// </summary>
    [TestMethod]
    public async Task Consensus_StartAsync_EmptyBlockchain_StartsAtBlockOne()
    {
        // Arrange
        var blockchain = _cluster!.GrainFactory.GetGrain<IBlockchainGrain>(0);
        var consensus = _cluster.GrainFactory.GetGrain<IConsensusGrain>(0);

        var height = await blockchain.GetHeightAsync();
        Assert.AreEqual(0u, height, "Blockchain should start empty");

        // Act
        await consensus.InitializeAsync(myIndex: 0, validatorCount: 7);
        await consensus.StartAsync();

        // Assert
        var state = await consensus.GetStateAsync();
        Assert.AreEqual(1u, state.BlockIndex, "Consensus should start at block 1 for empty blockchain");
    }

    /// <summary>
    /// Scenario: Multiple consensus rounds after blockchain grows.
    /// </summary>
    [TestMethod]
    public async Task Consensus_MultipleRounds_TracksBlockchainGrowth()
    {
        // Arrange
        var blockchain = _cluster!.GrainFactory.GetGrain<IBlockchainGrain>(0);
        var consensus = _cluster.GrainFactory.GetGrain<IConsensusGrain>(0);

        // Initialize consensus
        await consensus.InitializeAsync(myIndex: 1, validatorCount: 7);

        // Round 1: Empty blockchain
        await consensus.StartAsync();
        var state1 = await consensus.GetStateAsync();
        Assert.AreEqual(1u, state1.BlockIndex);

        // Simulate block 1 being persisted
        await blockchain.PersistBlockAsync(new MockBlockData(1, 1000));

        // Round 2: Restart consensus (simulating new round)
        await consensus.StopAsync();
        await consensus.StartAsync();
        var state2 = await consensus.GetStateAsync();
        Assert.AreEqual(2u, state2.BlockIndex, "After block 1, consensus should target block 2");

        // Persist more blocks
        await blockchain.PersistBlockAsync(new MockBlockData(2, 2000));
        await blockchain.PersistBlockAsync(new MockBlockData(3, 3000));

        // Round 3
        await consensus.StopAsync();
        await consensus.StartAsync();
        var state3 = await consensus.GetStateAsync();
        Assert.AreEqual(4u, state3.BlockIndex, "After block 3, consensus should target block 4");
    }

    /// <summary>
    /// Scenario: Consensus primary election based on blockchain height.
    /// </summary>
    [TestMethod]
    public async Task Consensus_PrimaryElection_DependsOnBlockchainHeight()
    {
        // Arrange
        var blockchain = _cluster!.GrainFactory.GetGrain<IBlockchainGrain>(0);

        // Persist blocks to height 3
        for (uint i = 1; i <= 3; i++)
        {
            await blockchain.PersistBlockAsync(new MockBlockData(i, i * 1000));
        }

        // Create consensus grain for validator at index 4
        // Primary = (BlockIndex + ViewNumber) % ValidatorCount = (4 + 0) % 7 = 4
        var consensus = _cluster!.GrainFactory.GetGrain<IConsensusGrain>(0);
        await consensus.InitializeAsync(myIndex: 4, validatorCount: 7);
        await consensus.StartAsync();

        // Assert - Validator 4 should be primary for block 4
        var isPrimary = await consensus.IsPrimaryAsync();
        Assert.IsTrue(isPrimary, "Validator 4 should be primary for block index 4 with view 0");
    }
}

/// <summary>
/// Mock implementation of IBlockData for integration testing.
/// </summary>
[GenerateSerializer]
[Alias("Neo.Orleans.Tests.Integration.MockBlockData")]
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
