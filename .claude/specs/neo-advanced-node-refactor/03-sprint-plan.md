# Neo Advanced Node (NeoAN) - Sprint 计划

**版本:** 1.0
**日期:** 2025-12-15
**项目:** neo-advanced-node-refactor
**周期:** 18 个月 (36 sprints × 2 周)
**团队:** 5-8 核心开发者

---

## 1. Sprint 框架

### 1.1 Sprint 结构
- **周期:** 2 周/Sprint
- **速度:** 40-60 故事点/Sprint
- **仪式:**
  - Sprint 计划: 第1天 (4小时)
  - 每日站会: 15分钟
  - Sprint 评审: 最后一天 (2小时)
  - Sprint 回顾: 最后一天 (1小时)

### 1.2 故事点规模 (Fibonacci)
| 点数 | 工作量 |
|------|--------|
| 1 | < 4 小时 |
| 2 | 4-8 小时 |
| 3 | 1-2 天 |
| 5 | 3-5 天 |
| 8 | 1-2 周 |
| 13 | > 2 周 (需拆分) |

### 1.3 完成定义 (DoD)
- [ ] 代码完成并通过评审
- [ ] 单元测试 (>80% 覆盖率)
- [ ] 集成测试通过
- [ ] 文档更新
- [ ] 性能基准测试
- [ ] 无关键 Bug
- [ ] 合并到功能分支

---

## 2. PHASE 1: 基础设施现代化 (Sprint 1-12)

### Sprint 1: Orleans 基础设施 (第1-2周)

**目标:** 建立 Orleans Silo 基础设施

| ID | 用户故事 | 故事点 |
|----|----------|--------|
| US-1.1 | Orleans 项目搭建 | 5 |
| US-1.2 | Grain 接口定义 | 8 |
| US-1.3 | RocksDB Grain 存储 | 8 |
| US-1.4 | Orleans TestKit 设置 | 3 |
| **总计** | | **24** |

**关键任务:**
- 创建 `src/Neo.Orleans/` 项目
- 定义 5 个核心 Grain 接口
- 实现 RocksDB 状态持久化

---

### Sprint 2: BlockchainGrain 迁移 (第3-4周)

**目标:** 迁移 Blockchain Actor 到 BlockchainGrain

| ID | 用户故事 | 故事点 |
|----|----------|--------|
| US-2.1 | BlockchainGrain 实现 | 13 |
| US-2.2 | Blockchain Observer 模式 | 5 |
| US-2.3 | BlockchainGrain 测试 | 5 |
| **总计** | | **23** |

**关键任务:**
- 实现 `PersistBlockAsync()`, `GetHeightAsync()`, `GetBlockAsync()`
- 实现观察者订阅机制
- 90%+ 测试覆盖率

---

### Sprint 3: MemoryPoolGrain 迁移 (第5-6周)

**目标:** 迁移 MemoryPool 到 MemoryPoolGrain

| ID | 用户故事 | 故事点 |
|----|----------|--------|
| US-3.1 | MemoryPoolGrain 实现 | 8 |
| US-3.2 | 交易优先级队列 | 5 |
| US-3.3 | MemoryPool 性能测试 | 3 |
| US-3.4 | Grain 间通信 | 5 |
| **总计** | | **21** |

---

### Sprint 4: 网络 Grain 迁移 (第7-8周)

**目标:** 迁移 LocalNode 和 RemoteNode Actor

| ID | 用户故事 | 故事点 |
|----|----------|--------|
| US-4.1 | LocalNodeGrain 实现 | 8 |
| US-4.2 | RemoteNodeGrain 实现 | 8 |
| US-4.3 | 网络集成测试 | 5 |
| **总计** | | **21** |

---

### Sprint 5: ConsensusGrain + 兼容层 (第9-10周)

**目标:** 完成 Grain 迁移，添加 Akka.NET 兼容层

| ID | 用户故事 | 故事点 |
|----|----------|--------|
| US-5.1 | ConsensusGrain 实现 | 8 |
| US-5.2 | Akka.NET 兼容适配器 | 5 |
| US-5.3 | 端到端集成测试 | 5 |
| **总计** | | **18** |

---

### Sprint 6: MessagePack 基础 (第11-12周)

**目标:** 建立 MessagePack 序列化基础设施

| ID | 用户故事 | 故事点 |
|----|----------|--------|
| US-6.1 | MessagePack 项目搭建 | 3 |
| US-6.2 | 核心类型 Formatter | 8 |
| US-6.3 | Transaction Formatter | 8 |
| US-6.4 | 序列化基准测试 | 3 |
| **总计** | | **22** |

---

### Sprint 7: MessagePack 复杂类型 (第13-14周)

**目标:** 实现复杂类型的 MessagePack Formatter

| ID | 用户故事 | 故事点 |
|----|----------|--------|
| US-7.1 | Block Formatter | 8 |
| US-7.2 | Payload Formatters | 8 |
| US-7.3 | Orleans 集成 | 5 |
| **总计** | | **21** |

---

### Sprint 8: MessagePack 集成测试 (第15-16周)

**目标:** 完成 MessagePack 集成和全面测试

| ID | 用户故事 | 故事点 |
|----|----------|--------|
| US-8.1 | SmartContract 类型 Formatter | 5 |
| US-8.2 | 向后兼容测试 | 5 |
| US-8.3 | 功能开关实现 | 3 |
| US-8.4 | 性能验证 | 3 |
| **总计** | | **16** |

---

### Sprint 9: NativeAOT 准备 (第17-18周)

