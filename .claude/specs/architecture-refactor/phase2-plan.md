# Architecture Refactoring - Phase 2: Break Circular Dependencies

## Overview
Create Neo.Ledger.Abstractions and begin breaking the Neo ↔ Ledger circular dependency.

## Phase 2 Scope

### Task 1: Create Neo.Ledger.Abstractions Project
**ID**: ARCH-006
**Priority**: CRITICAL
**Description**: Create ledger-specific abstraction interfaces
**File Scope**:
- `src/Neo.Ledger.Abstractions/Neo.Ledger.Abstractions.csproj` (NEW)
- `src/Neo.Ledger.Abstractions/IBlockExecutionStrategy.cs` (NEW)
- `src/Neo.Ledger.Abstractions/IBlockPersistenceStrategy.cs` (NEW)
- `src/Neo.Ledger.Abstractions/IBlockchainStateProvider.cs` (NEW)
- `src/Neo.Ledger.Abstractions/IHeaderCache.cs` (NEW)
**Dependencies**: Neo.Core.Abstractions only
**Test Command**: `dotnet build src/Neo.Ledger.Abstractions`

### Task 2: Update Neo.Core to Reference Abstractions
**ID**: ARCH-007
**Priority**: HIGH
**Description**: Make Neo.Core implement Neo.Core.Abstractions interfaces
**File Scope**:
- `src/Neo.Core/Neo.Core.csproj` (UPDATE)
- `src/Neo.Core/Interfaces/IBlockData.cs` (UPDATE - implement interface)
**Dependencies**: ARCH-006
**Test Command**: `dotnet build src/Neo.Core`

### Task 3: Add Ledger Abstractions Tests
**ID**: ARCH-008
**Priority**: HIGH
**Description**: Create tests for ledger abstractions
**File Scope**:
- `tests/Neo.Ledger.Abstractions.Tests/` (NEW)
**Dependencies**: ARCH-006
**Test Command**: `dotnet test tests/Neo.Ledger.Abstractions.Tests`

## Execution Order
```
Sequential:
└── ARCH-006: Create Neo.Ledger.Abstractions

Parallel (After ARCH-006):
├── ARCH-007: Update Neo.Core
└── ARCH-008: Add Tests
```
