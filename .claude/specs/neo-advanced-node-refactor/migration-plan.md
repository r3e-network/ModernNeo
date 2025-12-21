# Neo 模块迁移计划

## 概述

本文档详细描述了将 src/Neo/ 旧模块迁移到新模块化架构的完整计划。

## 当前状态

### 已完成的新模块 (15 个)

| 模块 | 文件数 | 状态 | 描述 |
|------|--------|------|------|
| Neo.Core | 12 | ✅ | 基础类型 + 接口抽象 |
| Neo.Cryptography | 16 | ✅ | 加密算法 |
| Neo.Storage | 11 | ✅ | 存储抽象 |
| Neo.Observability | 11 | ✅ | 可观测性 |
| Neo.IO | - | ✅ | 序列化接口 |
| Neo.Json | - | ✅ | JSON 处理 |
| Neo.Extensions | - | ✅ | 扩展方法 |
| Neo.Node | 1 | ✅ | 最小可运行节点 |
| Neo.TxPool | 6 | ✅ | 交易池抽象 |
| Neo.RPC | 5 | ✅ | RPC 服务抽象 |
| Neo.Ledger | 4 | ✅ | 账本抽象 |
| Neo.Consensus | 5 | ✅ | 共识抽象 |
| Neo.Orleans | 20+ | ✅ | Orleans Grains |
| Neo.Protocol | 34 | ✅ | 协议类型 |
| Neo.Plugins | 2 | ✅ | 插件基础 |

### 待迁移的旧模块 (src/Neo/)

| 目录 | 文件数 | 目标模块 | 优先级 | 阻塞因素 |
|------|--------|----------|--------|----------|
| IEventHandlers/ | 10 | Neo.Core | P1 | 无 |
| Sign/ | 3 | Neo.Cryptography | P1 | 无 |
| Builders/ | 8 | Neo.Builders (新) | P2 | 依赖 Payloads |
| Extensions/ | 10+ | Neo.Extensions | P2 | 部分依赖 SmartContract |
| Persistence/ | 3 | Neo.Storage | P2 | 依赖 SmartContract |
| Ledger/ | 8 | Neo.Ledger | P3 | 依赖 Akka, SmartContract |
| Wallets/ | 13 | Neo.Wallets (新) | P3 | 依赖 SmartContract |
| Network/P2P/ | 37 | Neo.Network (新) | P4 | 依赖 Akka, SmartContract |
| SmartContract/ | 74 | Neo.SmartContract (新) | P5 | 核心依赖链 |
| IO/Actors/ | 2 | 废弃 | - | Orleans 替代 |

## 迁移策略

### Phase 1: 低风险迁移 (1-2 周)

#### 1.1 IEventHandlers → Neo.Core
- 10 个事件处理器接口
- 无外部依赖
- 使用 TypeForwarding 保持兼容

#### 1.2 Sign → Neo.Cryptography
- ISigner, SignerManager, SignException
- 依赖 Neo.Cryptography 已有类型

### Phase 2: 中等风险迁移 (2-3 周)

#### 2.1 创建 Neo.Builders 模块
- TransactionBuilder, SignerBuilder, WitnessBuilder 等
- 依赖 Neo.Protocol 中的 Payload 类型

#### 2.2 Persistence → Neo.Storage
- DataCache, StoreCache, ClonedCache
- 需要解耦 SmartContract 依赖

### Phase 3: 高风险迁移 (4-6 周)

#### 3.1 创建 Neo.Wallets 模块
- Wallet, WalletAccount, KeyPair
- NEP6 实现
- 需要解耦 SmartContract 依赖

#### 3.2 Ledger 完整迁移
- Blockchain, MemoryPool, HeaderCache
- 需要替换 Akka Actor 为 Orleans Grain

### Phase 4: 核心模块迁移 (6-8 周)

#### 4.1 创建 Neo.Network 模块
- LocalNode, RemoteNode, Peer
- P2P 消息处理
- 需要替换 Akka Actor 为 Orleans Grain

#### 4.2 创建 Neo.SmartContract 模块
- ApplicationEngine
- Native Contracts
- Contract Manifest
- 这是最复杂的迁移，需要仔细规划

## 循环依赖解决方案

### 核心循环依赖链

```
Network.P2P.Payloads (Transaction, Block)
    ↓ implements
IVerifiable (需要 DataCache 验证)
    ↓ depends on
Neo.Persistence (DataCache)
    ↓ used by
Neo.SmartContract (ApplicationEngine)
    ↓ uses
IInteroperable (需要 StackItem)
    ↓ depends on
Neo.VM (StackItem, IReferenceCounter)
    ↓ used by
Transaction, Block (实现 IInteroperable)
    ↑ circular!
```

### 解决方案

1. **接口分离** (已完成)
   - IVerifiableBase (无 DataCache 依赖)
   - IInteroperableBase (无 VM 依赖)
   - IBlockData, ITransactionData (纯数据契约)

2. **服务接口** (已完成)
   - IVerificationService
   - IStackItemConverter

3. **Orleans 替代 Akka** (已完成)
   - BlockchainGrain 替代 Blockchain Actor
   - MemoryPoolGrain 替代 MemPool Actor
   - LocalNodeGrain 替代 LocalNode Actor

4. **TypeForwarding** (持续使用)
   - 保持二进制兼容性
   - 允许渐进式迁移

## 迁移检查清单

### 每个模块迁移前

- [ ] 分析依赖关系
- [ ] 识别循环依赖
- [ ] 创建接口抽象
- [ ] 设计 TypeForwarding 策略

### 每个模块迁移后

- [ ] 构建成功
- [ ] 所有测试通过
- [ ] TypeForwarding 配置正确
- [ ] 更新架构文档

## 风险评估

| 风险 | 影响 | 缓解措施 |
|------|------|----------|
| 破坏现有 API | 高 | TypeForwarding + 接口抽象 |
| 测试覆盖不足 | 中 | 迁移前增加测试 |
| 循环依赖 | 高 | 接口分离 + 服务注入 |
| Akka 依赖 | 高 | Orleans 替代方案 |

## 时间线

| 阶段 | 时间 | 目标 |
|------|------|------|
| Phase 1 | 第 1-2 周 | IEventHandlers, Sign |
| Phase 2 | 第 3-5 周 | Builders, Persistence |
| Phase 3 | 第 6-11 周 | Wallets, Ledger |
| Phase 4 | 第 12-20 周 | Network, SmartContract |

**总计: 约 5 个月完成完整迁移**

## 下一步行动

1. 开始 Phase 1.1: 迁移 IEventHandlers 到 Neo.Core
2. 为每个迁移创建详细的实施计划
3. 持续更新本文档
