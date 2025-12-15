# Neo Advanced Node (NeoAN) — ModernNeo 重构实施指南

> 目标：在 **不破坏现有 Neo N3 网络/数据/RPC 兼容性**（Phase 1-2）的前提下，将当前代码库逐步演进为 **高性能、模块化、可观测、可扩展** 的“先进全节点”架构。

本仓库当前形态更接近 **Neo N3 C# 协议实现/核心库**（`src/Neo`）而非完整“节点产品”（缺少独立 RPC/CLI 入口项目）。因此重构建议采用 **Strangler Fig（绞杀者）** 方式：先建立新架构的“骨架 + 兼容层 + 约束”，再逐块替换内部实现。

---

## 1. 当前代码库概况（用于映射与拆分）

- `src/Neo`：主要协议实现（网络、账本、持久化、智能合约、钱包、插件等）目前 **集中在同一程序集** 中。
- `src/Neo.Extensions`、`src/Neo.IO`、`src/Neo.Json`：已拆分成独立项目（说明“多程序集+同命名空间 Neo.*”在本仓库是可行的）。
- `NeoSystem`：节点核心组合入口（创建 `Blockchain/LocalNode/TaskManager` 等 Akka actors），是后续引入“Application/Service Layer”的最佳切入点。

---

## 2. 目标架构落地策略（Phase 1-2：完全兼容）

### 2.1 分层与依赖方向（强约束）

建议采用以下依赖方向（只允许向下依赖）：

1. **Application Layer**：`Neo.Node`（运行时入口、托管、配置、API Host）
2. **Service Layer**：`Neo.Rpc`、`Neo.Indexer`、`Neo.OracleService`、`Neo.WalletService`（可选模块化服务）
3. **Core Layer**：`Neo.Ledger`、`Neo.Execution`、`Neo.TxPool`、`Neo.Consensus`（协议核心逻辑）
4. **Infrastructure Layer**：`Neo.Network`、`Neo.Storage`、`Neo.Cryptography`、`Neo.Observability`
5. **Base/Primitives**：`Neo.Core`（基础类型、序列化接口、常量与小型工具）

> 兼容关键点：Phase 1-2 期间，**网络消息、区块/交易序列化、状态存储格式、RPC 语义** 必须保持与现有 Neo 节点一致。

### 2.2 渐进拆分的推荐技术手段

- **项目拆分（多程序集）优先于大规模重写**：先把现有代码从 `Neo` 项目“拆出”到新项目中，但保持 namespace 不变（例如类型仍在 `namespace Neo` 或 `namespace Neo.Network...`）。
- **Type Forwarding 维持二进制兼容（强烈推荐）**：
  - 将类型从 `Neo.dll` 移到 `Neo.Core.dll` 等新程序集后，在 `Neo` 中使用 `TypeForwardedTo` 将旧类型位置转发到新程序集。
  - 好处：下游插件/工具即使不重新编译，也更可能继续工作（避免“类型不在 Neo.dll”导致的加载失败）。
- **兼容性回归测试先行**：
  - “黄金向量”测试：对关键序列化/哈希/签名/网络消息做固定输入输出比对。
  - “互操作测试”：与官方节点或历史版本节点进行 P2P 互联/同步对比（可在 CI 外部环境跑）。
- **先抽象后替换**：
  - 为网络、存储、执行、内存池建立最小抽象接口（例如 `INetworkStack`、`IStoreProvider`、`IExecutionEngine`）。
  - 新实现以插件/替换组件方式接入，避免一次性推翻。

---

## 3. 拆分落地顺序建议（从低风险到高风险）

1. **Neo.Core（低风险）**
   - 迁移基础类型（`UInt160/UInt256/BigDecimal` 等）和少量无副作用工具。
   - 通过 Type Forwarding 保持旧 API 可用。
2. **Neo.Cryptography / Neo.Storage（中风险）**
   - 先拆程序集，不改变算法与存储格式；后续在 Phase 2 做批量验签、缓存层、RocksDB 优化。
3. **Neo.Network（中高风险）**
   - 保持 Legacy 协议栈完全兼容；新协议（QUIC）仅在 Phase 3 引入。
4. **Neo.Execution / Neo.Ledger / Neo.TxPool（高风险）**
   - 牵涉共识/状态一致性/性能优化，必须以回归测试与基准为门槛。
5. **Neo.Observability（低风险但高收益）**
   - 先引入统一指标/Tracing/结构化日志 API，再逐步在关键路径埋点。

---

## 4. 兼容性清单（每次重构 PR 必须过）

- **序列化兼容**：`Block/Transaction/Witness/Signer` 等二进制序列化输出一致。
- **哈希/签名兼容**：`Hash160/Hash256`、ECDSA 验签结果一致。
- **状态一致性**：相同区块输入后状态根/存储键值一致（至少在关键表空间）。
- **网络互操作**：握手、Capability 协商、消息编解码与流控一致。
- **API 兼容**（当引入 RPC 时）：JSON-RPC 方法名、参数、错误码、返回结构一致。

---

## 5. 本仓库内的“可执行最小里程碑”（建议）

> 在本仓库缺少 CLI/RPC 入口的情况下，建议优先产出一个可运行的 `Neo.Node`（或 `ModernNeo.Node`）宿主，最小能力为：

- 读取 `ProtocolSettings` + 存储配置
- 初始化 `NeoSystem`
- 启动 P2P 同步并输出可观测指标（日志/metrics）

后续再把 JSON-RPC / gRPC / WebSocket 作为 **可选模块** 接入。