**目标:** 准备代码库支持 NativeAOT 编译

| ID | 用户故事 | 故事点 |
|----|----------|--------|
| US-9.1 | NativeAOT 兼容性审计 | 5 |
| US-9.2 | 反射消除 | 8 |
| US-9.3 | Trim 警告修复 | 5 |
| **总计** | | **18** |

---

### Sprint 10: NativeAOT 编译 (第19-20周)

**目标:** 启用 NativeAOT 编译并验证

| ID | 用户故事 | 故事点 |
|----|----------|--------|
| US-10.1 | NativeAOT 构建配置 | 5 |
| US-10.2 | 运行时验证 | 8 |
| US-10.3 | 性能基准测试 | 5 |
| **总计** | | **18** |

---

### Sprint 11: Phase 1 集成测试 (第21-22周)

**目标:** 集成所有 Phase 1 组件并全面测试

| ID | 用户故事 | 故事点 |
|----|----------|--------|
| US-11.1 | 全系统集成 | 8 |
| US-11.2 | 兼容性测试 | 5 |
| US-11.3 | 性能验证 | 5 |
| US-11.4 | 文档更新 | 3 |
| **总计** | | **21** |

---

### Sprint 12: Phase 1 加固发布 (第23-24周)

**目标:** 加固 Phase 1 交付物并准备发布

| ID | 用户故事 | 故事点 |
|----|----------|--------|
| US-12.1 | Bug 修复与稳定化 | 8 |
| US-12.2 | 安全审计 | 5 |
| US-12.3 | 发布准备 | 5 |
| US-12.4 | Phase 1 回顾 | 2 |
| **总计** | | **20** |

---

## 3. PHASE 2: 网络与存储优化 (Sprint 13-24)

### Sprint 13-18: QUIC 协议栈 (第25-36周)

| Sprint | 目标 | 故事点 |
|--------|------|--------|
| 13 | QUIC 连接管理 | 22 |
| 14 | QUIC 流多路复用 | 20 |
| 15 | 协议协商机制 | 18 |
| 16 | TLS 1.3 集成 | 16 |
| 17 | QUIC 性能优化 | 20 |
| 18 | QUIC 集成测试 | 18 |

### Sprint 19-22: libp2p 集成 (第37-44周)

| Sprint | 目标 | 故事点 |
|--------|------|--------|
| 19 | libp2p 基础设施 | 20 |
| 20 | DHT 节点发现 | 22 |
| 21 | mDNS 本地发现 | 16 |
| 22 | libp2p 集成测试 | 18 |

### Sprint 23-24: LMDB 读缓存 (第45-48周)

| Sprint | 目标 | 故事点 |
|--------|------|--------|
| 23 | LMDB 缓存层实现 | 22 |
| 24 | 缓存预热与测试 | 18 |

---

## 4. PHASE 3: API 与可观测性 (Sprint 25-36)

### Sprint 25-28: gRPC API (第49-56周)

| Sprint | 目标 | 故事点 |
|--------|------|--------|
| 25 | gRPC Proto 定义 | 18 |
| 26 | gRPC 服务实现 | 22 |
| 27 | gRPC 流式传输 | 20 |
| 28 | gRPC 集成测试 | 16 |

### Sprint 29-32: GraphQL 查询层 (第57-64周)

| Sprint | 目标 | 故事点 |
|--------|------|--------|
| 29 | GraphQL Schema 定义 | 18 |
| 30 | GraphQL 解析器实现 | 22 |
| 31 | GraphQL 订阅功能 | 20 |
| 32 | GraphQL 集成测试 | 16 |

### Sprint 33-36: OpenTelemetry 与发布 (第65-72周)

| Sprint | 目标 | 故事点 |
|--------|------|--------|
| 33 | OpenTelemetry 集成 | 20 |
| 34 | 分布式追踪实现 | 18 |
| 35 | 性能调优 | 22 |
| 36 | 最终发布准备 | 16 |

---

## 5. 里程碑总览

| 里程碑 | Sprint | 日期 | 关键交付物 |
|--------|--------|------|-----------|
| M1 | 5 | 2026-02 | Orleans 迁移完成 |
| M2 | 8 | 2026-04 | MessagePack 集成完成 |
| M3 | 12 | 2026-06 | Phase 1 发布 |
| M4 | 18 | 2026-09 | QUIC 网络上线 |
| M5 | 24 | 2026-12 | Phase 2 发布 |
| M6 | 28 | 2027-02 | gRPC API 发布 |
| M7 | 32 | 2027-04 | GraphQL 发布 |
| M8 | 36 | 2027-06 | 最终发布 |

---

## 6. 风险与缓解

| 风险 | 影响 Sprint | 缓解措施 |
|------|------------|----------|
| Orleans 学习曲线 | 1-5 | 提前培训，结对编程 |
| NativeAOT 兼容性 | 9-10 | 渐进式启用，保留 JIT |
| QUIC 库稳定性 | 13-18 | 使用 MsQuic，保留 TCP |
| 性能回归 | 全程 | 持续基准测试 |

---

## 7. 依赖关系

```
Sprint 1 → Sprint 2 → Sprint 3 → Sprint 4 → Sprint 5
                                              ↓
Sprint 6 → Sprint 7 → Sprint 8 ─────────────→ Sprint 11 → Sprint 12
                                              ↑
Sprint 9 → Sprint 10 ─────────────────────────┘
```

**关键路径:** Sprint 1 → 2 → 3 → 4 → 5 → 11 → 12
