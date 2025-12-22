<p align="center">
  <a href="https://neo.org/">
      <img
      src="https://neo3.azureedge.net/images/logo%20files-dark.svg"
      width="250px" alt="neo-logo">
  </a>
</p>

<h3 align="center">ModernNeo - A Modernized, Modular Neo Node (NeoAN‑aligned)</h3>

<p align="center">
   A refactored, modular implementation of the Neo blockchain with modern .NET practices.
  <br>
  <a href="https://docs.neo.org/"><strong>Documentation »</strong></a>
  <br>
  <br>
  <a href="https://github.com/neo-project/neo"><strong>Legacy Neo</strong></a>
  ·
  <a href="https://github.com/r3e-network/ModernNeo">ModernNeo</a>
  ·
  <a href="https://github.com/neo-project/neo-devpack-dotnet">Neo DevPack</a>
</p>
<p align="center">
  <a href="https://twitter.com/neo_blockchain">
      <img
      src=".github/images/twitter-logo.png"
      width="25px">
  </a>
  &nbsp;
  <a href="https://medium.com/neo-smart-economy">
      <img
      src=".github/images/medium-logo.png"
      width="23px">
  </a>
  &nbsp;
  <a href="https://neonewstoday.com">
      <img
      src=".github/images/nnt-logo.jpg"
      width="23px">
  </a>
  &nbsp;
  <a href="https://t.me/NEO_EN">
      <img
      src=".github/images/telegram-logo.png"
      width="24px" >
  </a>
  &nbsp;
  <a href="https://www.reddit.com/r/NEO/">
      <img
      src=".github/images/reddit-logo.png"
      width="24px">
  </a>
  &nbsp;
  <a href="https://discord.com/invite/rvZFQ5382k">
      <img
      src=".github/images/discord-logo.png"
      width="25px">
  </a>
  &nbsp;
  <a href="https://www.youtube.com/neosmarteconomy">
      <img
      src=".github/images/youtube-logo.png"
      width="32px">
  </a>
  &nbsp;
  <!--How to get a link? -->
  <a href="https://neo.org/">
      <img
      src=".github/images/we-chat-logo.png"
      width="25px">
  </a>
  &nbsp;
  <a href="https://weibo.com/neosmarteconomy">
      <img
      src=".github/images/weibo-logo.png"
      width="28px">
  </a>
</p>
<p align="center">
  <a href="https://github.com/neo-project/neo/releases">
    <img src="https://badge.fury.io/gh/neo-project%2Fneo.svg" alt="Current neo version.">
  </a>
  <a href='https://coveralls.io/github/neo-project/neo'>
    <img src='https://coveralls.io/repos/github/neo-project/neo/badge.svg' alt='Coverage Status' />
  </a>
  <a href="https://deepwiki.com/neo-project/neo">
    <img src="https://deepwiki.com/badge.svg" alt="Ask DeepWiki.">
  </a>
  <a href="https://github.com/neo-project/neo/blob/master/LICENSE">
    <img src="https://img.shields.io/badge/license-MIT-blue.svg" alt="License.">
  </a>
</p>

<p align="center">
  <a href="https://codespaces.new/neo-project/neo">
    <img src="https://github.com/codespaces/badge.svg" alt="Open in GitHub Codespaces.">
  </a>
</p>

## Table of Contents

