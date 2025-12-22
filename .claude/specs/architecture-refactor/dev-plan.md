# Architecture Refactoring - Phase 1: Foundation Abstractions

## Overview
Create foundational abstraction modules to break circular dependencies and enable layered architecture.

## Phase 1 Scope (This Sprint)
Focus on creating `Neo.Core.Abstractions` - the foundation layer with zero dependencies.

## Tasks

### Task 1: Create Neo.Core.Abstractions Project
**ID**: ARCH-001
**Priority**: CRITICAL
**Description**: Create new project with core domain interfaces
**File Scope**:
- `src/Neo.Core.Abstractions/Neo.Core.Abstractions.csproj` (NEW)
- `src/Neo.Core.Abstractions/IBlockData.cs` (NEW)
- `src/Neo.Core.Abstractions/ITransactionData.cs` (NEW)
- `src/Neo.Core.Abstractions/IHeaderData.cs` (NEW)
- `src/Neo.Core.Abstractions/IVerifiable.cs` (NEW)
- `src/Neo.Core.Abstractions/IInventory.cs` (NEW)
**Dependencies**: None
**Test Command**: `dotnet build src/Neo.Core.Abstractions`
**Deliverables**:
- New project with zero external dependencies
- Core data interfaces extracted from Neo.Core
- XML documentation for all interfaces

### Task 2: Add Blockchain Abstractions
**ID**: ARCH-002
**Priority**: CRITICAL
**Description**: Add blockchain query and mutation interfaces
**File Scope**:
- `src/Neo.Core.Abstractions/Blockchain/IBlockchainQuery.cs` (NEW)
- `src/Neo.Core.Abstractions/Blockchain/IBlockchainMutator.cs` (NEW)
- `src/Neo.Core.Abstractions/Blockchain/IBlockValidator.cs` (NEW)
- `src/Neo.Core.Abstractions/Blockchain/ITransactionValidator.cs` (NEW)
**Dependencies**: ARCH-001
**Test Command**: `dotnet build src/Neo.Core.Abstractions`
**Deliverables**:
- Blockchain read/write abstractions
- Validation interfaces

### Task 3: Add Storage Abstractions
**ID**: ARCH-003
**Priority**: HIGH
**Description**: Add storage snapshot and mutation interfaces
**File Scope**:
- `src/Neo.Core.Abstractions/Storage/IStorageSnapshot.cs` (NEW)
- `src/Neo.Core.Abstractions/Storage/IStorageMutator.cs` (NEW)
- `src/Neo.Core.Abstractions/Storage/IStorageKey.cs` (NEW)
- `src/Neo.Core.Abstractions/Storage/IStorageItem.cs` (NEW)
**Dependencies**: ARCH-001
**Test Command**: `dotnet build src/Neo.Core.Abstractions`
**Deliverables**:
- Storage abstractions independent of implementation

### Task 4: Add MemoryPool Abstractions
**ID**: ARCH-004
**Priority**: HIGH
**Description**: Add memory pool query and mutation interfaces
**File Scope**:
- `src/Neo.Core.Abstractions/MemPool/IMemoryPoolQuery.cs` (NEW)
- `src/Neo.Core.Abstractions/MemPool/IMemoryPoolMutator.cs` (NEW)
**Dependencies**: ARCH-001
**Test Command**: `dotnet build src/Neo.Core.Abstractions`
**Deliverables**:
- MemPool abstractions for transaction pool access

### Task 5: Add Unit Tests for Abstractions
**ID**: ARCH-005
**Priority**: HIGH
**Description**: Create test project verifying interface contracts
**File Scope**:
- `tests/Neo.Core.Abstractions.Tests/Neo.Core.Abstractions.Tests.csproj` (NEW)
- `tests/Neo.Core.Abstractions.Tests/InterfaceContractTests.cs` (NEW)
**Dependencies**: ARCH-001, ARCH-002, ARCH-003, ARCH-004
**Test Command**: `dotnet test tests/Neo.Core.Abstractions.Tests`
**Deliverables**:
- Interface contract verification tests
- Coverage ≥90%

## Execution Order

```
Sequential (Foundation):
└── ARCH-001: Create Neo.Core.Abstractions Project

Parallel Group 1 (After ARCH-001):
├── ARCH-002: Add Blockchain Abstractions
├── ARCH-003: Add Storage Abstractions
└── ARCH-004: Add MemoryPool Abstractions

Sequential (After Group 1):
└── ARCH-005: Add Unit Tests
```

## Success Criteria
- Neo.Core.Abstractions has ZERO project dependencies
- All interfaces have XML documentation
- Build succeeds with 0 warnings
- Tests pass with ≥90% coverage

## Next Phase Preview
Phase 2 will:
- Create Neo.Ledger.Abstractions
- Update Neo.Core to reference Neo.Core.Abstractions
- Begin breaking Neo ↔ Ledger circular dependency
