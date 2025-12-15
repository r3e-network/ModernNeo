# Phase 3: Complete Modular Refactoring Plan

## Overview
Complete migration of all code from monolithic `src/Neo/` to modular architecture following NeoAN design.

## Current State
- `src/Neo/`: 217 .cs files (monolithic)
- New modules created but mostly empty shells

## Target Architecture (NeoAN)

```
Application Layer:
  └── Neo.Node (entry point, hosting, config)

Service Layer:
  ├── Neo.Wallet (wallet management)
  └── Neo.Plugins (plugin system)

Core Layer:
  ├── Neo.Execution (smart contract VM, syscalls)
  ├── Neo.Ledger (blockchain state, blocks, transactions)
  └── Neo.TxPool (transaction pool - part of Ledger for now)

Infrastructure Layer:
  ├── Neo.Network (P2P, message handling)
  ├── Neo.Storage (persistence abstraction)
  ├── Neo.Cryptography (crypto primitives)
  └── Neo.Observability (metrics, logging, tracing)

Base Layer:
  └── Neo.Core (primitives, types, utilities)
```

## Migration Tasks

### Task 1: Neo.Network (61 files)
- **ID**: T1-NETWORK
- **Source**: `src/Neo/Network/`
- **Target**: `src/Neo.Network/`
- **Files**: P2P/, Payloads/, all network-related code
- **Dependencies**: Neo.Core, Neo.Cryptography, Neo.IO
- **Risk**: HIGH (P2P compatibility critical)

### Task 2: Neo.Execution (81 files)
- **ID**: T2-EXECUTION
- **Source**: `src/Neo/SmartContract/`
- **Target**: `src/Neo.Execution/`
- **Files**: ApplicationEngine, Native contracts, Iterators, Manifest
- **Dependencies**: Neo.Core, Neo.Cryptography, Neo.Storage, Neo.Network
- **Risk**: HIGH (VM execution critical)

### Task 3: Neo.Ledger (12 files)
- **ID**: T3-LEDGER
- **Source**: `src/Neo/Ledger/`
- **Target**: `src/Neo.Ledger/`
- **Files**: Blockchain, MemoryPool, HeaderCache, TransactionVerificationContext
- **Dependencies**: Neo.Core, Neo.Storage, Neo.Network
- **Risk**: HIGH (state consistency critical)

### Task 4: Neo.Wallet (13 files)
- **ID**: T4-WALLET
- **Source**: `src/Neo/Wallets/`
- **Target**: `src/Neo.Wallet/`
- **Files**: Wallet, WalletAccount, NEP6, SQLite wallets
- **Dependencies**: Neo.Core, Neo.Cryptography
- **Risk**: MEDIUM

### Task 5: Neo.Core Extensions (42 files)
- **ID**: T5-CORE
- **Source**: `src/Neo/Extensions/`, `src/Neo/Builders/`, `src/Neo/IO/`, `src/Neo/IEventHandlers/`, `src/Neo/Sign/`
- **Target**: `src/Neo.Core/`
- **Dependencies**: None (base layer)
- **Risk**: LOW

### Task 6: Cleanup & TypeForwards
- **ID**: T6-CLEANUP
- **Source**: `src/Neo/`
- **Action**: Remove migrated files, update TypeForwards.cs
- **Dependencies**: T1-T5 complete
- **Risk**: MEDIUM

## Execution Strategy

### Phase 3.1: Infrastructure First (Parallel)
```
T5-CORE ────────┐
                ├──> Verify Build
T1-NETWORK ─────┘
```

### Phase 3.2: Core Layer (Sequential)
```
T3-LEDGER ──> T2-EXECUTION ──> Verify Build
```

### Phase 3.3: Service Layer
```
T4-WALLET ──> T6-CLEANUP ──> Full Test Suite
```

## TypeForwarding Strategy

For each migrated type, add to `src/Neo/TypeForwards.cs`:
```csharp
[assembly: TypeForwardedTo(typeof(Neo.Network.P2P.LocalNode))]
[assembly: TypeForwardedTo(typeof(Neo.SmartContract.ApplicationEngine))]
// etc.
```

## Compatibility Requirements
- All 1168 existing tests must pass
- Binary compatibility via TypeForwarding
- No changes to serialization formats
- P2P protocol unchanged

## Risk Mitigation
1. Migrate one module at a time
2. Run full test suite after each migration
3. Keep TypeForwards updated
4. Preserve all namespaces
