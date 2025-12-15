# Neo.Node MVP Development Plan

## Overview
Enhance Neo.Node to be a production-ready minimal viable node with health checks, observability, and structured logging.

## Context & Constraints
- Tech stack: .NET 10.0, Microsoft.Extensions.Hosting
- Existing: Basic Program.cs with NeoSystem initialization and P2P startup
- Constraint: Must maintain backward compatibility with existing config.json format

## Codebase Exploration
- `src/Neo.Node/Program.cs` - Entry point with NeoSystemNodeFactory
- `src/Neo.Observability/` - Metrics infrastructure (DiagnosticsMetricsProvider)
- `src/Neo/NeoSystem.cs` - Core system with Akka actors

## Technical Decisions
1. Use Microsoft.Extensions.Hosting for lifecycle management
2. Add /health endpoint via minimal API (Kestrel)
3. Integrate OpenTelemetry for metrics export (Prometheus format)
4. Use structured logging via Microsoft.Extensions.Logging

## Task Breakdown

### Task 1: Health Check Endpoint
- **ID**: T1-HEALTH
- **Description**: Add HTTP health check endpoint at /health
- **File Scope**:
  - MODIFY: `src/Neo.Node/Neo.Node.csproj` (add ASP.NET Core packages)
  - MODIFY: `src/Neo.Node/Program.cs` (add health endpoint)
- **Dependencies**: None
- **Test Command**: `curl http://localhost:5000/health`

### Task 2: OpenTelemetry Metrics Export
- **ID**: T2-OTEL
- **Description**: Export metrics via Prometheus endpoint at /metrics
- **File Scope**:
  - MODIFY: `src/Neo.Node/Neo.Node.csproj` (add OpenTelemetry packages)
  - MODIFY: `src/Neo.Node/Program.cs` (configure metrics export)
- **Dependencies**: T1-HEALTH (shares HTTP host)
- **Test Command**: `curl http://localhost:5000/metrics`

### Task 3: Structured Logging
- **ID**: T3-LOGGING
- **Description**: Replace console logging with structured logging
- **File Scope**:
  - MODIFY: `src/Neo.Node/Program.cs` (configure ILogger)
- **Dependencies**: None
- **Test Command**: `dotnet run` and verify JSON log output

### Task 4: Unit Tests
- **ID**: T4-TESTS
- **Description**: Add unit tests for Neo.Node components
- **File Scope**:
  - NEW: `tests/Neo.Node.Tests/Neo.Node.Tests.csproj`
  - NEW: `tests/Neo.Node.Tests/UT_NeoSystemNodeFactory.cs`
  - NEW: `tests/Neo.Node.Tests/UT_ChannelsConfigFactory.cs`
- **Dependencies**: T1, T2, T3
- **Test Command**: `dotnet test tests/Neo.Node.Tests`

## Execution Order
```
T1-HEALTH ──────┐
                ├──> T4-TESTS
T3-LOGGING ─────┤
                │
T2-OTEL ────────┘ (depends on T1)
```

## Coverage Requirements
- Each task must achieve ≥90% code coverage for new code
- Existing functionality must continue to work
