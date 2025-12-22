<p align="center">
  <a href="https://neo.org/">
      <img
      src="https://neo3.azureedge.net/images/logo%20files-dark.svg"
      width="250px" alt="neo-logo">
  </a>
</p>

<h3 align="center">ModernNeo - A Modernized, Modular Neo Blockchain Node</h3>

<p align="center">
  A fully refactored, modular implementation of the Neo blockchain with modern .NET 10 practices.
  <br>
  <a href="https://docs.neo.org/"><strong>Documentation</strong></a>
  ·
  <a href="https://github.com/r3e-network/ModernNeo">GitHub</a>
  ·
  <a href="https://github.com/r3e-network/ModernNeo/issues">Issues</a>
</p>

<p align="center">
  <a href="https://github.com/r3e-network/ModernNeo/actions">
    <img src="https://github.com/r3e-network/ModernNeo/workflows/.NET%20Core%20Test%20and%20Publish/badge.svg" alt="Build Status">
  </a>
  <a href="https://github.com/r3e-network/ModernNeo/blob/master/LICENSE">
    <img src="https://img.shields.io/badge/license-MIT-blue.svg" alt="License">
  </a>
  <img src="https://img.shields.io/badge/.NET-10.0-purple.svg" alt=".NET 10">
  <img src="https://img.shields.io/badge/modules-36+-green.svg" alt="Modules">
</p>

---

## Overview

**ModernNeo** is a ground-up modular refactoring of the Neo blockchain node, designed for:

- **Modularity**: 36+ focused, single-responsibility modules
- **Modern .NET**: .NET 10 with NativeAOT compilation support
- **Multiple Transports**: TCP, QUIC, and WebSocket P2P networking
- **Distributed Architecture**: Microsoft Orleans integration for actor-based consensus
- **Observability**: OpenTelemetry tracing, Prometheus metrics, health endpoints
- **Pluggable Storage**: LevelDB, RocksDB, LMDB, and in-memory providers

## Architecture

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                              APPLICATION LAYER                               │
├─────────────────────────────────────────────────────────────────────────────┤
│  Neo.Node          Neo.RPC           Neo.Grpc          Neo.GraphQL          │
│  (Host/CLI)        (JSON-RPC)        (gRPC Services)   (GraphQL API)        │
└─────────────────────────────────────────────────────────────────────────────┘
                                      │
┌─────────────────────────────────────────────────────────────────────────────┐
│                               SERVICES LAYER                                 │
├─────────────────────────────────────────────────────────────────────────────┤
│  Neo.Services      Neo.Plugins       Neo.Orleans       Neo.Observability    │
│  (Query/Business)  (Hot-reload)      (Distributed)     (Tracing/Metrics)    │
└─────────────────────────────────────────────────────────────────────────────┘
                                      │
┌─────────────────────────────────────────────────────────────────────────────┐
│                                 CORE LAYER                                   │
├─────────────────────────────────────────────────────────────────────────────┤
│  Neo.Ledger        Neo.Consensus     Neo.TxPool        Neo.Execution        │
│  (Blockchain)      (dBFT)            (Mempool)         (Parallel Exec)      │
├─────────────────────────────────────────────────────────────────────────────┤
│  Neo.SmartContract                   Neo.Protocol      Neo.Wallets          │
│  (VM/Native)                         (P2P Messages)    (Account Mgmt)       │
└─────────────────────────────────────────────────────────────────────────────┘
                                      │
┌─────────────────────────────────────────────────────────────────────────────┐
│                            INFRASTRUCTURE LAYER                              │
├─────────────────────────────────────────────────────────────────────────────┤
│  Neo.Network                         Neo.Storage                             │
│  ├─ TCP Transport                    ├─ MemoryStore                         │
│  ├─ QUIC Transport                   ├─ LevelDB                             │
│  ├─ WebSocket Transport              ├─ RocksDB                             │
│  └─ Kademlia DHT Discovery           └─ Tiered Cache                        │
├─────────────────────────────────────────────────────────────────────────────┤
│  Neo.Cryptography  Neo.IO            Neo.Extensions    Neo.Json             │
└─────────────────────────────────────────────────────────────────────────────┘
                                      │
