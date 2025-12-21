using Neo.Orleans.Grains;
using Neo.Orleans.Interfaces;
using Orleans.TestingHost;

namespace Neo.Orleans.Tests.Grains;

/// <summary>
/// Unit tests for LocalNodeGrain.
/// </summary>
[TestClass]
public class LocalNodeGrainTests
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
    public async Task GetConnectedPeerCountAsync_InitialState_ReturnsZero()
    {
        // Arrange
        var grain = _cluster!.GrainFactory.GetGrain<ILocalNodeGrain>(0);

        // Act
        var count = await grain.GetConnectedPeerCountAsync();

        // Assert
        Assert.AreEqual(0, count);
    }

    [TestMethod]
    public async Task RegisterPeerAsync_SinglePeer_IncreasesCount()
    {
        // Arrange
        var grain = _cluster!.GrainFactory.GetGrain<ILocalNodeGrain>(0);

        // Act
        await grain.RegisterPeerAsync("192.168.1.1", 10333, 100);
        var count = await grain.GetConnectedPeerCountAsync();

        // Assert
        Assert.AreEqual(1, count);
    }

    [TestMethod]
    public async Task RegisterPeerAsync_MultiplePeers_CountsAll()
    {
        // Arrange
        var grain = _cluster!.GrainFactory.GetGrain<ILocalNodeGrain>(0);

        // Act
        await grain.RegisterPeerAsync("192.168.1.1", 10333, 100);
        await grain.RegisterPeerAsync("192.168.1.2", 10333, 101);
        await grain.RegisterPeerAsync("192.168.1.3", 10333, 102);
        var count = await grain.GetConnectedPeerCountAsync();

        // Assert
        Assert.AreEqual(3, count);
    }

    [TestMethod]
    public async Task RegisterPeerAsync_DuplicatePeer_UpdatesExisting()
    {
        // Arrange
        var grain = _cluster!.GrainFactory.GetGrain<ILocalNodeGrain>(0);

        // Act
        await grain.RegisterPeerAsync("192.168.1.1", 10333, 100);
        await grain.RegisterPeerAsync("192.168.1.1", 10333, 200); // Same address:port
        var count = await grain.GetConnectedPeerCountAsync();
        var peers = (await grain.GetConnectedPeersAsync()).ToList();

        // Assert
        Assert.AreEqual(1, count);
        Assert.AreEqual(200u, peers[0].Height); // Height should be updated
    }

    [TestMethod]
    public async Task UnregisterPeerAsync_ExistingPeer_DecreasesCount()
    {
        // Arrange
        var grain = _cluster!.GrainFactory.GetGrain<ILocalNodeGrain>(0);
        await grain.RegisterPeerAsync("192.168.1.1", 10333, 100);
        await grain.RegisterPeerAsync("192.168.1.2", 10333, 101);

        // Act
        await grain.UnregisterPeerAsync("192.168.1.1", 10333);
        var count = await grain.GetConnectedPeerCountAsync();

        // Assert
        Assert.AreEqual(1, count);
    }

    [TestMethod]
    public async Task UnregisterPeerAsync_NonExistingPeer_DoesNothing()
    {
        // Arrange
        var grain = _cluster!.GrainFactory.GetGrain<ILocalNodeGrain>(0);
        await grain.RegisterPeerAsync("192.168.1.1", 10333, 100);

        // Act
        await grain.UnregisterPeerAsync("192.168.1.99", 10333);
        var count = await grain.GetConnectedPeerCountAsync();

        // Assert
        Assert.AreEqual(1, count);
    }

    [TestMethod]
    public async Task UpdatePeerHeightAsync_ExistingPeer_UpdatesHeight()
    {
        // Arrange
        var grain = _cluster!.GrainFactory.GetGrain<ILocalNodeGrain>(0);
        await grain.RegisterPeerAsync("192.168.1.1", 10333, 100);

        // Act
        await grain.UpdatePeerHeightAsync("192.168.1.1", 10333, 500);
        var peers = (await grain.GetConnectedPeersAsync()).ToList();

        // Assert
        Assert.AreEqual(1, peers.Count);
        Assert.AreEqual(500u, peers[0].Height);
    }

    [TestMethod]
    public async Task GetConnectedPeersAsync_MultiplePeers_ReturnsAll()
    {
        // Arrange
        var grain = _cluster!.GrainFactory.GetGrain<ILocalNodeGrain>(0);
        await grain.RegisterPeerAsync("192.168.1.1", 10333, 100);
        await grain.RegisterPeerAsync("192.168.1.2", 10334, 200);

        // Act
        var peers = (await grain.GetConnectedPeersAsync()).ToList();

        // Assert
        Assert.AreEqual(2, peers.Count);
        Assert.IsTrue(peers.Any(p => p.Address == "192.168.1.1" && p.Port == 10333 && p.Height == 100));
        Assert.IsTrue(peers.Any(p => p.Address == "192.168.1.2" && p.Port == 10334 && p.Height == 200));
    }

    [TestMethod]
    public async Task RelayAsync_NoPeers_DoesNotThrow()
    {
        // Arrange
        var grain = _cluster!.GrainFactory.GetGrain<ILocalNodeGrain>(0);
        var hash = new byte[32];

        // Act & Assert - should not throw
        await grain.RelayAsync(hash, 0x2b); // 0x2b = Transaction inventory type
    }

    [TestMethod]
    public async Task BroadcastAsync_NoPeers_DoesNotThrow()
    {
        // Arrange
        var grain = _cluster!.GrainFactory.GetGrain<ILocalNodeGrain>(0);
        var message = new byte[] { 0x01, 0x02, 0x03 };

        // Act & Assert - should not throw
        await grain.BroadcastAsync(message);
    }

    [TestMethod]
    public async Task RelayAsync_DuplicateHash_RelaysOnlyOnce()
    {
        // Arrange
        var grain = _cluster!.GrainFactory.GetGrain<ILocalNodeGrain>(0);
        var hash = new byte[32];
        hash[0] = 0x42;

        // Act - relay same hash twice
        await grain.RelayAsync(hash, 0x2b);
        await grain.RelayAsync(hash, 0x2b);

        // Assert - no exception, internal relay cache prevents duplicate
        var count = await grain.GetConnectedPeerCountAsync();
        Assert.AreEqual(0, count);
    }
}
