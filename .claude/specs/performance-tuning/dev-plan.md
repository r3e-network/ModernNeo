# Sprint 35: 性能调优开发计划

**版本:** 1.0
**日期:** 2025-12-21
**Sprint:** 35/36
**故事点:** 22

---

## 1. 目标

优化 Neo 区块链节点的运行时性能，重点关注：
- 序列化/反序列化热点
- 内存分配与 GC 压力
- 关键路径延迟
- 并发操作效率

---

## 2. 代码库分析结果

### 2.1 现有基准测试基础设施
- `benchmarks/Neo.Benchmarks/` - 核心类型基准测试
- `benchmarks/Neo.Orleans.Benchmarks/` - Orleans Grain 基准测试
- `benchmarks/Neo.Json.Benchmarks/` - JSON 序列化基准测试

### 2.2 已识别的性能热点

| 热点 | 位置 | 影响 |
|------|------|------|
| Activator.CreateInstance | `Neo.IO/MemoryExtensions.cs:43,57` | 反序列化时反射开销 |
| ToArray() 分配 | 多处序列化代码 | 频繁堆分配 |
| MessagePack vs Native | 序列化层 | 批量操作时 MessagePack 更优 |
| 缺少对象池 | 事务/区块处理 | GC 压力 |

### 2.3 优化机会

1. **ArrayPool/MemoryPool 使用** - 减少临时数组分配
2. **Span<T>/Memory<T> 优化** - 避免不必要的复制
3. **反射消除** - 使用源生成器替代 Activator
4. **批量操作优化** - 利用 MessagePack 批量序列化优势

---

## 3. 任务分解

### Task PT-1: 扩展基准测试覆盖 (5 SP)

**目标:** 建立完整的性能基线

**文件范围:**
- `benchmarks/Neo.Benchmarks/Benchmarks.BlockExecution.cs` (新建)
- `benchmarks/Neo.Benchmarks/Benchmarks.TransactionProcessing.cs` (新建)
- `benchmarks/Neo.Benchmarks/Benchmarks.Consensus.cs` (新建)

**验收标准:**
- 区块执行基准测试 (1-100 tx/block)
- 交易处理基准测试 (验证、序列化、内存池)
- 共识消息处理基准测试
- 所有基准测试可运行并产生基线数据

**测试命令:**
```bash
dotnet run -c Release --project benchmarks/Neo.Benchmarks -- --filter "*BlockExecution*" --warmup 3 --iteration 5
```

**依赖:** 无

---

### Task PT-2: 序列化热点优化 (5 SP)

**目标:** 减少序列化/反序列化的内存分配

**文件范围:**
- `src/Neo.IO/MemoryExtensions.cs` - 消除 Activator.CreateInstance
- `src/Neo.IO/BinaryReaderExtensions.cs` - 添加 Span 重载
- `src/Neo.IO/ISerializableExtensions.cs` - 优化 ToArray

**优化策略:**
1. 使用泛型约束 `where T : ISerializable, new()` 替代反射
2. 添加 `SerializeTo(Span<byte>)` 方法避免分配
3. 使用 `ArrayPool<byte>.Shared` 进行临时缓冲

**验收标准:**
- 反序列化性能提升 ≥20%
- 内存分配减少 ≥30%
- 所有现有测试通过

**测试命令:**
```bash
dotnet test tests/Neo.UnitTests --filter "FullyQualifiedName~IO" -v n
```

**依赖:** PT-1 (基线数据)

---

### Task PT-3: 对象池实现 (5 SP)

**目标:** 减少高频对象的 GC 压力

**文件范围:**
- `src/Neo.Core/Pooling/ObjectPool.cs` (新建)
- `src/Neo.Core/Pooling/PooledTransaction.cs` (新建)
- `src/Neo.Core/Pooling/PooledBuffer.cs` (新建)

**池化对象:**
1. `MemoryReader` - 反序列化读取器
2. `BinaryWriter` 缓冲区 - 序列化写入器
3. 临时字节数组 - 哈希计算、签名验证

**验收标准:**
- 对象池实现符合 `Microsoft.Extensions.ObjectPool` 模式
- 池化对象正确重置和回收
- GC Gen0 收集减少 ≥25%
- 单元测试覆盖率 ≥90%

**测试命令:**
```bash
dotnet test tests/Neo.UnitTests --filter "FullyQualifiedName~Pooling" -v n
```

**依赖:** 无 (可与 PT-1, PT-2 并行)

---

### Task PT-4: 并发优化与 GC 调优 (4 SP)

**目标:** 优化多线程场景和 GC 行为

**文件范围:**
- `src/Neo.Execution/ParallelExecutor.cs` - 并行执行优化
- `src/Neo.Node/runtimeconfig.template.json` - GC 配置
- `src/Neo.Node.AOT/runtimeconfig.template.json` - AOT GC 配置

**优化策略:**
1. 配置 Server GC 模式
2. 调整 GC 堆大小和代阈值
3. 使用 `GC.TryStartNoGCRegion` 保护关键路径
4. 优化 `Parallel.ForEachAsync` 分区策略

**验收标准:**
- Server GC 配置正确启用
- 关键路径 (区块执行) 无 GC 暂停
- 并行执行吞吐量提升 ≥15%
- 配置可通过环境变量覆盖

**测试命令:**
```bash
dotnet test tests/Neo.UnitTests --filter "FullyQualifiedName~Execution" -v n
```

**依赖:** PT-3 (对象池)

---

### Task PT-5: 性能回归测试框架 (3 SP)

**目标:** 建立持续性能监控

**文件范围:**
- `tests/Neo.PerformanceTests/PerformanceBaseline.cs` (新建)
- `tests/Neo.PerformanceTests/PerformanceRegressionTests.cs` (新建)
- `.github/workflows/performance.yml` (新建)

**功能:**
1. 性能基线存储和比较
2. 自动检测性能回归 (>10% 降级告警)
3. CI 集成性能测试

**验收标准:**
- 性能基线可序列化存储
- 回归检测准确率 ≥95%
- CI 工作流正确触发

**测试命令:**
```bash
dotnet test tests/Neo.PerformanceTests -v n
```

**依赖:** PT-1, PT-2, PT-3, PT-4

---

## 4. 任务依赖图

```
PT-1 (基准测试) ─────────────────────────────┐
                                              │
PT-2 (序列化优化) ──────────────────────────→ PT-5 (回归测试)
                                              │
PT-3 (对象池) ──────→ PT-4 (GC调优) ─────────┘
```

**并行执行策略:**
- 第一批: PT-1, PT-3 (并行)
- 第二批: PT-2, PT-4 (并行，依赖第一批)
- 第三批: PT-5 (依赖所有)

---

## 5. 验收标准汇总

| 指标 | 目标 | 测量方法 |
|------|------|----------|
| 序列化性能 | +20% | BenchmarkDotNet |
| 内存分配 | -30% | MemoryDiagnoser |
| GC Gen0 收集 | -25% | GC.CollectionCount |
| 并行吞吐量 | +15% | 区块执行基准 |
| 测试覆盖率 | ≥90% | dotnet test --collect |

---

## 6. 风险与缓解

| 风险 | 影响 | 缓解措施 |
|------|------|----------|
| 优化引入 Bug | 高 | 完整回归测试 |
| 性能提升不达标 | 中 | 迭代优化，优先高影响热点 |
| GC 配置不兼容 | 低 | 保留默认配置回退 |

---

## 7. 完成定义

- [ ] 所有任务代码完成并通过评审
- [ ] 单元测试覆盖率 ≥90%
- [ ] 性能基准测试通过
- [ ] 无性能回归
- [ ] 文档更新