┌─────────────────────────────────────────────────────────────────────────────┐
│                                BASE LAYER                                    │
├─────────────────────────────────────────────────────────────────────────────┤
│  Neo.Core (Primitives, Interfaces, Abstractions)                            │
│  Neo.P2P.Abstractions (Transport-agnostic messaging)                        │
└─────────────────────────────────────────────────────────────────────────────┘
```

## Module Catalog

### Application Layer

| Module         | Description                                        |
| -------------- | -------------------------------------------------- |
| `Neo.Node`     | Main node host with health/ready/metrics endpoints |
| `Neo.Node.AOT` | NativeAOT-compiled node variant                    |
| `Neo.RPC`      | JSON-RPC 2.0 server implementation                 |
| `Neo.Grpc`     | gRPC service definitions and implementations       |
| `Neo.GraphQL`  | GraphQL API for blockchain queries                 |

### Services Layer

| Module              | Description                                    |
| ------------------- | ---------------------------------------------- |
| `Neo.Services`      | Business logic services (BlockQuery, NodeInfo) |
| `Neo.Plugins`       | Hot-reloadable plugin system with DI           |
| `Neo.Orleans`       | Distributed actor grains for consensus/state   |
| `Neo.Observability` | OpenTelemetry tracing and metrics              |

### Core Layer

| Module                           | Description                                     |
| -------------------------------- | ----------------------------------------------- |
| `Neo.Ledger`                     | Blockchain state management and block execution |
| `Neo.Consensus`                  | dBFT consensus state machine                    |
| `Neo.TxPool`                     | Transaction pool with priority queuing          |
| `Neo.Execution`                  | Parallel transaction execution engine           |
| `Neo.SmartContract`              | Smart contract VM and native contracts          |
| `Neo.SmartContract.Abstractions` | Contract interfaces                             |
| `Neo.Protocol`                   | P2P protocol message definitions                |
| `Neo.Protocol.Payloads`          | Serializable payload types                      |
| `Neo.Wallets`                    | Wallet and account management                   |
| `Neo.Builders`                   | Fluent builders for transactions/witnesses      |
| `Neo.Sign`                       | Cryptographic signing services                  |

### Infrastructure Layer

| Module                 | Description                              |
| ---------------------- | ---------------------------------------- |
| `Neo.Network`          | Multi-transport networking (TCP/QUIC/WS) |
| `Neo.P2P.Abstractions` | Transport-agnostic P2P messaging         |
| `Neo.Storage`          | Storage abstractions and providers       |
| `Neo.Cryptography`     | ECC, hashing, and crypto primitives      |
| `Neo.IO`               | Serialization and data structures        |
| `Neo.Extensions`       | Utility extensions                       |
| `Neo.Json`             | JSON serialization                       |
| `Neo.Core`             | Core primitives and interfaces           |

### Plugins

| Module                | Description                    |
| --------------------- | ------------------------------ |
| `Neo.ApplicationLogs` | Application log persistence    |
| `Neo.RpcNep17Tracker` | NEP-17 token transfer tracking |
| `Neo.LevelDBStore`    | LevelDB storage plugin         |

## Quick Start

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- (Optional) LevelDB or RocksDB for persistent storage

### Build

```bash
dotnet build
```

### Run Tests

```bash
dotnet test
```

### Run Node

```bash
dotnet run --project src/Neo.Node -- --config src/Neo.Node/config.json
```

### Endpoints

| Endpoint      | URL                             | Description            |
| ------------- | ------------------------------- | ---------------------- |
| Health        | `http://localhost:5000/health`  | Liveness probe         |
| Ready         | `http://localhost:5000/ready`   | Readiness probe        |
| Metrics       | `http://localhost:5000/metrics` | Prometheus metrics     |
| Node Info     | `http://localhost:5000/info`    | Node status JSON       |
| P2P WebSocket | `ws://localhost:5000/p2p`       | WebSocket P2P endpoint |

## Configuration

### P2P with QUIC Support

```json
{
  "ApplicationConfiguration": {
    "P2P": {
      "Port": 10333,
      "EnableCompression": true,
      "Quic": {
        "Enabled": true,
        "Port": 10334,
        "Alpn": "neo-p2p"
      }
    }
  }
}
```

### Storage Providers

```json
{
  "ApplicationConfiguration": {
    "Storage": {
      "Engine": "LevelDB",
      "Path": "Data_LevelDB"
    }
  }
}
```

Supported engines: `Memory`, `LevelDB`, `RocksDB`

## Key Features

### Multi-Transport Networking

- **TCP**: Default reliable transport
- **QUIC**: Low-latency, multiplexed connections (Windows 11+, Linux with libmsquic, macOS 14+)
- **WebSocket**: Browser-compatible P2P connections
- **Kademlia DHT**: Decentralized peer discovery

### Parallel Execution

- Dependency analysis for transaction parallelization
- Configurable execution scheduler
- Conflict detection and resolution

### Orleans Integration

- Distributed virtual actor model
- Grain-based consensus coordination
- Scalable state management

### Observability

- OpenTelemetry distributed tracing
- Prometheus metrics export
- Structured logging
- Health check endpoints

## Development

### Project Structure

```
ModernNeo/
├── src/
│   ├── Neo/                    # Legacy compatibility layer
│   ├── Neo.Core/               # Core primitives
│   ├── Neo.Node/               # Main node application
│   ├── Neo.Network/            # P2P networking
│   ├── Neo.Storage/            # Storage providers
│   ├── Neo.Ledger/             # Blockchain state
│   ├── Neo.Consensus/          # dBFT consensus
│   ├── Neo.TxPool/             # Transaction pool
│   ├── Neo.Execution/          # Parallel execution
│   ├── Neo.SmartContract/      # Smart contracts
│   ├── Neo.Orleans/            # Distributed actors
│   ├── Neo.RPC/                # JSON-RPC server
│   ├── Neo.Grpc/               # gRPC services
│   ├── Neo.GraphQL/            # GraphQL API
│   ├── Neo.Observability/      # Tracing/metrics
│   ├── Neo.Services/           # Business services
│   ├── Neo.Plugins/            # Plugin system
│   └── ...                     # Additional modules
├── tests/
│   ├── Neo.UnitTests/          # Unit tests
│   ├── Neo.Storage.Tests/      # Storage tests
│   └── ...
├── benchmarks/
│   └── Neo.Benchmarks/         # Performance benchmarks
└── docs/
    ├── ARCHITECTURE.md         # Architecture details
    ├── ROADMAP.md              # Development roadmap
    └── NEOAN-COMPLIANCE.md     # NeoAN compliance status
```

### Running Benchmarks

```bash
dotnet run -c Release --project benchmarks/Neo.Benchmarks
```

## Contributing

1. Fork the repository
2. Create a feature branch from `master-n3`
3. Make your changes with tests
4. Run `dotnet format` before committing
5. Submit a pull request

## License

This project is licensed under the [MIT License](LICENSE).

---

<p align="center">
  <sub>Built with modern .NET practices for the Neo ecosystem</sub>
</p>
