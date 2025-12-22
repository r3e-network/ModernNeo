// Copyright (C) 2015-2025 The Neo Project.
//
// HealthCheckService.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Neo.Observability.Health
{
    /// <summary>
    /// Default implementation of <see cref="IHealthCheckService"/> that aggregates
    /// multiple health checks and provides overall health status.
    /// </summary>
    public sealed class HealthCheckService : IHealthCheckService
    {
        private readonly ConcurrentDictionary<string, IHealthCheck> _healthChecks = new();
        private readonly TimeSpan _timeout;

        /// <summary>
        /// Initializes a new instance of the <see cref="HealthCheckService"/> class.
        /// </summary>
        /// <param name="timeout">Timeout for individual health checks. Default is 30 seconds.</param>
        public HealthCheckService(TimeSpan? timeout = null)
        {
            _timeout = timeout ?? TimeSpan.FromSeconds(30);
        }

        /// <inheritdoc/>
        public void Register(IHealthCheck healthCheck)
        {
            ArgumentNullException.ThrowIfNull(healthCheck);
            _healthChecks[healthCheck.Name] = healthCheck;
        }

        /// <inheritdoc/>
        public async Task<IReadOnlyDictionary<string, HealthCheckResult>> CheckAllAsync(
            CancellationToken cancellationToken = default)
        {
            var results = new Dictionary<string, HealthCheckResult>();
            var tasks = new List<Task<(string Name, HealthCheckResult Result)>>();

            foreach (var (name, check) in _healthChecks)
            {
                tasks.Add(ExecuteHealthCheckAsync(name, check, cancellationToken));
            }

            var completedTasks = await Task.WhenAll(tasks);

            foreach (var (name, result) in completedTasks)
            {
                results[name] = result;
            }

            return results;
        }

        /// <inheritdoc/>
        public async Task<HealthStatus> GetOverallStatusAsync(CancellationToken cancellationToken = default)
        {
            var results = await CheckAllAsync(cancellationToken);

            if (results.Count == 0)
                return HealthStatus.Healthy;

            if (results.Values.Any(r => r.Status == HealthStatus.Unhealthy))
                return HealthStatus.Unhealthy;

            if (results.Values.Any(r => r.Status == HealthStatus.Degraded))
                return HealthStatus.Degraded;

            return HealthStatus.Healthy;
        }

        private async Task<(string Name, HealthCheckResult Result)> ExecuteHealthCheckAsync(
            string name,
            IHealthCheck check,
            CancellationToken cancellationToken)
        {
            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(_timeout);

                var result = await check.CheckHealthAsync(cts.Token);
                return (name, result);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return (name, HealthCheckResult.Unhealthy($"Health check timed out after {_timeout.TotalSeconds}s"));
            }
            catch (Exception ex)
            {
                return (name, HealthCheckResult.Unhealthy($"Health check failed: {ex.Message}"));
            }
        }
    }
}
