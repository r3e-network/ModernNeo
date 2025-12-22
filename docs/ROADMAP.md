# ModernNeo Roadmap (18 months)

This roadmap mirrors the NeoAN plan and tracks repository alignment. Phases emphasize compatibility first, then performance and new features.

## Phase 1: Infrastructure (Months 1–6)

- Project structure, CI/CD, test harness
- Core primitives (`Neo.Core`) and crypto (`Neo.Cryptography`)
- Storage abstractions + RocksDB/LevelDB providers (`Neo.Storage`)
- Multi-tier state cache, snapshot manager
- Network TCP compatibility layer; connection and message routing optimizations
- Milestone: sync with existing network

## Phase 2: Core Optimization (Months 7–12)

- VM optimizations and hotspots
- Parallel execution pipeline and batch signature verification (`Neo.Execution`)
- Smart mempool with priority queues + dynamic fee estimation (`Neo.Ledger/TxPool`)
- Fast sync
- dBFT state machine optimizations, RPC service layer, observability
- Milestone: eligible for consensus participation

## Phase 3: Advanced Features (Months 13–18)

- QUIC transport for modern node peers (`Neo.Network`)
- Incremental state sync and improved discovery
- State pruning and historical archive tier
- MPT proofs (subject to NEP) and advanced storage features
- Production readiness: performance tuning, security audit, documentation, mainnet deployment

## Status in Repo

- Modules present for: Core, Crypto, Storage, Network (including QUIC stubs), Execution, Ledger, Consensus, Observability, RPC/gRPC, Plugins, Node host.
- ✅ GraphQL endpoint implemented with 13 query endpoints (blocks, transactions, node info)
- ✅ Solution file `neo.sln` updated to include all module csprojs
- Neo.Services layer expanded with BlockQueryService, NodeInfoService, TransactionQueryService
- Pending: richer Neo.Services layer (account queries, contract queries, event subscriptions)
