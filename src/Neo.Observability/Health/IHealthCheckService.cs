// Copyright (C) 2015-2025 The Neo Project.
//
// IHealthCheckService.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Neo.Observability.Health
{
    /// <summary>
    /// Provides health check aggregation and reporting for the Neo node.
    /// </summary>
    public interface IHealthCheckService
    {
        /// <summary>
        /// Registers a health check with the service.
        /// </summary>
        /// <param name="healthCheck">The health check to register.</param>
        void Register(IHealthCheck healthCheck);

        /// <summary>
        /// Runs all registered health checks.
        /// </summary>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A dictionary of health check names to their results.</returns>
        Task<IReadOnlyDictionary<string, HealthCheckResult>> CheckAllAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the overall health status based on all registered checks.
        /// </summary>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>The aggregate health status.</returns>
        Task<HealthStatus> GetOverallStatusAsync(CancellationToken cancellationToken = default);
    }
}
