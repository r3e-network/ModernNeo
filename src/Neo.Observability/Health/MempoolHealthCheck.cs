// Copyright (C) 2015-2025 The Neo Project.
//
// MempoolHealthCheck.cs file belongs to the neo project and is free
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
    /// Health check for transaction mempool status.
    /// </summary>
    public sealed class MempoolHealthCheck : IHealthCheck
    {
        private readonly Func<int> _getMempoolCount;
        private readonly Func<int> _getMaxMempoolSize;
        private readonly double _warningThreshold;
        private readonly double _criticalThreshold;

        /// <inheritdoc/>
        public string Name => "mempool";

        /// <summary>
        /// Initializes a new instance of the <see cref="MempoolHealthCheck"/> class.
        /// </summary>
        /// <param name="getMempoolCount">Function to get current mempool transaction count.</param>
        /// <param name="getMaxMempoolSize">Function to get maximum mempool size.</param>
        /// <param name="warningThreshold">Percentage threshold for degraded status (0.0-1.0).</param>
        /// <param name="criticalThreshold">Percentage threshold for unhealthy status (0.0-1.0).</param>
        public MempoolHealthCheck(
            Func<int> getMempoolCount,
            Func<int> getMaxMempoolSize,
            double warningThreshold = 0.7,
            double criticalThreshold = 0.9)
        {
            _getMempoolCount = getMempoolCount ?? throw new ArgumentNullException(nameof(getMempoolCount));
            _getMaxMempoolSize = getMaxMempoolSize ?? throw new ArgumentNullException(nameof(getMaxMempoolSize));
            _warningThreshold = warningThreshold;
            _criticalThreshold = criticalThreshold;
        }

        /// <inheritdoc/>
        public Task<HealthCheckResult> CheckHealthAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var count = _getMempoolCount();
                var maxSize = _getMaxMempoolSize();

                if (maxSize <= 0)
                {
                    return Task.FromResult(HealthCheckResult.Healthy($"Mempool: {count} transactions"));
                }

                var utilization = (double)count / maxSize;

                if (utilization >= _criticalThreshold)
                {
                    return Task.FromResult(HealthCheckResult.Unhealthy(
                        $"Mempool near capacity: {count}/{maxSize} ({utilization:P0})"));
                }

                if (utilization >= _warningThreshold)
                {
                    return Task.FromResult(HealthCheckResult.Degraded(
                        $"Mempool filling up: {count}/{maxSize} ({utilization:P0})"));
                }

                return Task.FromResult(HealthCheckResult.Healthy(
                    $"Mempool healthy: {count}/{maxSize} ({utilization:P0})"));
            }
            catch (Exception ex)
            {
                return Task.FromResult(HealthCheckResult.Unhealthy($"Failed to check mempool: {ex.Message}"));
            }
        }
    }
}
