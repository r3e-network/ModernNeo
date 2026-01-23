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
- QUIC transport: Implemented with platform guards; optional QUIC listener wired into Neo.Orleans (inbound only)
- TCP transport: Neo.Orleans host listener forwards inbound messages to Orleans grains
- Outbound peer maintenance: LocalNodeGrain maintains MinDesiredConnections using the unconnected pool
- Compression and max-known-hash limits are configured via `ApplicationConfiguration:P2P` and honored by Orleans grains
- WebSocket transport: Inbound listener wired into `Neo.Orleans` (WS capability advertised); outbound remains TCP/QUIC

### P2P Abstractions (New)

- New module `Neo.P2P.Abstractions` hosts transport-agnostic P2P messages and contracts:
  - `AcceptBridge`: request the runtime to attach a server-side bridge (WS/QUIC/etc.)
  - `BridgeBind`: instruct a bridge to forward bytes to a target
  - `IProtocolBridge` marker implemented by `WsServerConnection` and `QuicServerConnection`
- Server bridge connections accept `BridgeBind` to attach an `IMessageTarget`.
- Orleans grains (`LocalNodeGrain`, `RemoteNodeGrain`) handle P2P state and message processing.
- This split enables gradual migration of transport wiring without introducing project cycles.

References:
- `src/Neo.Network/P2P/Transport/{QuicTransport,ProtocolNegotiator,Ws*}.cs`
- `src/Neo.Node/Program.cs` (`/p2p` returns 501; P2P lives in `Neo.Orleans`)

## Storage

- Abstractions: `IStore`, `IStoreSnapshot`, `IStoreProvider`
- Providers: `MemoryStore`, `LevelDBStore`, `RocksDBStore`
- Caching: Multilayer cache and snapshot/clone semantics

References:
- `src/Neo.Storage/**`

## Services and Endpoints

- JSON‑RPC: `Neo.RPC` with tracing and processing pipeline
- gRPC: `Neo.Grpc` (generated stubs and service layer)
- GraphQL: `Neo.GraphQL` (schema + services); hosted separately from `Neo.Node`

## Gaps and Next Actions

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

- Node host: `src/Neo.Node` (health/metrics; no P2P)
- RPC server: `src/Neo.RPC` (host separately or integrate as hosted service)
- GraphQL: `src/Neo.GraphQL`

See also:
- `docs/ARCHITECTURE.md`
- `docs/neoan-refactor.md`
- `docs/ROADMAP.md`
