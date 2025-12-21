# Sprint 34: 分布式追踪实现 - 开发计划

**版本:** 1.0
**日期:** 2025-12-21
**Sprint:** 34
**故事点:** 18

---

## 1. 概述

实现跨服务分布式追踪，使用 OpenTelemetry 标准，采用关联模式（Option B）进行 P2P 跨节点追踪。

### 1.1 设计决策

| 决策 | 选择 | 理由 |
|------|------|------|
| P2P 追踪策略 | 关联模式 (Option B) | 零协议风险，使用 span links + txid/blockhash 关联 |
| 上下文传播 | W3C Trace Context | 行业标准，兼容性好 |
| 采样策略 | ParentBased + RateLimiting | 高吞吐场景下控制开销 |

---

## 2. 任务分解

### Task 1: 核心追踪 + 传播原语
**ID:** DT-1
**故事点:** 5
**依赖:** 无
**可并行:** 是

**文件范围:**
- `src/Neo.Observability/Tracing/ITracer.cs` (扩展)
- `src/Neo.Observability/Tracing/ISpan.cs` (扩展)
- `src/Neo.Observability/Tracing/OpenTelemetryTracer.cs` (扩展)
- `src/Neo.Observability/Tracing/Propagation/` (新建)
  - `ITextMapPropagator.cs`
  - `W3CTraceContextPropagator.cs`
  - `TraceContext.cs`
- `tests/Neo.UnitTests/Observability/UT_Propagation.cs` (新建)

**交付物:**
- 扩展 ITracer 支持 `StartSpan(name, kind, parentContext)` 和 `StartSpan(name, kind, links)`
- 实现 W3C Trace Context 提取/注入
- 单元测试覆盖率 ≥90%

**测试命令:**
```bash
dotnet test tests/Neo.UnitTests/Neo.UnitTests.csproj --filter "FullyQualifiedName~Observability"
```

---

### Task 2: RPC/gRPC 入口追踪
**ID:** DT-2
**故事点:** 3
**依赖:** DT-1
**可并行:** 否 (依赖 DT-1)

**文件范围:**
- `src/Neo.RPC/TracingMiddleware.cs` (新建)
- `src/Neo.RPC/RpcProcessor.cs` (集成)
- `src/Neo.Grpc/Interceptors/TracingInterceptor.cs` (新建)
- `tests/Neo.UnitTests/RPC/UT_TracingMiddleware.cs` (新建)

**交付物:**
- RPC 请求自动创建 Server span
- 从 HTTP headers 提取 W3C trace context
- gRPC 拦截器实现追踪
- 单元测试覆盖率 ≥90%

**测试命令:**
```bash
dotnet test tests/Neo.UnitTests/Neo.UnitTests.csproj --filter "FullyQualifiedName~RPC"
```

---

### Task 3: P2P 追踪集成 (关联模式)
**ID:** DT-3
**故事点:** 4
**依赖:** DT-1
**可并行:** 是 (与 DT-2 并行)

**文件范围:**
- `src/Neo.Observability/Tracing/P2PTracing.cs` (新建)
- `src/Neo/Network/P2P/RemoteNode.ProtocolHandler.cs` (集成)
- `src/Neo/Ledger/TransactionRouter.cs` (集成)
- `tests/Neo.UnitTests/Observability/UT_P2PTracing.cs` (新建)

**交付物:**
- P2P 消息处理 span (按消息类型)
- 使用 span links 关联 txid/blockhash
- 交易路由追踪
- 单元测试覆盖率 ≥90%

**测试命令:**
```bash
dotnet test tests/Neo.UnitTests/Neo.UnitTests.csproj --filter "FullyQualifiedName~P2PTracing"
```

---

### Task 4: 共识 + 区块执行追踪
**ID:** DT-4
**故事点:** 4
**依赖:** DT-1
**可并行:** 是 (与 DT-2, DT-3 并行)

**文件范围:**
- `src/Neo.Observability/Tracing/ConsensusTracing.cs` (新建)
- `src/Neo.Observability/Tracing/BlockTracing.cs` (新建)
- `src/Neo/Ledger/Blockchain.cs` (集成 - Persist span)
- `src/Neo.Execution/ParallelExecutor.cs` (集成)
- `tests/Neo.UnitTests/Observability/UT_ConsensusTracing.cs` (新建)
- `tests/Neo.UnitTests/Observability/UT_BlockTracing.cs` (新建)

**交付物:**
- 共识轮次 span
- 区块持久化 span
- 并行执行追踪
- 单元测试覆盖率 ≥90%

**测试命令:**
```bash
dotnet test tests/Neo.UnitTests/Neo.UnitTests.csproj --filter "FullyQualifiedName~Tracing"
```

---

### Task 5: 采样 + 导出器配置
**ID:** DT-5
**故事点:** 2
**依赖:** DT-1, DT-2
**可并行:** 否 (依赖 DT-1, DT-2)

**文件范围:**
- `src/Neo.Observability/Tracing/Sampling/` (新建)
  - `RateLimitingSampler.cs`
  - `SamplerConfiguration.cs`
- `src/Neo.Node/Program.cs` (添加 WithTracing)
- `src/Neo.Node/config.json` (采样配置)
- `tests/Neo.UnitTests/Observability/UT_Sampling.cs` (新建)

**交付物:**
- ParentBased 采样器
- 速率限制采样器
- OTLP 导出器配置
- 配置文件支持
- 单元测试覆盖率 ≥90%

**测试命令:**
```bash
dotnet test tests/Neo.UnitTests/Neo.UnitTests.csproj --filter "FullyQualifiedName~Sampling"
```

---

## 3. 依赖关系图

```
DT-1 (核心追踪原语)
  ├── DT-2 (RPC/gRPC 追踪)
  │     └── DT-5 (采样配置)
  ├── DT-3 (P2P 追踪) [并行]
  └── DT-4 (共识/区块追踪) [并行]
```

**并行执行策略:**
- Phase 1: DT-1 (必须先完成)
- Phase 2: DT-2, DT-3, DT-4 (可并行)
- Phase 3: DT-5 (依赖 DT-1, DT-2)

---

## 4. 验收标准

- [ ] 所有任务单元测试覆盖率 ≥90%
- [ ] RPC 请求自动创建追踪 span
- [ ] P2P 消息使用 span links 关联
- [ ] 共识和区块处理有完整追踪
- [ ] 采样策略可配置
- [ ] 无性能回归 (基准测试验证)

---

## 5. 风险与缓解

| 风险 | 影响 | 缓解措施 |
|------|------|----------|
| 追踪开销影响性能 | 中 | 使用采样，默认低采样率 |
| Activity.Current 跨 Akka 丢失 | 高 | 显式传递 ActivityContext |
| 高基数 span 属性 | 中 | 限制属性数量，使用事件 |
