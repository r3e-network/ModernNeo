// Copyright (C) 2015-2025 The Neo Project.
//
// BlockchainHealthCheck.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Neo.Observability.Health
{
    /// <summary>
    /// Health check for blockchain synchronization status.
    /// </summary>
    public sealed class BlockchainHealthCheck : IHealthCheck
    {
        private readonly Func<uint> _getCurrentHeight;
        private readonly Func<uint> _getHeaderHeight;
        private readonly uint _maxBlockLag;

        /// <inheritdoc/>
        public string Name => "blockchain";

        /// <summary>
        /// Initializes a new instance of the <see cref="BlockchainHealthCheck"/> class.
        /// </summary>
        /// <param name="getCurrentHeight">Function to get current block height.</param>
        /// <param name="getHeaderHeight">Function to get current header height.</param>
        /// <param name="maxBlockLag">Maximum acceptable block lag before degraded status.</param>
        public BlockchainHealthCheck(
            Func<uint> getCurrentHeight,
            Func<uint> getHeaderHeight,
            uint maxBlockLag = 10)
        {
            _getCurrentHeight = getCurrentHeight ?? throw new ArgumentNullException(nameof(getCurrentHeight));
            _getHeaderHeight = getHeaderHeight ?? throw new ArgumentNullException(nameof(getHeaderHeight));
            _maxBlockLag = maxBlockLag;
        }

        /// <inheritdoc/>
        public Task<HealthCheckResult> CheckHealthAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var currentHeight = _getCurrentHeight();
                var headerHeight = _getHeaderHeight();
                var lag = headerHeight > currentHeight ? headerHeight - currentHeight : 0;

                if (lag == 0)
                {
                    return Task.FromResult(HealthCheckResult.Healthy(
                        $"Blockchain synced at height {currentHeight}"));
                }

                if (lag <= _maxBlockLag)
                {
                    return Task.FromResult(HealthCheckResult.Degraded(
                        $"Blockchain syncing: {currentHeight}/{headerHeight} ({lag} blocks behind)"));
                }

                return Task.FromResult(HealthCheckResult.Unhealthy(
                    $"Blockchain significantly behind: {currentHeight}/{headerHeight} ({lag} blocks behind)"));
            }
            catch (Exception ex)
            {
                return Task.FromResult(HealthCheckResult.Unhealthy($"Failed to check blockchain: {ex.Message}"));
            }
        }
    }
}
