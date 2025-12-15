# NeoAN 模块映射（Current → Target）

本文件用于把当前仓库的目录/namespace，映射到 NeoAN 目标模块，便于拆分任务“可切块、可回滚、可验证”。

---

## 1. 当前项目/目录概览

- `src/Neo`（单程序集承载大部分核心逻辑）
  - `Cryptography/`、`Persistence/`、`Network/`、`Ledger/`、`SmartContract/`、`Wallets/`、`Plugins/`
  - `NeoSystem`：节点组合入口（Actors + Store + Plugins）
- `src/Neo.Extensions`（独立程序集，namespace 仍为 `Neo`/`Neo.Extensions`）
- `src/Neo.IO`（独立程序集）
- `src/Neo.Json`（独立程序集）

---

## 2. 目标模块（建议新增项目）

> 目标是“多程序集、清晰依赖、保持命名空间与协议兼容”。Phase 1-2 不做协议变更。

| 目标模块（程序集） | 目标职责 | 初期来源/迁移范围 |
|---|---|---|
| `Neo.Core` | 基础类型与最小依赖 primitives | `UInt160/UInt256/BigDecimal` 等从 `src/Neo` 拆出 |
| `Neo.Cryptography` | ECC/Hash/签名验证 | `src/Neo/Cryptography/**`（后续可做批量验签优化） |
| `Neo.Storage` | IStore/Snapshot/Providers/Cache | `src/Neo/Persistence/**` + 存储 Providers |
| `Neo.Network` | P2P 协议与同步 | `src/Neo/Network/**`（Legacy 兼容优先） |
| `Neo.Ledger` | 区块处理、状态管理、MemPool | `src/Neo/Ledger/**` |
| `Neo.SmartContract` | Native contracts / ApplicationEngine | `src/Neo/SmartContract/**` |
| `Neo.Wallets` | 钱包/账户/NEP6 | `src/Neo/Wallets/**` |
| `Neo.Plugins` | 插件 API 与生命周期 | `src/Neo/Plugins/**` |
| `Neo.Observability` | Metrics/Tracing/Logging | 新增（后续把日志/指标统一出口） |
| `Neo.Node` | 节点宿主（Application Layer） | 新增：托管/配置/启动 `NeoSystem` 或新内核 |

---

## 3. 推荐拆分顺序（与风险匹配）

1. `Neo.Core`（低风险）→ 验证 Type Forwarding 与构建链条
2. `Neo.Plugins` / `Neo.Observability`（低风险，高收益）
3. `Neo.Cryptography` / `Neo.Storage`（中风险）
4. `Neo.Network`（中高风险，需要互操作测试）
5. `Neo.Ledger` / `Neo.SmartContract` / `Neo.TxPool`（高风险，需要更强回归）

---

## 4. 关键“兼容表面”（拆分时必须保持不变）

- 二进制序列化：`Size/Serialize/Deserialize` 行为
- 哈希与签名：椭圆曲线与哈希实现不可改变语义
- 网络消息：消息 ID、payload 编码、压缩方式、握手 capability
- 存储格式：表空间/前缀/Key 编码、快照语义

