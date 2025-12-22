# NeoAN Compliance Checklist (ModernNeo)

This document tracks how the repository aligns with the Neo Advanced Node (NeoAN) architecture and modernization plan.

## Summary

- Architecture layers present and separated:
  - Application: `Neo.Node`, `Neo.RPC`, `Neo.Grpc`, `Neo.GraphQL`
  - Services: `Neo.Services`, `Neo.Plugins`
  - Core: `Neo.Execution`, `Neo.Ledger`, `Neo.TxPool`, `Neo.Consensus`, `Neo.SmartContract*`, `Neo.Protocol*`
  - Infrastructure: `Neo.Network`, `Neo.Storage`, `Neo.Cryptography`, `Neo.IO`, `Neo.Extensions`, `Neo.Observability`
  - Base/Primitives: `Neo.Core`

## Phase 1–2 Compatibility (Required)

- Serialization compatibility (blocks/tx/witness/signer): Maintained
- Hash/signature semantics: Maintained
- Storage keyspace/layout: Maintained (LevelDB/RocksDB providers; `StoreFactory`)
- Network interoperability: Legacy TCP compatible; capability negotiation scaffolding present
- API compatibility: JSON‑RPC kept in `Neo.RPC` and aligned; gRPC/GraphQL are additive
- Type forwarding: Implemented via `[TypeForwardedTo]` for moved types to preserve binary compatibility

References:
- `src/Neo/TypeForwards.cs`
- Storage interfaces and providers under `src/Neo.Storage/**`
- Network compatibility under `src/Neo.Network/P2P/**`

## Observability

- Metrics: OpenTelemetry meters for SmartContract/Network/Ledger, Prometheus export in `Neo.Node`
- Tracing: OpenTelemetry tracer with spans for block execution, consensus, and networking
- Health: `/health` and `/ready` endpoints in `Neo.Node`

References:
- `src/Neo.Observability/**`
- `src/Neo.Node/Program.cs`

## Execution/Throughput

- Parallel execution pipeline (`Neo.Execution`):
  - `DependencyAnalyzer`, `ExecutionScheduler`, `ParallelExecutor`
  - Per‑batch NoGC region support, MVCC via cloned `StoreCache`
- Batch signature verification: hooks in `Neo.Execution`/`Neo.Cryptography` for Phase 2 optimization

References:
- `src/Neo.Execution/**`

## Network (Dual Stack)

- Legacy TCP stack: Present and compatible
- QUIC transport: Implemented with platform guards; negotiator selects QUIC/WebSocket/TCP
- WebSocket transport: Server shim in `Neo.Node`, client/server actors in `Neo.Network`

### P2P Abstractions (New)

- New module `Neo.P2P.Abstractions` hosts transport-agnostic P2P messages and contracts:
  - `AcceptBridge`: request LocalNode to attach a server-side bridge (WS/QUIC/etc.)
  - `BridgeBind`: instruct a bridge to forward bytes to the protocol actor
  - `IProtocolBridge` marker implemented by `WsServerConnection` and `QuicServerConnection`
- `LocalNode` emits `BridgeBind` and retains reflection fallback to concrete `Bind` types for compatibility.
- This split enables gradual migration of actor implementations without introducing project cycles.

References:
- `src/Neo.Network/P2P/Transport/{QuicTransport,ProtocolNegotiator,Ws*}.cs`
- `src/Neo.Node/Program.cs` (`/p2p` WebSocket endpoint)

## Storage

- Abstractions: `IStore`, `IStoreSnapshot`, `IStoreProvider`
- Providers: `MemoryStore`, `LevelDbStore`, `RocksDbStore`
- Caching: Multilayer cache and snapshot/clone semantics

References:
- `src/Neo.Storage/**`

## Services and Endpoints

- JSON‑RPC: `Neo.RPC` with tracing and processing pipeline
- gRPC: `Neo.Grpc` (generated stubs and service layer)
- GraphQL: `Neo.GraphQL` (schema + services); hosted separately from `Neo.Node`

## Gaps and Next Actions

- QUIC configuration surface in `config.json` (Phase 3):
  - Add `ApplicationConfiguration:P2P:Quic` subsection (port/enable)
- Incremental state sync and advanced discovery (Phase 3):
  - Implement DHT/Kademlia flows end‑to‑end; wire to TaskManager
- State pruning and archive tier (Phase 3):
  - Extend `Neo.Storage` with pruning policies and optional archive writes
- MPT proofs (subject to NEP):
  - Add proof types, verification, and RPC exposure
- End‑to‑end regression “golden vector” tests:
  - Ensure serialization/hash/verify golden tests cover all critical types

## Benchmarks

- BenchmarkDotNet harness present with reports under `BenchmarkDotNet.Artifacts/`
- Next: integrate goal KPIs and publish trend charts (TPS, block time, startup) in CI artifacts

## How to Run

- Node host: `src/Neo.Node` (health/metrics/P2P WS)
- RPC server: `src/Neo.RPC` (host separately or integrate as hosted service)
- GraphQL: `src/Neo.GraphQL`

See also:
- `docs/ARCHITECTURE.md`
- `docs/neoan-refactor.md`
- `docs/ROADMAP.md`
