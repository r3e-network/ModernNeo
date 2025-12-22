# Consistency & Correctness Review - Development Plan

## Overview
Fix critical code consistency issues, add missing tests, and document architecture concerns.

## Priority: CRITICAL

### Task 1: Fix Async Void Methods
**ID**: CONSIST-001
**Priority**: CRITICAL
**Description**: Convert async void methods to async Task to prevent unhandled exceptions
**File Scope**:
- `src/Neo/SmartContract/ApplicationEngine.Contract.cs` (lines 152, 174)
- `src/Neo.Consensus/DbftStateMachine.cs` (line 190)
- `src/Neo.Plugins/PluginManager.cs` (lines 806, 836, 853)
- `src/Neo.Plugins/PluginConfigurationProvider.cs` (line 254)
**Dependencies**: None
**Test Command**: `dotnet test tests/Neo.UnitTests --filter "FullyQualifiedName~SmartContract"`
**Deliverables**:
- Convert 7 async void methods to async Task
- Add proper exception handling
- Ensure callers await properly

### Task 2: Fix Blocking Async Calls
**ID**: CONSIST-002
**Priority**: CRITICAL
**Description**: Replace .Result blocking calls with proper async/await
**File Scope**:
- `src/Neo.Ledger/BlockchainWriter.cs` (lines 115, 121, 144)
**Dependencies**: None
**Test Command**: `dotnet test tests/Neo.UnitTests --filter "FullyQualifiedName~Ledger"`
**Deliverables**:
- Convert 3 .Result calls to await
- Make calling methods async if needed
- Add ConfigureAwait(false) where appropriate

### Task 3: Add Missing Critical Tests - BlockQueryService
**ID**: CONSIST-003
**Priority**: HIGH
**Description**: Add tests for Neo.Services.Blocks.BlockQueryService
**File Scope**:
- `tests/Neo.UnitTests/Services/UT_BlockQueryService.cs` (NEW)
**Dependencies**: None
**Test Command**: `dotnet test tests/Neo.UnitTests --filter "FullyQualifiedName~UT_BlockQueryService"`
**Deliverables**:
- Test GetBlockByIndex()
- Test GetBlockByHash()
- Test error handling
- Coverage ≥90%

### Task 4: Add Missing Critical Tests - NodeInfoService
**ID**: CONSIST-004
**Priority**: HIGH
**Description**: Add tests for Neo.Services.NodeInfo.NodeInfoService
**File Scope**:
- `tests/Neo.UnitTests/Services/UT_NodeInfoService.cs` (NEW)
**Dependencies**: None
**Test Command**: `dotnet test tests/Neo.UnitTests --filter "FullyQualifiedName~UT_NodeInfoService"`
**Deliverables**:
- Test network info retrieval
- Test node status queries
- Test mempool statistics
- Coverage ≥90%

### Task 5: Fix Private Field Naming Conventions
**ID**: CONSIST-005
**Priority**: LOW
**Description**: Fix private field naming to follow _camelCase convention
**File Scope**:
- `src/Neo/Network/P2P/TaskManager.cs` (line 62)
- `src/Neo/SmartContract/ApplicationEngine.Runtime.cs` (line 44)
- `src/Neo/Network/P2P/Connection.cs` (line 52)
**Dependencies**: None
**Test Command**: `dotnet build`
**Deliverables**:
- Rename 3 private fields to _camelCase
- Update all references

## Priority: DOCUMENTATION

### Task 6: Document Architecture Concerns
**ID**: CONSIST-006
**Priority**: MEDIUM
**Description**: Document known architecture issues for future refactoring
**File Scope**:
- `docs/ARCHITECTURE.md` (UPDATE)
**Dependencies**: None
**Test Command**: N/A
**Deliverables**:
- Document Neo.Node.Core "God Object" issue
- Document circular dependency between Neo.Protocol and Neo.Protocol.Payloads
- Add refactoring recommendations

## Execution Order

```
Parallel Group 1 (Independent - CRITICAL):
├── CONSIST-001: Fix Async Void Methods
├── CONSIST-002: Fix Blocking Async Calls
└── CONSIST-005: Fix Private Field Naming

Parallel Group 2 (Independent - HIGH):
├── CONSIST-003: Add BlockQueryService Tests
└── CONSIST-004: Add NodeInfoService Tests

Sequential (After Group 1 & 2):
└── CONSIST-006: Document Architecture Concerns
```

## Success Criteria
- All 6 tasks completed
- 0 async void methods in production code
- 0 .Result blocking calls
- New test files achieve ≥90% coverage
- All existing tests pass
- Build succeeds with 0 warnings
