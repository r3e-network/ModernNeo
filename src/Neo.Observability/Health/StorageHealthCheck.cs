// Copyright (C) 2015-2025 The Neo Project.
//
// StorageHealthCheck.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Neo.Observability.Health
{
    /// <summary>
    /// Health check for storage layer responsiveness.
    /// </summary>
    public sealed class StorageHealthCheck : IHealthCheck
    {
        private readonly Func<bool> _checkStorageAccess;
        private readonly TimeSpan _warningLatency;
        private readonly TimeSpan _criticalLatency;

        /// <inheritdoc/>
        public string Name => "storage";

        /// <summary>
        /// Initializes a new instance of the <see cref="StorageHealthCheck"/> class.
        /// </summary>
        /// <param name="checkStorageAccess">Function that performs a storage access test.</param>
        /// <param name="warningLatency">Latency threshold for degraded status.</param>
        /// <param name="criticalLatency">Latency threshold for unhealthy status.</param>
        public StorageHealthCheck(
            Func<bool> checkStorageAccess,
            TimeSpan? warningLatency = null,
            TimeSpan? criticalLatency = null)
        {
            _checkStorageAccess = checkStorageAccess ?? throw new ArgumentNullException(nameof(checkStorageAccess));
            _warningLatency = warningLatency ?? TimeSpan.FromMilliseconds(100);
            _criticalLatency = criticalLatency ?? TimeSpan.FromMilliseconds(500);
        }

        /// <inheritdoc/>
        public Task<HealthCheckResult> CheckHealthAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var sw = Stopwatch.StartNew();
                var success = _checkStorageAccess();
                sw.Stop();

                if (!success)
                {
                    return Task.FromResult(HealthCheckResult.Unhealthy("Storage access failed"));
                }

                if (sw.Elapsed >= _criticalLatency)
                {
                    return Task.FromResult(HealthCheckResult.Unhealthy(
                        $"Storage latency critical: {sw.ElapsedMilliseconds}ms"));
                }

                if (sw.Elapsed >= _warningLatency)
                {
                    return Task.FromResult(HealthCheckResult.Degraded(
                        $"Storage latency elevated: {sw.ElapsedMilliseconds}ms"));
                }

                return Task.FromResult(HealthCheckResult.Healthy(
                    $"Storage responsive: {sw.ElapsedMilliseconds}ms"));
            }
            catch (Exception ex)
            {
                return Task.FromResult(HealthCheckResult.Unhealthy($"Storage check failed: {ex.Message}"));
            }
        }
    }
}
