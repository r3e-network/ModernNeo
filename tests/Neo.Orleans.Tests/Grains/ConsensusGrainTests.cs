using Neo.Orleans.Grains;
using Neo.Orleans.Interfaces;
using Orleans.TestingHost;

namespace Neo.Orleans.Tests.Grains;

/// <summary>
/// Unit tests for ConsensusGrain.
/// </summary>
[TestClass]
public class ConsensusGrainTests
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
    public async Task GetStateAsync_InitialState_ReturnsInitialPhase()
    {
        // Arrange
        var grain = _cluster!.GrainFactory.GetGrain<IConsensusGrain>(0);

        // Act
        var state = await grain.GetStateAsync();

        // Assert
        Assert.AreEqual(ConsensusPhase.Initial, state.Phase);
        Assert.AreEqual((byte)0, state.ViewNumber);
    }

    [TestMethod]
    public async Task StartAsync_NotRunning_StartsConsensus()
    {
        // Arrange
        var grain = _cluster!.GrainFactory.GetGrain<IConsensusGrain>(0);
        // BlockIndex will be 1 (height 0 + 1), so primary = (1 + 0) % 7 = 1
        await grain.InitializeAsync(myIndex: 1, validatorCount: 7);

        // Act
        await grain.StartAsync();
        var state = await grain.GetStateAsync();

        // Assert
        Assert.AreEqual(ConsensusPhase.Primary, state.Phase); // Index 1 is primary for block 1 view 0
    }

    [TestMethod]
    public async Task StartAsync_AlreadyRunning_DoesNothing()
    {
        // Arrange
        var grain = _cluster!.GrainFactory.GetGrain<IConsensusGrain>(0);
        // BlockIndex will be 1, so primary = (1 + 0) % 7 = 1
        await grain.InitializeAsync(myIndex: 1, validatorCount: 7);
        await grain.StartAsync();

        // Act
        await grain.StartAsync(); // Second call
        var state = await grain.GetStateAsync();

        // Assert
        Assert.AreEqual(ConsensusPhase.Primary, state.Phase);
    }

    [TestMethod]
    public async Task StopAsync_Running_StopsConsensus()
    {
        // Arrange
        var grain = _cluster!.GrainFactory.GetGrain<IConsensusGrain>(0);
        await grain.InitializeAsync(myIndex: 0, validatorCount: 7);
        await grain.StartAsync();

        // Act
        await grain.StopAsync();
        var state = await grain.GetStateAsync();

        // Assert
        Assert.AreEqual(ConsensusPhase.Initial, state.Phase);
    }

    [TestMethod]
    public async Task IsPrimaryAsync_ValidatorIndex1View0_ReturnsTrue()
    {
        // Arrange - BlockIndex=1, view=0, so primary = (1 + 0) % 7 = 1
        var grain = _cluster!.GrainFactory.GetGrain<IConsensusGrain>(0);
        await grain.InitializeAsync(myIndex: 1, validatorCount: 7);
        await grain.StartAsync();

        // Act
        var isPrimary = await grain.IsPrimaryAsync();

        // Assert
        Assert.IsTrue(isPrimary);
    }

    [TestMethod]
    public async Task IsPrimaryAsync_ValidatorIndex0View0_ReturnsFalse()
    {
        // Arrange - BlockIndex=1, view=0, so primary = 1, not 0
        var grain = _cluster!.GrainFactory.GetGrain<IConsensusGrain>(0);
        await grain.InitializeAsync(myIndex: 0, validatorCount: 7);
        await grain.StartAsync();

        // Act
        var isPrimary = await grain.IsPrimaryAsync();

        // Assert
        Assert.IsFalse(isPrimary);
    }

    [TestMethod]
    public async Task GetViewNumberAsync_InitialState_ReturnsZero()
    {
        // Arrange
        var grain = _cluster!.GrainFactory.GetGrain<IConsensusGrain>(0);

        // Act
        var viewNumber = await grain.GetViewNumberAsync();

        // Assert
        Assert.AreEqual((byte)0, viewNumber);
    }

    [TestMethod]
    public async Task OnConsensusMessageAsync_PrepareRequest_UpdatesPhase()
    {
        // Arrange
        var grain = _cluster!.GrainFactory.GetGrain<IConsensusGrain>(0);
        // BlockIndex=1, primary = (1+0)%7 = 1, so index 0 is backup
        await grain.InitializeAsync(myIndex: 0, validatorCount: 7); // Backup node
        await grain.StartAsync();

        // PrepareRequest message: [type:0x20][validator_index:1][block_hash:32 bytes]
        var message = new byte[34];
        message[0] = 0x20; // PrepareRequest type
        message[1] = 1;    // From primary (validator 1)

        // Act
        await grain.OnConsensusMessageAsync(message, "primary_address");
        var state = await grain.GetStateAsync();

        // Assert
        Assert.AreEqual(ConsensusPhase.RequestSent, state.Phase);
    }

    [TestMethod]
    public async Task OnConsensusMessageAsync_PrepareResponse_UpdatesPayloads()
    {
        // Arrange
        var grain = _cluster!.GrainFactory.GetGrain<IConsensusGrain>(0);
        await grain.InitializeAsync(myIndex: 0, validatorCount: 7);
        await grain.StartAsync();

        // First send PrepareRequest to move to RequestSent phase
        var prepareRequest = new byte[34];
        prepareRequest[0] = 0x20;
        prepareRequest[1] = 0;
        await grain.OnConsensusMessageAsync(prepareRequest, "self");

        // PrepareResponse from validator 1
        var prepareResponse = new byte[34];
        prepareResponse[0] = 0x21; // PrepareResponse type
        prepareResponse[1] = 1;    // From validator 1

        // Act
        await grain.OnConsensusMessageAsync(prepareResponse, "validator1_address");
        var state = await grain.GetStateAsync();

        // Assert - should still be in RequestSent (need 5 responses for threshold)
        Assert.AreEqual(ConsensusPhase.RequestSent, state.Phase);
    }

    [TestMethod]
    public async Task OnConsensusMessageAsync_ChangeView_IncrementsViewNumber()
    {
        // Arrange
        var grain = _cluster!.GrainFactory.GetGrain<IConsensusGrain>(0);
        await grain.InitializeAsync(myIndex: 0, validatorCount: 7);
        await grain.StartAsync();

        // ChangeView message
        var changeView = new byte[3];
        changeView[0] = 0x23; // ChangeView type
        changeView[1] = 1;    // From validator 1

        // Act
        await grain.OnConsensusMessageAsync(changeView, "validator1_address");
        var viewNumber = await grain.GetViewNumberAsync();

        // Assert
        Assert.AreEqual((byte)1, viewNumber);
    }

    [TestMethod]
    public async Task OnConsensusMessageAsync_EmptyMessage_DoesNotThrow()
    {
        // Arrange
        var grain = _cluster!.GrainFactory.GetGrain<IConsensusGrain>(0);
        await grain.InitializeAsync(myIndex: 0, validatorCount: 7);
        await grain.StartAsync();

        // Act & Assert - should not throw
        await grain.OnConsensusMessageAsync(Array.Empty<byte>(), "any_address");
    }

    [TestMethod]
    public async Task OnConsensusMessageAsync_NotRunning_IgnoresMessage()
    {
        // Arrange
        var grain = _cluster!.GrainFactory.GetGrain<IConsensusGrain>(0);
        await grain.InitializeAsync(myIndex: 0, validatorCount: 7);
        // Note: Not calling StartAsync

        var message = new byte[34];
        message[0] = 0x20;
        message[1] = 0;

        // Act
        await grain.OnConsensusMessageAsync(message, "any_address");
        var state = await grain.GetStateAsync();

        // Assert - should still be Initial since not running
        Assert.AreEqual(ConsensusPhase.Initial, state.Phase);
    }
}
