// Copyright (C) 2015-2025 The Neo Project.
//
// UT_DbftStateMachine.cs file belongs to the neo project and is free
// software distributed under the MIT software license.

#nullable enable

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Neo.Consensus;
using Neo.Core.Interfaces;

namespace Neo.UnitTests.Consensus;

[TestClass]
public class UT_DbftStateMachine
{
    public TestContext TestContext { get; set; } = null!;

    private Mock<IConsensusContext> _mockContext = null!;
    private DbftStateMachine _stateMachine = null!;
    private DbftOptions _options = null!;

    [TestInitialize]
    public void Setup()
    {
        _mockContext = new Mock<IConsensusContext>();
        _mockContext.Setup(c => c.BlockIndex).Returns(100);
        _mockContext.Setup(c => c.ViewNumber).Returns((byte)0);
        _mockContext.Setup(c => c.MyIndex).Returns(0);
        _mockContext.Setup(c => c.ValidatorCount).Returns(7);
        _mockContext.Setup(c => c.Validators).Returns(new List<byte[]>
        {
            new byte[33], new byte[33], new byte[33], new byte[33],
            new byte[33], new byte[33], new byte[33]
        });
        _mockContext.Setup(c => c.PrepareResponseCount).Returns(0);
        _mockContext.Setup(c => c.CommitCount).Returns(0);

        _options = new DbftOptions
        {
            BlockInterval = TimeSpan.FromSeconds(1),
            ViewChangeTimeout = TimeSpan.FromSeconds(5)
        };

        _stateMachine = new DbftStateMachine(_mockContext.Object, _options);
    }

    [TestCleanup]
    public void Cleanup()
    {
        _stateMachine.Dispose();
    }

    [TestMethod]
    public void TestConstructor_NullContext_ThrowsException()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() =>
            new DbftStateMachine(null!, _options));
    }

    [TestMethod]
    public void TestConstructor_DefaultOptions()
    {
        using var sm = new DbftStateMachine(_mockContext.Object);
        Assert.IsNotNull(sm);
        Assert.IsFalse(sm.IsRunning);
    }

    [TestMethod]
    public void TestIsRunning_InitiallyFalse()
    {
        Assert.IsFalse(_stateMachine.IsRunning);
    }

    [TestMethod]
    public async Task TestStartAsync()
    {
        await _stateMachine.StartAsync(TestContext.CancellationTokenSource.Token);

        Assert.IsTrue(_stateMachine.IsRunning);
        _mockContext.Verify(c => c.Reset(It.IsAny<uint>(), It.IsAny<byte>()), Times.Once);
    }

    [TestMethod]
    public async Task TestStartAsync_AlreadyRunning()
    {
        var ct = TestContext.CancellationTokenSource.Token;
        await _stateMachine.StartAsync(ct);
        await _stateMachine.StartAsync(ct); // Should not throw

        Assert.IsTrue(_stateMachine.IsRunning);
        _mockContext.Verify(c => c.Reset(It.IsAny<uint>(), It.IsAny<byte>()), Times.Once);
    }

    [TestMethod]
    public async Task TestStopAsync()
    {
        var ct = TestContext.CancellationTokenSource.Token;
        await _stateMachine.StartAsync(ct);
        await _stateMachine.StopAsync(ct);

        Assert.IsFalse(_stateMachine.IsRunning);
    }

    [TestMethod]
    public async Task TestStopAsync_NotRunning()
    {
        await _stateMachine.StopAsync(TestContext.CancellationTokenSource.Token); // Should not throw
        Assert.IsFalse(_stateMachine.IsRunning);
    }

    [TestMethod]
    public async Task TestGetStateAsync()
    {
        var ct = TestContext.CancellationTokenSource.Token;
        await _stateMachine.StartAsync(ct);

        var state = await _stateMachine.GetStateAsync();

        Assert.AreEqual(100u, state.BlockIndex);
        Assert.IsTrue(state.IsRunning);
    }

    [TestMethod]
    public async Task TestOnBlockPersistedAsync()
    {
        var ct = TestContext.CancellationTokenSource.Token;
        await _stateMachine.StartAsync(ct);
        await _stateMachine.OnBlockPersistedAsync(100);

        var state = await _stateMachine.GetStateAsync();
        Assert.AreEqual(101u, state.BlockIndex);
    }

    [TestMethod]
    public async Task TestOnMessageAsync_NotRunning()
    {
        var mockMessage = new Mock<IConsensusMessage>();
        mockMessage.Setup(m => m.Type).Returns(ConsensusMessageType.PrepareRequest);

        // Should not throw when not running
        await _stateMachine.OnMessageAsync(mockMessage.Object, new byte[33]);
    }

    [TestMethod]
    public void TestContext_ReturnsInjectedContext()
    {
        Assert.AreSame(_mockContext.Object, _stateMachine.Context);
    }

    [TestMethod]
    public void TestDispose()
    {
        _stateMachine.Dispose();
        _stateMachine.Dispose(); // Should not throw on double dispose
    }
}

