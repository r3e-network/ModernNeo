using Neo.Network.P2P.Payloads;
using Neo.Orleans.Interfaces;
using Orleans.TestingHost;

namespace Neo.Orleans.Tests.Grains;

/// <summary>
/// Unit tests for TaskManagerGrain.
/// </summary>
[TestClass]
public class TaskManagerGrainTests
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
    public async Task AddTasksAsync_UpdatesStateSummary()
    {
        var grain = _cluster!.GrainFactory.GetGrain<ITaskManagerGrain>(0);

        var hash1 = new byte[32];
        var hash2 = new byte[32];
        BitConverter.GetBytes(1).CopyTo(hash1, 0);
        BitConverter.GetBytes(2).CopyTo(hash2, 0);

        var added = await grain.AddTasksAsync(new[] { hash1, hash2 }, (byte)InventoryType.Block);
        Assert.AreEqual(2, added);

        var summary = await grain.GetStateSummaryAsync();
        Assert.IsTrue(summary.PendingTaskCount >= 2);

        await grain.ClearAsync();
        summary = await grain.GetStateSummaryAsync();
        Assert.AreEqual(0, summary.PendingTaskCount);
    }
}
