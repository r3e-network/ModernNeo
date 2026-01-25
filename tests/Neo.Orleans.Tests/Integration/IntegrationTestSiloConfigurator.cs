// Copyright (C) 2015-2025 The Neo Project.
//
// IntegrationTestSiloConfigurator.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Microsoft.Extensions.DependencyInjection;
using Neo;
using Neo.Orleans.Hosting;
using Neo.Orleans.Services;
using Neo.Orleans.Tests;
using Orleans.TestingHost;

namespace Neo.Orleans.Tests.Integration
{
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
            siloBuilder.AddMemoryGrainStorage("TaskManagerStore");
            siloBuilder.AddMemoryGrainStorage("TxRouterStore");
            siloBuilder.Services.AddSingleton<IBlockStorageService, InMemoryBlockStorageService>();
            siloBuilder.Services.AddSingleton(new NeoOrleansOptions
            {
                ValidationMode = NeoValidationMode.None,
                ProtocolSettings = TestProtocolSettings.SoleNode,
                NetworkMagic = TestProtocolSettings.SoleNode.Network,
                UseMemoryStorage = true
            });
            siloBuilder.Services.AddSingleton(sp =>
            {
                var options = sp.GetRequiredService<NeoOrleansOptions>();
                return new NeoSystem(options.ProtocolSettings);
            });
        }
    }
}
