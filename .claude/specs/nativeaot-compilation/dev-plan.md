# Sprint 10: NativeAOT 编译 - 开发计划

**版本:** 1.1
**日期:** 2025-12-21
**Sprint:** 10 (第19-20周)
**故事点:** 18
**状态:** ✅ 已完成

---

## 1. 目标

启用 NativeAOT 编译并进行全面验证，包括运行时测试和性能基准测试。

---

## 2. 用户故事

| ID | 用户故事 | 故事点 | 优先级 | 状态 |
|----|----------|--------|--------|------|
| US-10.1 | NativeAOT 构建配置优化 | 5 | P0 | ✅ |
| US-10.2 | 运行时验证 | 8 | P0 | ✅ |
| US-10.3 | 性能基准测试 | 5 | P1 | ✅ |

---

## 3. 任务分解

### Task 1: NativeAOT 构建配置优化 (US-10.1) ✅
**ID:** T10-1
**状态:** 已完成

**完成的工作:**
1. ✅ 优化 NativeAOT 编译参数
   - `IlcOptimizationPreference=Speed`
   - `IlcGenerateStackTraceData=false`
   - `IlcFoldIdenticalMethodBodies=true`
2. ✅ 移除 `runtimeconfig.template.json` 警告
   - 删除 `src/Neo.Node.AOT/runtimeconfig.template.json`
   - 将配置迁移到 csproj 属性
3. ✅ 配置 Runtime Host Configuration
   - `ServerGarbageCollection=true`
   - `ConcurrentGarbageCollection=true`
   - `GarbageCollectionHighMemoryPercent=90`
   - `ThreadPoolMinThreads=16`
   - `ThreadPoolMaxThreads=256`

**修改的文件:**
- `src/Neo.Node.AOT/Neo.Node.AOT.csproj`
- 删除: `src/Neo.Node.AOT/runtimeconfig.template.json`

---

### Task 2: 运行时验证扩展 (US-10.2) ✅
**ID:** T10-2
**状态:** 已完成

**完成的工作:**
1. ✅ 扩展 Neo.Node.AOT 测试覆盖 (29 项测试)
   - Core Types: UInt160/UInt256 解析、比较、相等性
   - Cryptography: SHA256, RIPEMD160, Murmur32, Hash256, Hash160
   - JSON: 创建、序列化、解析、嵌套对象、特殊字符
   - Storage: Put/Get, Delete, Find, Snapshot, 隔离性
   - Protocol Types: MessageCommand, InventoryType, WitnessScope
   - IO Extensions: VarInt, VarString, Hex 转换
   - Edge Cases: 空数组、大数据、边界键
2. ✅ 添加 NativeAOT 集成测试 (7 项测试)
   - `NativeAOT_BinaryExists`
   - `NativeAOT_BinarySizeOptimal`
   - `NativeAOT_ExecutionSucceeds`
   - `NativeAOT_StartupTimeAcceptable`
   - `NativeAOT_AllTestGroupsPass`
   - `NativeAOT_ProjectConfigurationValid`
   - `NativeAOT_NoRuntimeConfigTemplateFiles`

**创建的文件:**
- `src/Neo.Node.AOT/Program.cs` (扩展)
- `tests/Neo.Node.Tests/UT_NativeAOT.cs` (新建)

---

### Task 3: 性能基准测试 (US-10.3) ✅
**ID:** T10-3
**状态:** 已完成

**完成的工作:**
1. ✅ 创建 NativeAOT 性能基准测试
   - Cryptography: SHA256 (小/中/大), RIPEMD160, Hash256, Hash160, Murmur32
   - JSON: Parse, Serialize, ParseAndAccess
   - Storage: Put, TryGet, Find, Snapshot
   - Core Types: UInt160/UInt256 Parse/ToString/Compare
   - IO: VarInt Read/Write, Hex Convert/Parse
   - Memory: ArrayCopy, SpanCopy, SliceOperator, AsMemory
   - Startup: CreateMemoryStore, CreateStoreAndSnapshot, CreateJsonObject

**创建的文件:**
- `benchmarks/Neo.Benchmarks/Benchmarks.NativeAOT.cs` (新建)

**性能结果 (JIT 模式):**
| 操作 | 平均时间 | 内存分配 |
|------|----------|----------|
| SHA256 (9B) | 214.3 ns | 56 B |
| SHA256 (1KB) | 430.5 ns | 56 B |
| SHA256 (1MB) | 210.9 μs | 56 B |
| RIPEMD160 | 146.0 ns | 240 B |
| Hash256 | 419.0 ns | 112 B |
| Hash160 | 361.1 ns | 296 B |
| Murmur32 | 263.5 ns | 40 B |

---

## 4. 验收标准

- [x] NativeAOT 编译无警告
- [x] 二进制文件大小 < 5MB (实际: **2.2MB**)
- [x] 所有核心功能测试通过 (29/29)
- [x] 启动时间 < 100ms (实际: **~40ms**)
- [x] 性能基准测试完成并记录

---

## 5. 任务依赖图

```
T10-1 (构建配置) ✅ ──→ T10-2 (运行时验证) ✅ ──→ T10-3 (性能基准) ✅
```

**执行完成:** 所有任务已按顺序完成

---

## 6. 风险与缓解

| 风险 | 影响 | 缓解措施 | 状态 |
|------|------|----------|------|
| 某些功能在 AOT 下不工作 | 高 | 保留 JIT 回退选项 | ✅ 无问题 |
| 二进制文件过大 | 中 | 使用 IlcTrimMode=link | ✅ 2.2MB |
| 跨平台编译问题 | 中 | 优先支持 linux-x64 | ✅ 已验证 |

---

## 7. 完成定义 (DoD)

- [x] 代码完成并通过评审
- [x] 单元测试 (29 项 AOT 测试 + 7 项集成测试)
- [x] 集成测试通过
- [x] NativeAOT 编译成功
- [x] 性能基准测试完成
- [x] 文档更新

---

## 8. 交付物总结

### 修改的文件
1. `src/Neo.Node.AOT/Neo.Node.AOT.csproj` - 优化 AOT 配置
2. `src/Neo.Node.AOT/Program.cs` - 扩展测试覆盖 (29 项)

### 新建的文件
1. `tests/Neo.Node.Tests/UT_NativeAOT.cs` - 集成测试 (7 项)
2. `benchmarks/Neo.Benchmarks/Benchmarks.NativeAOT.cs` - 性能基准

### 删除的文件
1. `src/Neo.Node.AOT/runtimeconfig.template.json` - 迁移到 csproj

### 关键指标
- **二进制大小:** 2.2 MB (目标 < 5MB) ✅
- **AOT 测试:** 29 项全部通过 ✅
- **集成测试:** 7 项全部通过 ✅
- **执行时间:** ~40ms ✅
