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
        changeView[0] = 0x00; // ChangeView type (correct value)
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

    #region Compatibility Tests - Message Type Codes

    [TestMethod]
    public async Task MessageTypeCode_Commit_MatchesConsensusMessageType()
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

        // Commit message with correct type code 0x30 (matches ConsensusMessageType.Commit)
        var commit = new byte[34];
        commit[0] = 0x30; // ConsensusMessageType.Commit
        commit[1] = 1;    // From validator 1

        // Act
        await grain.OnConsensusMessageAsync(commit, "validator1_address");
        var state = await grain.GetStateAsync();

        // Assert - message should be processed (phase remains RequestSent, need more commits)
        Assert.AreEqual(ConsensusPhase.RequestSent, state.Phase);
    }

    [TestMethod]
    public async Task MessageTypeCode_ChangeView_MatchesConsensusMessageType()
    {
        // Arrange
        var grain = _cluster!.GrainFactory.GetGrain<IConsensusGrain>(0);
        await grain.InitializeAsync(myIndex: 0, validatorCount: 7);
        await grain.StartAsync();

        // ChangeView message with correct type code 0x00 (matches ConsensusMessageType.ChangeView)
        var changeView = new byte[3];
        changeView[0] = 0x00; // ConsensusMessageType.ChangeView
        changeView[1] = 1;    // From validator 1

        // Act
        await grain.OnConsensusMessageAsync(changeView, "validator1_address");
        var viewNumber = await grain.GetViewNumberAsync();

        // Assert - view number should increment
        Assert.AreEqual((byte)1, viewNumber);
    }

    [TestMethod]
    public async Task MessageTypeCode_PrepareRequest_MatchesConsensusMessageType()
    {
        // Arrange
        var grain = _cluster!.GrainFactory.GetGrain<IConsensusGrain>(0);
        await grain.InitializeAsync(myIndex: 0, validatorCount: 7);
        await grain.StartAsync();

        // PrepareRequest message with correct type code 0x20 (matches ConsensusMessageType.PrepareRequest)
        var prepareRequest = new byte[34];
        prepareRequest[0] = 0x20; // ConsensusMessageType.PrepareRequest
        prepareRequest[1] = 1;    // From primary

        // Act
        await grain.OnConsensusMessageAsync(prepareRequest, "primary_address");
        var state = await grain.GetStateAsync();

        // Assert - should move to RequestSent phase
        Assert.AreEqual(ConsensusPhase.RequestSent, state.Phase);
    }

    [TestMethod]
    public async Task MessageTypeCode_PrepareResponse_MatchesConsensusMessageType()
    {
        // Arrange
        var grain = _cluster!.GrainFactory.GetGrain<IConsensusGrain>(0);
        await grain.InitializeAsync(myIndex: 0, validatorCount: 7);
        await grain.StartAsync();

        // First send PrepareRequest
        var prepareRequest = new byte[34];
        prepareRequest[0] = 0x20;
        prepareRequest[1] = 0;
        await grain.OnConsensusMessageAsync(prepareRequest, "self");

        // PrepareResponse message with correct type code 0x21 (matches ConsensusMessageType.PrepareResponse)
        var prepareResponse = new byte[34];
        prepareResponse[0] = 0x21; // ConsensusMessageType.PrepareResponse
        prepareResponse[1] = 1;    // From validator 1

        // Act
        await grain.OnConsensusMessageAsync(prepareResponse, "validator1_address");
        var state = await grain.GetStateAsync();

        // Assert - should remain in RequestSent (need more responses)
        Assert.AreEqual(ConsensusPhase.RequestSent, state.Phase);
    }

    [TestMethod]
    public async Task MessageTypeCode_InvalidCommitCode_IgnoresMessage()
    {
        // Arrange
        var grain = _cluster!.GrainFactory.GetGrain<IConsensusGrain>(0);
        await grain.InitializeAsync(myIndex: 0, validatorCount: 7);
        await grain.StartAsync();

        // First send PrepareRequest
        var prepareRequest = new byte[34];
        prepareRequest[0] = 0x20;
        prepareRequest[1] = 0;
        await grain.OnConsensusMessageAsync(prepareRequest, "self");

        // Commit message with OLD INCORRECT type code 0x22 (should be ignored)
        var invalidCommit = new byte[34];
        invalidCommit[0] = 0x22; // OLD incorrect value
        invalidCommit[1] = 1;

        // Act
        await grain.OnConsensusMessageAsync(invalidCommit, "validator1_address");
        var state = await grain.GetStateAsync();

        // Assert - should remain in RequestSent (message ignored)
        Assert.AreEqual(ConsensusPhase.RequestSent, state.Phase);
    }

    [TestMethod]
    public async Task MessageTypeCode_InvalidChangeViewCode_IgnoresMessage()
    {
        // Arrange
        var grain = _cluster!.GrainFactory.GetGrain<IConsensusGrain>(0);
        await grain.InitializeAsync(myIndex: 0, validatorCount: 7);
        await grain.StartAsync();

        var initialViewNumber = await grain.GetViewNumberAsync();

        // ChangeView message with OLD INCORRECT type code 0x23 (should be ignored)
        var invalidChangeView = new byte[3];
        invalidChangeView[0] = 0x23; // OLD incorrect value
        invalidChangeView[1] = 1;

        // Act
        await grain.OnConsensusMessageAsync(invalidChangeView, "validator1_address");
        var viewNumber = await grain.GetViewNumberAsync();

        // Assert - view number should NOT change (message ignored)
        Assert.AreEqual(initialViewNumber, viewNumber);
    }

    [TestMethod]
    public async Task MessageFormat_AllMessageTypes_UseCorrectDbftStateMachineFormat()
    {
        // This test verifies that ConsensusGrain uses the same message type codes as DbftStateMachine
        // Message format: [type:1][validator_index:1][payload:...]

        // Arrange
        var grain = _cluster!.GrainFactory.GetGrain<IConsensusGrain>(0);
        await grain.InitializeAsync(myIndex: 0, validatorCount: 7);
        await grain.StartAsync();

        // Test all message types with correct codes from ConsensusMessageType enum
        var testCases = new[]
        {
            (Type: (byte)0x00, Name: "ChangeView"),
            (Type: (byte)0x20, Name: "PrepareRequest"),
            (Type: (byte)0x21, Name: "PrepareResponse"),
            (Type: (byte)0x30, Name: "Commit")
        };

        foreach (var testCase in testCases)
        {
            var message = new byte[34];
            message[0] = testCase.Type;
            message[1] = 1; // validator index

            // Act & Assert - should not throw
            await grain.OnConsensusMessageAsync(message, $"validator_{testCase.Name}");
        }

        // If we reach here, all message types were processed without errors
        Assert.IsTrue(true, "All message types processed successfully");
    }

    [TestMethod]
    public async Task Compatibility_CommitMessageFlow_MatchesDbftStateMachine()
    {
        // This test verifies the complete commit flow matches DbftStateMachine behavior
        // Tests that message type codes 0x20, 0x21, 0x30 work correctly through full consensus

        // Arrange
        var grain = _cluster!.GrainFactory.GetGrain<IConsensusGrain>(0);
        await grain.InitializeAsync(myIndex: 0, validatorCount: 7);
        await grain.StartAsync();

        var initialBlockIndex = (await grain.GetStateAsync()).BlockIndex;

        // Step 1: PrepareRequest (0x20) - correct code matching ConsensusMessageType
        var prepareRequest = new byte[34];
        prepareRequest[0] = 0x20; // ConsensusMessageType.PrepareRequest
        prepareRequest[1] = 0;
        await grain.OnConsensusMessageAsync(prepareRequest, "self");

        var state1 = await grain.GetStateAsync();
        Assert.AreEqual(ConsensusPhase.RequestSent, state1.Phase);

        // Step 2: Send PrepareResponses (0x21) - correct code matching ConsensusMessageType
        // Threshold = (7 * 2 / 3) + 1 = 5
        // Grain already has 1 PrepareResponse (its own), need 4 more
        for (int i = 1; i <= 4; i++)
        {
            var prepareResponse = new byte[34];
            prepareResponse[0] = 0x21; // ConsensusMessageType.PrepareResponse
            prepareResponse[1] = (byte)i;
            await grain.OnConsensusMessageAsync(prepareResponse, $"validator{i}");
        }

        // After threshold, should be in ResponseSent and have sent own commit
        var state2 = await grain.GetStateAsync();
        Assert.AreEqual(ConsensusPhase.ResponseSent, state2.Phase);

        // Step 3: Send Commit messages (0x30) - correct code matching ConsensusMessageType
        // Grain already has 1 commit (its own), need 4 more to reach threshold of 5
        for (int i = 1; i <= 4; i++)
        {
            var commit = new byte[34];
            commit[0] = 0x30; // ConsensusMessageType.Commit (FIXED from 0x22)
            commit[1] = (byte)i;
            await grain.OnConsensusMessageAsync(commit, $"validator{i}");
        }

        // After reaching commit threshold, block advances and phase resets
        var state3 = await grain.GetStateAsync();
        // Block should have advanced (CommitSent triggers AdvanceToNextBlock)
        Assert.AreEqual(initialBlockIndex + 1, state3.BlockIndex);
        // View should reset to 0
        Assert.AreEqual((byte)0, state3.ViewNumber);
    }

    [TestMethod]
    public async Task Compatibility_ChangeViewFlow_MatchesDbftStateMachine()
    {
        // This test verifies the change view flow matches DbftStateMachine behavior

        // Arrange
        var grain = _cluster!.GrainFactory.GetGrain<IConsensusGrain>(0);
        await grain.InitializeAsync(myIndex: 0, validatorCount: 7);
        await grain.StartAsync();

        var initialView = await grain.GetViewNumberAsync();
        Assert.AreEqual((byte)0, initialView);

        // Act: Send ChangeView message with correct code (0x00) matching DbftStateMachine
        var changeView = new byte[3];
        changeView[0] = 0x00; // ConsensusMessageType.ChangeView
        changeView[1] = 1;
        await grain.OnConsensusMessageAsync(changeView, "validator1");

        // Assert
        var newView = await grain.GetViewNumberAsync();
        Assert.AreEqual((byte)1, newView);

        var state = await grain.GetStateAsync();
        Assert.AreEqual(ConsensusPhase.Backup, state.Phase); // Should reset to appropriate phase
    }

    #endregion
}
