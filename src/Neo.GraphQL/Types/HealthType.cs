// Copyright (C) 2015-2025 The Neo Project.
//
// HealthType.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using GraphQL.Types;
using Neo.Observability.Health;

namespace Neo.GraphQL.Types
{
    /// <summary>
    /// GraphQL type representing overall health status.
    /// </summary>
    public sealed class HealthReportType : ObjectGraphType<HealthReport>
    {
        public HealthReportType()
        {
            Name = "HealthReport";
            Description = "Overall health status of the Neo node";

            Field<NonNullGraphType<HealthStatusEnumType>>("status")
                .Description("Overall health status")
                .Resolve(ctx => ctx.Source.Status);

            Field<NonNullGraphType<ListGraphType<NonNullGraphType<HealthCheckEntryType>>>>("checks")
                .Description("Individual health check results")
                .Resolve(ctx => ctx.Source.Checks);

            Field<NonNullGraphType<LongGraphType>>("timestamp")
                .Description("Timestamp of the health check (Unix milliseconds)")
                .Resolve(ctx => ctx.Source.Timestamp);
        }
    }

    /// <summary>
    /// GraphQL type representing a single health check entry.
    /// </summary>
    public sealed class HealthCheckEntryType : ObjectGraphType<HealthCheckEntry>
    {
        public HealthCheckEntryType()
        {
            Name = "HealthCheckEntry";
            Description = "Result of an individual health check";

            Field<NonNullGraphType<StringGraphType>>("name")
                .Description("Name of the health check")
                .Resolve(ctx => ctx.Source.Name);

            Field<NonNullGraphType<HealthStatusEnumType>>("status")
                .Description("Health status")
                .Resolve(ctx => ctx.Source.Status);

            Field<StringGraphType>("description")
                .Description("Optional description of the health check result")
                .Resolve(ctx => ctx.Source.Description);
        }
    }

    /// <summary>
    /// GraphQL enum type for health status.
    /// </summary>
    public sealed class HealthStatusEnumType : EnumerationGraphType<HealthStatus>
    {
        public HealthStatusEnumType()
        {
            Name = "HealthStatus";
            Description = "Health status of a component";
        }
    }

    /// <summary>
    /// DTO for health report.
    /// </summary>
    public sealed class HealthReport
    {
        public required HealthStatus Status { get; init; }
        public required HealthCheckEntry[] Checks { get; init; }
        public required long Timestamp { get; init; }
    }

    /// <summary>
    /// DTO for individual health check entry.
    /// </summary>
    public sealed class HealthCheckEntry
    {
        public required string Name { get; init; }
        public required HealthStatus Status { get; init; }
        public string? Description { get; init; }
    }
}
