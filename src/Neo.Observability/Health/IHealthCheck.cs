// Copyright (C) 2015-2025 The Neo Project.
//
// IHealthCheck.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using System.Threading;
using System.Threading.Tasks;

namespace Neo.Observability.Health
{
    /// <summary>
    /// Represents a health check for a component of the Neo node.
    /// </summary>
    public interface IHealthCheck
    {
        /// <summary>
        /// Gets the name of the health check.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Performs the health check.
        /// </summary>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>The result of the health check.</returns>
        Task<HealthCheckResult> CheckHealthAsync(CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Represents the result of a health check.
    /// </summary>
    public readonly struct HealthCheckResult
    {
        /// <summary>
        /// Gets the status of the health check.
        /// </summary>
        public HealthStatus Status { get; }

        /// <summary>
        /// Gets an optional description of the health check result.
        /// </summary>
        public string? Description { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="HealthCheckResult"/> struct.
        /// </summary>
        /// <param name="status">The health status.</param>
        /// <param name="description">An optional description.</param>
        public HealthCheckResult(HealthStatus status, string? description = null)
        {
            Status = status;
            Description = description;
        }

        /// <summary>
        /// Creates a healthy result.
        /// </summary>
        /// <param name="description">An optional description.</param>
        /// <returns>A healthy result.</returns>
        public static HealthCheckResult Healthy(string? description = null) =>
            new(HealthStatus.Healthy, description);

        /// <summary>
        /// Creates a degraded result.
        /// </summary>
        /// <param name="description">An optional description.</param>
        /// <returns>A degraded result.</returns>
        public static HealthCheckResult Degraded(string? description = null) =>
            new(HealthStatus.Degraded, description);

        /// <summary>
        /// Creates an unhealthy result.
        /// </summary>
        /// <param name="description">An optional description.</param>
        /// <returns>An unhealthy result.</returns>
        public static HealthCheckResult Unhealthy(string? description = null) =>
            new(HealthStatus.Unhealthy, description);
    }

    /// <summary>
    /// Represents the health status of a component.
    /// </summary>
    public enum HealthStatus
    {
        /// <summary>
        /// The component is healthy.
        /// </summary>
        Healthy,

        /// <summary>
        /// The component is degraded but still functional.
        /// </summary>
        Degraded,

        /// <summary>
        /// The component is unhealthy.
        /// </summary>
        Unhealthy
    }
}
