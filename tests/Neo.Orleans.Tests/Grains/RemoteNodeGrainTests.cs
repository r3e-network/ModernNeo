using Neo.Orleans.Interfaces;
using Orleans.TestingHost;

namespace Neo.Orleans.Tests.Grains;

/// <summary>
/// Unit tests for RemoteNodeGrain.
/// </summary>
[TestClass]
public class RemoteNodeGrainTests
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
    public async Task GetStateAsync_InitialState_ReturnsConnecting()
    {
        // Arrange
        var grain = _cluster!.GrainFactory.GetGrain<IRemoteNodeGrain>("192.168.1.1:10333");

        // Act
        var state = await grain.GetStateAsync();

        // Assert
        Assert.AreEqual(ConnectionState.Connecting, state);
    }

    [TestMethod]
    public async Task GetRemoteHeightAsync_InitialState_ReturnsZero()
    {
        // Arrange
        var grain = _cluster!.GrainFactory.GetGrain<IRemoteNodeGrain>("192.168.1.1:10333");

        // Act
        var height = await grain.GetRemoteHeightAsync();

        // Assert
        Assert.AreEqual(0u, height);
    }

    [TestMethod]
    public async Task CompleteHandshakeAsync_SetsActiveState()
    {
        // Arrange
        var grain = _cluster!.GrainFactory.GetGrain<IRemoteNodeGrain>("192.168.1.2:10333");

        // Act
        await grain.CompleteHandshakeAsync(
            remoteHeight: 1000,
            listenerPort: 10333,
            isFullNode: true,
            userAgent: "/Neo:3.6.0/");

        var state = await grain.GetStateAsync();
        var height = await grain.GetRemoteHeightAsync();

        // Assert
        Assert.AreEqual(ConnectionState.Active, state);
        Assert.AreEqual(1000u, height);
    }

    [TestMethod]
    public async Task StartHandshakeAsync_SetsHandshakingState()
    {
        // Arrange
        var grain = _cluster!.GrainFactory.GetGrain<IRemoteNodeGrain>("192.168.1.3:10333");

        // Act
        await grain.StartHandshakeAsync(
            localHeight: 500,
            nonce: 12345,
            userAgent: "/NeoAN:1.0.0/");

        var state = await grain.GetStateAsync();

        // Assert
        Assert.AreEqual(ConnectionState.Handshaking, state);
    }

    [TestMethod]
    public async Task DisconnectAsync_DeactivatesGrain()
    {
        // Arrange
        var grain = _cluster!.GrainFactory.GetGrain<IRemoteNodeGrain>("192.168.1.4:10333");
        await grain.CompleteHandshakeAsync(1000, 10333, true, "/Neo:3.6.0/");

        // Verify active state before disconnect
        var stateBefore = await grain.GetStateAsync();
        Assert.AreEqual(ConnectionState.Active, stateBefore);

        // Act
        await grain.DisconnectAsync();

        // Note: After DisconnectAsync calls DeactivateOnIdle(), the grain is deactivated.
        // The next call to GetStateAsync() will activate a NEW grain instance,
        // which starts in Connecting state. This is expected Orleans behavior.
        // The test verifies that DisconnectAsync completes without error
        // and the grain can be reactivated (new connection).
        var stateAfter = await grain.GetStateAsync();

        // Assert - new grain instance starts fresh in Connecting state
        Assert.AreEqual(ConnectionState.Connecting, stateAfter);
    }

    [TestMethod]
    public async Task AddKnownHashAsync_AddsHash()
    {
        // Arrange
        var grain = _cluster!.GrainFactory.GetGrain<IRemoteNodeGrain>("192.168.1.5:10333");
        var hash = new byte[32];
        new Random(42).NextBytes(hash);

        // Act
        await grain.AddKnownHashAsync(hash);
        var isKnown = await grain.IsKnownHashAsync(hash);

        // Assert
        Assert.IsTrue(isKnown);
    }

    [TestMethod]
    public async Task IsKnownHashAsync_UnknownHash_ReturnsFalse()
    {
        // Arrange
        var grain = _cluster!.GrainFactory.GetGrain<IRemoteNodeGrain>("192.168.1.6:10333");
        var hash = new byte[32];
        new Random(42).NextBytes(hash);

        // Act
        var isKnown = await grain.IsKnownHashAsync(hash);

        // Assert
        Assert.IsFalse(isKnown);
    }

    [TestMethod]
    public async Task HandleMessageAsync_VersionAck_SetsActiveState()
    {
        // Arrange
        var grain = _cluster!.GrainFactory.GetGrain<IRemoteNodeGrain>("192.168.1.7:10333");
        await grain.StartHandshakeAsync(500, 12345, "/NeoAN:1.0.0/");

        // Create VERSION_ACK message (command 0x01)
        var message = new byte[] { 0x01 };

        // Act
        await grain.HandleMessageAsync(message);
        var state = await grain.GetStateAsync();

        // Assert
        Assert.AreEqual(ConnectionState.Active, state);
    }

    [TestMethod]
    public async Task HandleMessageAsync_HeightUpdate_UpdatesRemoteHeight()
    {
        // Arrange
        var grain = _cluster!.GrainFactory.GetGrain<IRemoteNodeGrain>("192.168.1.8:10333");
        await grain.CompleteHandshakeAsync(1000, 10333, true, "/Neo:3.6.0/");

        // Create HEIGHT_UPDATE message (command 0x02 + uint32 height)
        var message = new byte[5];
        message[0] = 0x02;
        BitConverter.GetBytes(2000u).CopyTo(message, 1);

        // Act
        await grain.HandleMessageAsync(message);
        var height = await grain.GetRemoteHeightAsync();

        // Assert
        Assert.AreEqual(2000u, height);
    }

    [TestMethod]
    public async Task SendAsync_WhenNotActive_QueuesMessage()
    {
        // Arrange
        var grain = _cluster!.GrainFactory.GetGrain<IRemoteNodeGrain>("192.168.1.9:10333");
        // Grain starts in Connecting state, not Active

        var message = new byte[] { 0x10, 0x20, 0x30 };

        // Act - should queue without error
        await grain.SendAsync(message);

        // Assert - no exception means success
        var state = await grain.GetStateAsync();
        Assert.AreEqual(ConnectionState.Connecting, state);
    }

    [TestMethod]
    public async Task MultipleGrains_IndependentState()
    {
        // Arrange
        var grain1 = _cluster!.GrainFactory.GetGrain<IRemoteNodeGrain>("10.0.0.1:10333");
        var grain2 = _cluster!.GrainFactory.GetGrain<IRemoteNodeGrain>("10.0.0.2:10333");

        // Act
        await grain1.CompleteHandshakeAsync(1000, 10333, true, "/Neo:3.6.0/");
        await grain2.CompleteHandshakeAsync(2000, 10333, false, "/Neo:3.5.0/");

        var height1 = await grain1.GetRemoteHeightAsync();
        var height2 = await grain2.GetRemoteHeightAsync();

        // Assert
        Assert.AreEqual(1000u, height1);
        Assert.AreEqual(2000u, height2);
    }
}
