// Copyright (C) 2015-2025 The Neo Project.
//
// ConsensusHealthCheck.cs file belongs to the neo project and is free
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
    /// Health check for consensus participation status.
    /// </summary>
    public sealed class ConsensusHealthCheck : IHealthCheck
    {
        private readonly Func<bool> _isConsensusEnabled;
        private readonly Func<bool> _isConsensusActive;
        private readonly Func<int> _getViewNumber;
        private readonly int _maxViewNumber;

        /// <inheritdoc/>
        public string Name => "consensus";

        /// <summary>
        /// Initializes a new instance of the <see cref="ConsensusHealthCheck"/> class.
        /// </summary>
        /// <param name="isConsensusEnabled">Function to check if consensus is enabled.</param>
        /// <param name="isConsensusActive">Function to check if consensus is actively participating.</param>
        /// <param name="getViewNumber">Function to get current view number.</param>
        /// <param name="maxViewNumber">Maximum view number before degraded status.</param>
        public ConsensusHealthCheck(
            Func<bool> isConsensusEnabled,
            Func<bool> isConsensusActive,
            Func<int> getViewNumber,
            int maxViewNumber = 3)
        {
            _isConsensusEnabled = isConsensusEnabled ?? throw new ArgumentNullException(nameof(isConsensusEnabled));
            _isConsensusActive = isConsensusActive ?? throw new ArgumentNullException(nameof(isConsensusActive));
            _getViewNumber = getViewNumber ?? throw new ArgumentNullException(nameof(getViewNumber));
            _maxViewNumber = maxViewNumber;
        }

        /// <inheritdoc/>
        public Task<HealthCheckResult> CheckHealthAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                if (!_isConsensusEnabled())
                {
                    return Task.FromResult(HealthCheckResult.Healthy("Consensus not enabled (observer mode)"));
                }

                if (!_isConsensusActive())
                {
                    return Task.FromResult(HealthCheckResult.Degraded("Consensus enabled but not active"));
                }

                var viewNumber = _getViewNumber();

                if (viewNumber > _maxViewNumber)
                {
                    return Task.FromResult(HealthCheckResult.Degraded(
                        $"High view number: {viewNumber} (possible network issues)"));
                }

                return Task.FromResult(HealthCheckResult.Healthy(
                    $"Consensus active, view {viewNumber}"));
            }
            catch (Exception ex)
            {
                return Task.FromResult(HealthCheckResult.Unhealthy($"Consensus check failed: {ex.Message}"));
            }
        }
    }
}
