# ModernNeo Architecture

This document outlines the ModernNeo architecture aligned with the "Neo Advanced Node (NeoAN)" design. The codebase is organized into cohesive modules that map to the Infrastructure, Core, Service, and Application layers.

## Layers

- Application Layer: `Neo.RPC`, `Neo.Grpc`, `Neo.GraphQL`, `Neo.Node`
- Service Layer: `Neo.Services`, `Neo.Plugins`, feature services in `Neo.*`
- Core Layer: `Neo.Execution`, `Neo.Ledger`, `Neo.TxPool`, `Neo.Consensus`, `Neo.SmartContract*`, `Neo.Protocol*`
- Infrastructure Layer: `Neo.Network`, `Neo.Storage`, `Neo.Cryptography`, `Neo.IO`, `Neo.Extensions`, `Neo.Observability`

## Key Modules

- Neo.Core: primitives, blocks, transactions, serialization contracts; kept 100% wire-compatible.
- Neo.Cryptography: ECC, hash, signature verification; optimized for batch ops where available.
- Neo.Storage: provider abstractions, LevelDB/RocksDB providers, state cache, snapshot APIs.
- Neo.Network: dual-stack transport (TCP + QUIC where supported), protocol negotiation, discovery hooks.
- Neo.VM / SmartContract: execution engine and native contracts; application engine integrations.
- Neo.Execution: parallel execution orchestration, dependency analysis, batch signature verification.
- Neo.Ledger: blockchain state, block processing, mempool with priority queues and fee estimation hooks.
- Neo.Consensus: dBFT state machine with recovery; room for pipelined consensus (feature-gated).
- Neo.Observability: OpenTelemetry metrics/OTLP, structured logging, health checks.
- Neo.Node / Neo.Node.Core: composition root and runtime host with DI, configuration, and health endpoints.

## Interfaces and Extensibility

- Storage: `IStore`, `ISnapshot`, `IStoreProvider` to enable pluggable stores.
- Network: transport and protocol abstractions; capability negotiation for legacy vs. advanced.
- Plugins: `Neo.Plugins` provides DI-first extensibility with hot-reload boundaries where feasible.
- RPC/gRPC/GraphQL: separate application endpoints that consume services and core.

## Compatibility

- Network: legacy TCP-compatible with capability negotiation; QUIC opt-in for modern nodes.
 - Network: legacy TCP-compatible with capability negotiation; QUIC opt-in for modern nodes.
 
### Why `LocalNode` still lives in `src/Neo`

To keep the project graph acyclic and preserve Phase 1–2 binary and behavioral compatibility, the actor-level P2P types (`LocalNode`, `RemoteNode`, `Peer`, `Message`) remain in the `Neo` assembly for now, while transport implementations (WS/QUIC) live in `Neo.Network`.

- Today’s dependency DAG:
  - `Neo.Network` → `Neo.Ledger` → `Neo`
  - Moving `LocalNode` into `Neo.Network` would force `Neo` → `Neo.Network`, creating a cycle.

- Mitigations already in place:
  - Transport-agnostic accept (`LocalNode.AcceptBridge`) and reflection-based `Bind` avoid compile-time dependency on transport types.
  - All new transport code (QUIC/WebSocket bridges) resides in `Neo.Network`.

- Planned migration (no breaking changes):
  1) `Neo.P2P.Abstractions`: messages + minimal contracts (added: `AcceptBridge`, `BridgeBind`, `WriteBytes`, `DataReceived`, `CloseConnection`).
  2) Adapt bridges and `LocalNode` to speak abstractions (done; reflection fallback retained).
  3) Introduce `IProtocolConnection` shape and refactor `RemoteNode/Connection` internals incrementally.
  4) Remove legacy reflection binds when the interface path is fully wired; consider `TypeForwardedTo` if types relocate.

This staged approach keeps current nodes interoperable and CI stable while aligning with the modular NeoAN design.
- Data: ledger, storage keys and native contract ABI maintained; migration utilities under `Neo.Builders`.
- API: JSON-RPC maintained for full compatibility; GraphQL is additive.

## Observability

- Metrics: runtime metrics, network, ledger, VM meters exported via Prometheus.
- Tracing: OTLP exporter optional; spans around block processing, consensus, and RPC pipelines.
- Health: `/health` and `/ready` endpoints in `Neo.Node`.

## Node Host

- Management endpoints:
  - `/health` – liveness and basic diagnostics
  - `/ready` – readiness signal
  - `/metrics` – Prometheus scrape endpoint
  - `/info` – quick status (network, mempool, height, peer counts)
- P2P endpoints:
  - WebSocket: `/p2p` (optional)
  - QUIC: configurable listener; opt-in and platform-guarded

### Configuration (snippet)

```
ApplicationConfiguration:
  P2P:
    Port: 10333
    EnableCompression: true
    Quic:
      Enabled: false
      Port: 10334
      Alpn: neo-p2p
  Rpc:
    Enabled: false
    ListenAddress: "http://localhost:10332/"
```
