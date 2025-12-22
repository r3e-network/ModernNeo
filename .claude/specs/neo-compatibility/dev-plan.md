# Neo Compatibility Verification - Development Plan

## Overview
Ensure ModernNeo correctness and compatibility with origin Neo blockchain.

## Tasks

### Task 1: Fix ConsensusGrain Message Type Codes
**ID**: COMPAT-001
**Priority**: CRITICAL
**Description**: Fix incompatible message type codes in ConsensusGrain to match DbftStateMachine
**File Scope**:
- `src/Neo.Orleans/Grains/ConsensusGrain.cs`
- `tests/Neo.Orleans.Tests/Grains/ConsensusGrainTests.cs`
**Dependencies**: None
**Test Command**: `dotnet test tests/Neo.Orleans.Tests --filter "FullyQualifiedName~ConsensusGrain"`
**Deliverables**:
- Fix message type codes: Commit (0x30), ChangeView (0x00)
- Add compatibility tests verifying message format matches DbftStateMachine
- Coverage ≥90%

### Task 2: Add LedgerContract Tests
**ID**: COMPAT-002
**Priority**: CRITICAL
**Description**: Create comprehensive tests for LedgerContract native contract
**File Scope**:
- `tests/Neo.UnitTests/SmartContract/Native/UT_LedgerContract.cs` (NEW)
**Dependencies**: None
**Test Command**: `dotnet test tests/Neo.UnitTests --filter "FullyQualifiedName~UT_LedgerContract"`
**Deliverables**:
- Test block/transaction persistence
- Test CurrentIndex/CurrentHash operations
- Test conflict tracking (HF_Echidna)
- Coverage ≥90%

### Task 3: Add ContractManagement Tests
**ID**: COMPAT-003
**Priority**: CRITICAL
**Description**: Create comprehensive tests for ContractManagement native contract
**File Scope**:
- `tests/Neo.UnitTests/SmartContract/Native/UT_ContractManagement.cs` (NEW)
**Dependencies**: None
**Test Command**: `dotnet test tests/Neo.UnitTests --filter "FullyQualifiedName~UT_ContractManagement"`
**Deliverables**:
- Test deploy/update/destroy operations
- Test hardfork-based script validation
- Test contract state management
- Coverage ≥90%

### Task 4: Add Protocol Serialization Compatibility Tests
**ID**: COMPAT-004
**Priority**: HIGH
**Description**: Add cross-version serialization tests comparing with origin Neo format
**File Scope**:
- `tests/Neo.UnitTests/Network/P2P/Payloads/UT_ProtocolCompatibility.cs` (NEW)
**Dependencies**: None
**Test Command**: `dotnet test tests/Neo.UnitTests --filter "FullyQualifiedName~UT_ProtocolCompatibility"`
**Deliverables**:
- Golden vector tests for all major payloads
- Variable-length encoding edge cases
- Message compression interoperability tests
- Coverage ≥90%

### Task 5: Fix Consensus Threshold Calculation
**ID**: COMPAT-005
**Priority**: HIGH
**Description**: Ensure consistent threshold calculation between DbftStateMachine and ConsensusGrain
**File Scope**:
- `src/Neo.Orleans/Grains/ConsensusGrain.cs`
- `src/Neo.Consensus/DbftStateMachine.cs`
- `tests/Neo.UnitTests/Consensus/UT_ConsensusThreshold.cs` (NEW)
**Dependencies**: COMPAT-001
**Test Command**: `dotnet test tests/Neo.UnitTests --filter "FullyQualifiedName~UT_ConsensusThreshold"`
**Deliverables**:
- Unify threshold calculation formula
- Add edge case tests (N=4, N=7, N=21, N=100)
- Verify M = N - (N-1)/3 formula
- Coverage ≥90%

## Execution Order

```
Parallel Group 1 (Independent):
├── COMPAT-001: Fix ConsensusGrain Message Types
├── COMPAT-002: Add LedgerContract Tests
├── COMPAT-003: Add ContractManagement Tests
└── COMPAT-004: Add Protocol Compatibility Tests

Sequential (After COMPAT-001):
└── COMPAT-005: Fix Consensus Threshold Calculation
```

## Success Criteria
- All 5 tasks completed
- Each task achieves ≥90% coverage
- All tests pass
- No compatibility regressions