1. [Overview](#overview)
2. [Fork Information](#fork-information)
3. [Architecture](#architecture)
4. [Project structure](#project-structure)
5. [Running the Node](#running-the-node)
6. [Compliance Checklist](#compliance-checklist)
7. [Related projects](#related-projects)
8. [Opening a new issue](#opening-a-new-issue)
9. [Contributing](#contributing)
10. [Bounty program](#bounty-program)
11. [License](#license)

## Overview

This repository is a csharp implementation of the [neo](https://neo.org) blockchain. It is jointly maintained by the neo core developers and neo global development community.
Visit the [tutorials](https://docs.neo.org) to get started.

## Fork Information

**ModernNeo** is a modernized, modular refactoring of the [legacy Neo blockchain](https://github.com/neo-project/neo) implementation.

### Base Commit

This project was forked and refactored from the following commit in the legacy Neo repository:

| Property           | Value                                                                                                                            |
| ------------------ | -------------------------------------------------------------------------------------------------------------------------------- |
| **Repository**     | [neo-project/neo](https://github.com/neo-project/neo)                                                                            |
| **Commit Hash**    | [`d3949f9203a51fea1b5a6957d5d3b963646fde0e`](https://github.com/neo-project/neo/commit/d3949f9203a51fea1b5a6957d5d3b963646fde0e) |
| **Commit Date**    | 2025-11-14                                                                                                                       |
| **Commit Message** | Revert "Fix: consistent behavior for hash methods (#4305)" (#4310)                                                               |

### Tracking Upstream Updates

To sync future updates from the legacy Neo repository:

```bash
# Add upstream remote (if not already added)
git remote add upstream https://github.com/neo-project/neo.git

# Fetch upstream changes
git fetch upstream

# View commits since fork
git log d3949f92..upstream/master --oneline

# Cherry-pick or merge specific changes as needed
git cherry-pick <commit-hash>
```

### Key Modernization Changes

- **Modular Architecture**: Split monolithic codebase into 33+ focused modules
- **Modern .NET**: Upgraded to .NET 10 with NativeAOT support
- **Orleans Integration**: Distributed actor model for consensus and state management
- **Enhanced Storage**: Pluggable storage with LevelDB, RocksDB, and in-memory providers
- **Observability**: OpenTelemetry-based distributed tracing and metrics
- **Plugin System**: Hot-reloadable plugin architecture with dependency injection

## Architecture

ModernNeo follows the Neo Advanced Node (NeoAN) design. See docs for full details:

- Architecture: docs/ARCHITECTURE.md
- Module map: docs/neoan-module-map.md
- Refactor strategy: docs/neoan-refactor.md
- Roadmap: docs/ROADMAP.md

Layers and key modules:

- Application: `Neo.Node` (host, health, metrics), `Neo.RPC`, `Neo.Grpc`, `Neo.GraphQL`
- Services: `Neo.Services`, `Neo.Plugins`
- Core: `Neo.Execution`, `Neo.Ledger`, `Neo.TxPool`, `Neo.Consensus`, `Neo.SmartContract*`, `Neo.Protocol*`
- Infrastructure: `Neo.Network` (TCP/QUIC/WS), `Neo.Storage` (providers, snapshot/cache), `Neo.Cryptography`, `Neo.IO`, `Neo.Extensions`, `Neo.Observability`
- Base: `Neo.Core` primitives and serialization


## Project structure

An overview of the project folders can be seen below.

| Folder                                                                                          | Content                                                                                           |
| ----------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------- |
| [/src/neo/Cryptography/](https://github.com/neo-project/neo/tree/master/src/Neo/Cryptography)   | General cryptography implementation, including ECC.                                               |
| [/src/neo/IO/](https://github.com/neo-project/neo/tree/master/src/Neo/IO)                       | Data structures used for caching and collection interaction.                                      |
| [/src/neo/Ledger/](https://github.com/neo-project/neo/tree/master/src/Neo/Ledger)               | Classes responsible for the state control, including the `MemoryPool` and `Blockchain`.           |
| [/src/neo/Network/](https://github.com/neo-project/neo/tree/master/src/Neo/Network)             | Peer-to-peer protocol implementation.                                                             |
| [/src/neo/Persistence/](https://github.com/neo-project/neo/tree/master/src/Neo/Persistence)     | Classes used to allow other classes to access application state.                                  |
| [/src/neo/Plugins/](https://github.com/neo-project/neo/tree/master/src/Neo/Plugins)             | Interfaces used to extend Neo, including the storage interface.                                   |
| [/src/neo/SmartContract/](https://github.com/neo-project/neo/tree/master/src/Neo/SmartContract) | Native contracts, `ApplicationEngine`, `InteropService` and other smart-contract related classes. |
| [/src/neo/Wallets/](https://github.com/neo-project/neo/tree/master/src/Neo/Wallets)             | Wallet and account implementation.                                                                |
| [/src/Neo.Extensions/](https://github.com/neo-project/neo/tree/master/src/Neo.Extensions)       | Extensions to expand the existing functionality.                                                  |
| [/src/Neo.Json/](https://github.com/neo-project/neo/tree/master/src/Neo.Json)                   | Neo's JSON specification.                                                                         |
| [/tests/](https://github.com/neo-project/neo/tree/master/tests)                                 | All unit tests.                                                                                   |

Additional ModernNeo modules of interest:

- `src/Neo.Node` — Node host with health (`/health`), ready (`/ready`), metrics (`/metrics`), and optional P2P WebSocket endpoint (`/p2p`).
- `src/Neo.Network` — Dual‑stack transport with `ProtocolNegotiator`, `QuicTransport`, and WebSocket peer/server bridges.
- `src/Neo.Execution` — Parallel transaction execution with dependency analysis and scheduler.
- `src/Neo.Storage` — Abstractions and providers (`Memory`, `LevelDB`, `RocksDB`).
- `src/Neo.Observability` — OpenTelemetry metrics/tracing and health interfaces.
- `src/Neo.Services` — Query/services used by GraphQL and future APIs.
- `src/Neo.GraphQL` — Minimal GraphQL endpoint backed by `Neo.Services`.

### GraphQL (optional)

- Build and run the standalone GraphQL host:

```
dotnet run -c Release -p src/Neo.GraphQL -- --urls http://localhost:4000
```

- Query examples:
  - `POST http://localhost:4000/graphql` with body `{ network height mempoolCount }`
  - Blocks range: `{ blocks(start: 0, count: 5) }`

## Running the Node

Run the node host with the bundled configuration:

```bash
dotnet run -c Release -p src/Neo.Node -- --config src/Neo.Node/config.json
```

Management endpoints:

- Health: http://localhost:5000/health
- Ready: http://localhost:5000/ready
- Metrics (Prometheus): http://localhost:5000/metrics

Optional P2P WebSocket (server‑side): http://localhost:5000/p2p

### QUIC (optional)

- Enable QUIC in `src/Neo.Node/config.json`:

```
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
```

- QUIC runs only on supported platforms (Windows 11+, Linux with libmsquic, macOS 14+). TCP remains the default.

### Node Info Endpoint

- `GET /info` returns quick status: network magic, P2P port, mempool counts, current height, and peer counts.

## Compliance Checklist

See docs/NEOAN-COMPLIANCE.md for status against the NeoAN plan (compatibility, observability, execution, network, storage, services).

## Related projects

Code references are provided for all platform building blocks. That includes the base library, the VM, a command line application and the compiler.

- [neo:](https://github.com/neo-project/neo/) Included libraries are Neo, Neo-CLI, Neo-GUI, Neo-VM, test and plugin modules.
- [neo-express:](https://github.com/neo-project/neo-express/) A private net optimized for development scenarios.
- [neo-devpack-dotnet:](https://github.com/neo-project/neo-devpack-dotnet/) These are the official tools used to convert a C# smart-contract into a _neo executable file_.
- [neo-proposals:](https://github.com/neo-project/proposals) NEO Enhancement Proposals (NEPs) describe standards for the NEO platform, including core protocol specifications, client APIs, and contract standards.
- [neo-non-native-contracts:](https://github.com/neo-project/non-native-contracts) Includes non-native contracts that live on the blockchain, included but not limited to NeoNameService.

## Opening a new issue

Please feel free to create new issues to suggest features or ask questions.

- [Feature request](https://github.com/neo-project/neo/issues/new?assignees=&labels=discussion&template=feature-or-enhancement-request.md&title=)
- [Bug report](https://github.com/neo-project/neo/issues/new?assignees=&labels=&template=bug_report.md&title=)
- [Questions](https://github.com/neo-project/neo/issues/new?assignees=&labels=question&template=questions.md&title=)

If you found a security issue, please refer to our [security policy](https://github.com/neo-project/neo/security/policy).

## Contributing

We welcome contributions to the Neo project! To ensure a smooth collaboration process, please follow these guidelines:

### Branch Rules

- **`master`** - Contains the latest stable release version. This branch reflects the current production state.
- **`dev`** - The main development branch where all new features and improvements are integrated.

### Pull Request Guidelines

**Important**: All pull requests must be based on the `dev` branch, not `master`.

1. **Fork the repository** and create your feature branch from `dev`:

   ```bash
   git checkout dev
   git pull origin dev
   git checkout -b feature/your-feature-name
   ```

2. **Make your changes** following the project's coding standards and conventions.

3. **Test your changes** thoroughly to ensure they don't break existing functionality.

4. **Commit your changes** with clear, descriptive commit messages:

   ```bash
   git commit -m "feat: add new feature description"
   ```

5. **Push to your fork** and create a pull request against the `dev` branch:

   ```bash
   git push origin feature/your-feature-name
   ```

6. **Create a Pull Request** targeting the `dev` branch with:
   - Clear title and description
   - Reference to any related issues
   - Summary of changes made

### Development Workflow

```
feature/bug-fix → dev → master (via release)
```

- Feature branches are merged into `dev`
- `dev` is periodically merged into `master` for releases
- Never create PRs directly against `master`

For more detailed contribution guidelines, please check our documentation or reach out to the maintainers.

## Bounty program

You can be rewarded by finding security issues. Please refer to our [bounty program page](https://neo.org/bounty) for more information.

## License

The NEO project is licensed under the [MIT license](LICENSE).
