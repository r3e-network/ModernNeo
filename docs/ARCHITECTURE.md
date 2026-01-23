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
- Neo.Network: transport support for TCP, QUIC (where supported), and WebSocket, plus protocol negotiation and discovery hooks.
- Neo.VM / SmartContract: execution engine and native contracts; application engine integrations.
- Neo.Execution: parallel execution orchestration, dependency analysis, batch signature verification.
- Neo.Ledger: blockchain state, block processing, mempool with priority queues and fee estimation hooks.
- Neo.Consensus: dBFT state machine with recovery; room for pipelined consensus (feature-gated).
- Neo.Observability: OpenTelemetry metrics/OTLP, structured logging, health checks.
- Neo.Node: composition root and runtime host with DI, configuration, and health endpoints.

## Interfaces and Extensibility

- Storage: `IStore`, `ISnapshot`, `IStoreProvider` to enable pluggable stores.
- Network: transport and protocol abstractions; capability negotiation for legacy vs. advanced.
- Plugins: `Neo.Plugins` provides DI-first extensibility with hot-reload boundaries where feasible.
- RPC/gRPC/GraphQL: separate application endpoints that consume services and core.

## Compatibility

- Network: legacy TCP-compatible with capability negotiation; QUIC opt-in for modern nodes.

### P2P Runtime and Transport Split

The distributed P2P runtime lives in `Neo.Orleans` (LocalNodeGrain, RemoteNodeGrain, TaskManagerGrain), while protocol payloads and transport shims remain in `Neo.Network`.

- `Neo.Network` contains protocol payloads, capability negotiation, and WS/QUIC server bridges.
- `Neo.P2P.Abstractions` defines `IMessageTarget` and bridge messages used to connect transports to the runtime.
- `Neo.Orleans` hosts TCP plus optional QUIC and WebSocket P2P listeners that forward inbound traffic to Orleans grains.
- `LocalNodeGrain` maintains outbound peer connections to meet `MinDesiredConnections` from the unconnected pool.

This split keeps the project graph acyclic while allowing transport stacks to evolve independently from the distributed runtime.

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
- P2P endpoints are provided by the `Neo.Orleans` host. `Neo.Node` returns 501 for `/p2p`.

### Configuration (snippet)

```
ApplicationConfiguration:
  P2P:
    BindAddress: "0.0.0.0"
    Port: 10333
    MinDesiredConnections: 10
    MaxConnections: 40
    MaxConnectionsPerAddress: 3
    MaxKnownHashes: 1000
    EnableCompression: true
    Quic:
      Enabled: false
      Port: 10334
      Alpn: neo-p2p
      CertificatePath: "/path/to/cert.pfx"
      CertificatePassword: "changeit"
    WebSocket:
      Enabled: false
      Port: 10335
  Rpc:
    Enabled: false
    ListenAddress: "http://localhost:10332/"

Note: P2P settings apply to `Neo.Orleans`. `Neo.Node` only hosts management endpoints.
If QUIC is enabled without a configured certificate, `Neo.Orleans` generates a self-signed certificate at startup.
WebSocket P2P uses `HttpListener`; on Windows you may need URL ACLs for the selected prefix.
```

## Known Architecture Issues

This section documents known architectural concerns identified during code review, along with recommended refactoring approaches.

### 1. Neo.Node Composition Root Fan-out (Medium)

**Issue**: `Neo.Node` depends on multiple modules across layers as the composition root.

**Dependencies**: Neo, Neo.Core, Neo.Extensions, Neo.IO, Neo.Network, Neo.Plugins, Neo.Protocol, Neo.RPC

**Impact**:

- Tight coupling makes testing difficult
- Changes in any module can affect Neo.Node
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

**Current Score: 9/10** (Updated: legacy actor system removed, Orleans integrated)

**Strengths**:

- Clear layer definitions
- Well-isolated base modules
- Good separation of concerns in new modules
- **Legacy actor system removed** - Orleans is now the distributed runtime
- **All legacy actor dependencies removed** from all projects
- **IMessageTarget** replaces legacy actor references
- **MessageBuffer** replaces legacy ByteString for P2P messaging

**Completed Improvements**:

- ✅ Legacy actor system removed from Neo.Node
- ✅ Legacy actor system removed from Neo.P2P.Abstractions
- ✅ Legacy actor system removed from Neo.Extensions
- ✅ Legacy actor system removed from Neo.Network
- ✅ Legacy TestKit removed from Neo.UnitTests
- ✅ Orleans grains provide distributed consensus (LocalNodeGrain, ConsensusGrain, etc.)

**Remaining Areas for Improvement**:

- Neo.Node still has multiple project references (acceptable for composition root)
