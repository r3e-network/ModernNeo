using Orleans.TestingHost;

namespace Neo.Orleans.Tests.Integration;

/// <summary>
/// Shared silo configurator for integration tests.
/// Configures all grain storage providers needed for multi-grain collaboration tests.
/// </summary>
public class IntegrationTestSiloConfigurator : ISiloConfigurator
{
    public void Configure(ISiloBuilder siloBuilder)
    {
        // Configure all grain storage providers
        siloBuilder.AddMemoryGrainStorage("BlockchainStore");
        siloBuilder.AddMemoryGrainStorage("MemoryPoolStore");
        siloBuilder.AddMemoryGrainStorage("LocalNodeStore");
        siloBuilder.AddMemoryGrainStorage("ConsensusStore");
        siloBuilder.AddMemoryGrainStorage("RemoteNodeStore");
    }
}