[TestClass]
public class UT_DbftOptions
{
    [TestMethod]
    public void TestDefaultValues()
    {
        var options = new DbftOptions();

        Assert.AreEqual(TimeSpan.FromSeconds(15), options.BlockInterval);
        Assert.AreEqual(TimeSpan.FromSeconds(30), options.ViewChangeTimeout);
        Assert.AreEqual(10, options.MaxViewChanges);
    }

    [TestMethod]
    public void TestCustomValues()
    {
        var options = new DbftOptions
        {
            BlockInterval = TimeSpan.FromSeconds(5),
            ViewChangeTimeout = TimeSpan.FromSeconds(10),
            MaxViewChanges = 5
        };

        Assert.AreEqual(TimeSpan.FromSeconds(5), options.BlockInterval);
        Assert.AreEqual(TimeSpan.FromSeconds(10), options.ViewChangeTimeout);
        Assert.AreEqual(5, options.MaxViewChanges);
    }
}

[TestClass]
public class UT_ConsensusStateSnapshot
{
    [TestMethod]
    public void TestRecordCreation()
    {
        var snapshot = new ConsensusStateSnapshot(
            BlockIndex: 100,
            ViewNumber: 1,
            Phase: ConsensusPhase.Primary,
            IsPrimary: true,
            ValidatorCount: 7,
            PrepareResponseCount: 5,
            CommitCount: 3,
            IsRunning: true);

        Assert.AreEqual(100u, snapshot.BlockIndex);
        Assert.AreEqual((byte)1, snapshot.ViewNumber);
        Assert.AreEqual(ConsensusPhase.Primary, snapshot.Phase);
        Assert.IsTrue(snapshot.IsPrimary);
        Assert.AreEqual(7, snapshot.ValidatorCount);
        Assert.AreEqual(5, snapshot.PrepareResponseCount);
        Assert.AreEqual(3, snapshot.CommitCount);
        Assert.IsTrue(snapshot.IsRunning);
    }

    [TestMethod]
    public void TestRecordEquality()
    {
        var snapshot1 = new ConsensusStateSnapshot(100, 1, ConsensusPhase.Primary, true, 7, 5, 3, true);
        var snapshot2 = new ConsensusStateSnapshot(100, 1, ConsensusPhase.Primary, true, 7, 5, 3, true);

        Assert.AreEqual(snapshot1, snapshot2);
    }
}

[TestClass]
public class UT_ConsensusPhase
{
    [TestMethod]
    public void TestPhaseValues()
    {
        Assert.AreEqual((byte)0, (byte)ConsensusPhase.Initial);
        Assert.AreEqual((byte)1, (byte)ConsensusPhase.Primary);
        Assert.AreEqual((byte)2, (byte)ConsensusPhase.Backup);
        Assert.AreEqual((byte)3, (byte)ConsensusPhase.RequestSent);
        Assert.AreEqual((byte)4, (byte)ConsensusPhase.RequestReceived);
        Assert.AreEqual((byte)5, (byte)ConsensusPhase.ResponseSent);
        Assert.AreEqual((byte)6, (byte)ConsensusPhase.CommitSent);
        Assert.AreEqual((byte)7, (byte)ConsensusPhase.ViewChanging);
        Assert.AreEqual((byte)8, (byte)ConsensusPhase.BlockSent);
    }
}
