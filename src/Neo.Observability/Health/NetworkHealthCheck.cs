// Copyright (C) 2015-2025 The Neo Project.
//
// NetworkHealthCheck.cs file belongs to the neo project and is free
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
    /// Health check for P2P network connectivity.
    /// </summary>
    public sealed class NetworkHealthCheck : IHealthCheck
    {
        private readonly Func<int> _getConnectedPeers;
        private readonly int _minPeers;
        private readonly int _degradedThreshold;

        /// <inheritdoc/>
        public string Name => "network";

        /// <summary>
        /// Initializes a new instance of the <see cref="NetworkHealthCheck"/> class.
        /// </summary>
        /// <param name="getConnectedPeers">Function to get connected peer count.</param>
        /// <param name="minPeers">Minimum peers for healthy status.</param>
        /// <param name="degradedThreshold">Peer count below which status is degraded.</param>
        public NetworkHealthCheck(
            Func<int> getConnectedPeers,
            int minPeers = 3,
            int degradedThreshold = 5)
        {
            _getConnectedPeers = getConnectedPeers ?? throw new ArgumentNullException(nameof(getConnectedPeers));
            _minPeers = minPeers;
            _degradedThreshold = degradedThreshold;
        }

        /// <inheritdoc/>
        public Task<HealthCheckResult> CheckHealthAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var peerCount = _getConnectedPeers();

                if (peerCount >= _degradedThreshold)
                {
                    return Task.FromResult(HealthCheckResult.Healthy(
                        $"Connected to {peerCount} peers"));
                }

                if (peerCount >= _minPeers)
                {
                    return Task.FromResult(HealthCheckResult.Degraded(
                        $"Low peer count: {peerCount} (threshold: {_degradedThreshold})"));
                }

                if (peerCount > 0)
                {
                    return Task.FromResult(HealthCheckResult.Unhealthy(
                        $"Critical: only {peerCount} peer(s) connected (minimum: {_minPeers})"));
                }

                return Task.FromResult(HealthCheckResult.Unhealthy("No peers connected"));
            }
            catch (Exception ex)
            {
                return Task.FromResult(HealthCheckResult.Unhealthy($"Failed to check network: {ex.Message}"));
            }
        }
    }
}
