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
    1. `Neo.P2P.Abstractions`: messages + minimal contracts (added: `AcceptBridge`, `BridgeBind`, `WriteBytes`, `DataReceived`, `CloseConnection`).
    2. Adapt bridges and `LocalNode` to speak abstractions (done; reflection fallback retained).
    3. Introduce `IProtocolConnection` shape and refactor `RemoteNode/Connection` internals incrementally.
    4. Remove legacy reflection binds when the interface path is fully wired; consider `TypeForwardedTo` if types relocate.

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

## Known Architecture Issues

This section documents known architectural concerns identified during code review, along with recommended refactoring approaches.

### 1. Neo.Node.Core "God Object" (Critical)

**Issue**: `Neo.Node.Core` depends on 13+ modules across all layers, violating the layered architecture principle.

**Dependencies**: Neo, Neo.Core, Neo.Execution, Neo.Cryptography, Neo.Extensions, Neo.IO, Neo.Json, Neo.Ledger, Neo.Network, Neo.Observability, Neo.Plugins, Neo.Protocol, Neo.SmartContract, Neo.Storage, Neo.Wallets

**Impact**:

- Tight coupling makes testing difficult
- Changes in any module can affect Neo.Node.Core
- Violates Single Responsibility Principle

**Recommended Refactoring**:

1. Extract focused composition modules (e.g., `Neo.Node.Composition`)
2. Use dependency injection to break compile-time dependencies
3. Define clear interfaces for cross-layer communication

### 2. Circular Dependency: Neo.Protocol ↔ Neo.Protocol.Payloads (High)

**Issue**: Potential circular dependency between `Neo.Protocol` and `Neo.Protocol.Payloads`.

**Impact**:

- Complicates build order
- Makes independent testing difficult
- Violates acyclic dependency principle

**Recommended Refactoring**:

1. Merge into single `Neo.Protocol` module, or
2. Extract shared types to `Neo.Protocol.Core` base module
3. Use interfaces to break the cycle

### 3. Layer Violations (Medium)

**Identified Violations**:

- `Neo.Network` (Infrastructure) → `Neo.Ledger` (Core): Infrastructure should not depend on Core
- `Neo.SmartContract` (Core) → `Neo.Observability` (Services): Core should not depend on Services
- `Neo.Protocol.Payloads` (Core) → `Neo.Storage` (Infrastructure): Protocol payloads should be pure data

**Recommended Refactoring**:

1. Use abstractions/interfaces instead of direct dependencies
2. Move `Neo.Observability` to cross-cutting concern pattern (AOP or interfaces)
3. Extract storage-independent payload types

### 4. Cross-Cutting Concerns: Neo.Observability

**Issue**: `Neo.Observability` is used across all layers (Network, SmartContract, Services, RPC, Grpc).

**Impact**:

- Creates implicit dependencies across layers
- Makes it difficult to use modules independently

**Recommended Refactoring**:

1. Define `IMetrics`, `ITracing` interfaces in base layer
2. Use dependency injection for observability services
3. Consider aspect-oriented programming for tracing

## Well-Designed Modules

The following modules correctly follow the layered architecture:

- **Neo.Core** (Base): Properly isolated, only depends on Neo.Extensions and Neo.IO
- **Neo.P2P.Abstractions** (Base): Properly isolated, minimal external dependencies
- **Neo.TxPool** (Core): Properly isolated, only depends on Neo.Core
- **Neo.Consensus** (Core): Properly isolated, only depends on Neo.Core
- **Neo.Extensions** (Infrastructure): Pure utility layer with no dependencies

## Architecture Health Score

**Current Score: 9/10** (Updated: Akka fully removed, Orleans integrated)

**Strengths**:

- Clear layer definitions
- Well-isolated base modules
- Good separation of concerns in new modules
- **Akka completely removed** - Orleans is now the distributed runtime
- **All NuGet Akka dependencies removed** from all projects
- **IMessageTarget** replaces IActorRef for actor abstraction
- **MessageBuffer** replaces Akka.IO.ByteString for P2P messaging

**Completed Improvements**:

- ✅ Akka removed from Neo.Node.Core
- ✅ Akka removed from Neo.P2P.Abstractions
- ✅ Akka removed from Neo.Extensions
- ✅ Akka removed from Neo.Network
- ✅ Akka.TestKit removed from Neo.UnitTests
- ✅ Orleans grains provide distributed consensus (LocalNodeGrain, ConsensusGrain, etc.)

**Remaining Areas for Improvement**:

- Neo.Node.Core still has multiple project references (acceptable for composition root)
- LocalNode remains in src/Neo (planned migration to Neo.Network)
