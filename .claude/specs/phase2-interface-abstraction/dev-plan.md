# Phase 2: Interface Abstraction Development Plan

## Overview
Create abstraction interfaces for SmartContract execution engine to enable future modularization and testability improvements.

## Context & Constraints
- Tech stack: .NET 10.0, Akka actors, Neo.VM
- Existing patterns: IApplicationEngineProvider, IDiagnostic already exist
- Constraint: Must maintain backward compatibility with existing code

## Technical Decisions
1. Create IMemoryPool interface to abstract transaction pool operations
2. Create IBlockchain interface to abstract ledger operations
3. Create IExecutionContext interface for execution state management
4. Integrate with existing Neo.Observability for metrics

## Task Breakdown

### Task 1: IMemoryPool Interface
- **ID**: T1-MEMPOOL
- **Description**: Extract IMemoryPool interface from MemoryPool class
- **File Scope**:
  - NEW: `src/Neo/Ledger/IMemoryPool.cs`
  - MODIFY: `src/Neo/Ledger/MemoryPool.cs`
- **Dependencies**: None
- **Test Command**: `dotnet test tests/Neo.UnitTests --filter "FullyQualifiedName~MemoryPool"`

### Task 2: IBlockchainOperations Interface
- **ID**: T2-BLOCKCHAIN
- **Description**: Create interface for blockchain query operations
- **File Scope**:
  - NEW: `src/Neo/Ledger/IBlockchainOperations.cs`
  - MODIFY: `src/Neo/Ledger/Blockchain.cs`
- **Dependencies**: None
- **Test Command**: `dotnet test tests/Neo.UnitTests --filter "FullyQualifiedName~Blockchain"`

### Task 3: IExecutionMetrics Interface
- **ID**: T3-METRICS
- **Description**: Create execution metrics interface integrating with Neo.Observability
- **File Scope**:
  - NEW: `src/Neo/SmartContract/IExecutionMetrics.cs`
  - MODIFY: `src/Neo/SmartContract/ApplicationEngine.cs`
- **Dependencies**: T1, T2 (for context)
- **Test Command**: `dotnet test tests/Neo.UnitTests --filter "FullyQualifiedName~ApplicationEngine"`

### Task 4: Unit Tests for New Interfaces
- **ID**: T4-TESTS
- **Description**: Add comprehensive unit tests for new interfaces
- **File Scope**:
  - NEW: `tests/Neo.UnitTests/Ledger/UT_IMemoryPool.cs`
  - NEW: `tests/Neo.UnitTests/Ledger/UT_IBlockchainOperations.cs`
  - NEW: `tests/Neo.UnitTests/SmartContract/UT_IExecutionMetrics.cs`
- **Dependencies**: T1, T2, T3
- **Test Command**: `dotnet test tests/Neo.UnitTests --filter "FullyQualifiedName~UT_I"`

## Execution Order
```
T1-MEMPOOL ──────┐
                 ├──> T3-METRICS ──> T4-TESTS
T2-BLOCKCHAIN ───┘
```

## Coverage Requirements
- Each task must achieve ≥90% code coverage for new code
- Existing tests must continue to pass
