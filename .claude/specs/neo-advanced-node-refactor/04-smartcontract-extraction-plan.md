# Neo.SmartContract 模块提取计划

**创建日期:** 2025-12-16
**优先级:** P5 (最高复杂度)
**预计时间:** 6-8 周

---

## 1. 依赖分析总结

### 1.1 文件统计

| 类别 | 文件数 | 描述 |
|------|--------|------|
| **总计** | 74 | SmartContract 目录下所有 .cs 文件 |
| **ApplicationEngine** | 8 | 核心执行引擎 (partial classes) |
| **Native Contracts** | 24 | 原生合约实现 |
| **Manifest** | 8 | 合约清单类型 |
| **Iterators** | 2 | 存储迭代器 |
| **其他** | 32 | 辅助类型、接口、序列化器 |

### 1.2 外部依赖分析

#### Neo.VM 依赖 (🔴 关键阻塞器)

| 依赖类型 | 文件数 | 使用场景 |
|----------|--------|----------|
| `Neo.VM` | 45 | ExecutionEngine, Script, OpCode |
| `Neo.VM.Types` | 38 | StackItem, Buffer, Array, Struct |
| `IReferenceCounter` | 15+ | ToStackItem() 方法参数 |
| `ScriptBuilder` | 5 | 脚本构建 |

**关键耦合点:**
```csharp
// IInteroperable.cs - 核心 VM 耦合
public interface IInteroperable : IInteroperableBase
{
    void FromStackItem(StackItem stackItem);
    StackItem ToStackItem(IReferenceCounter? referenceCounter);
}

// ApplicationEngine.cs - 继承 ExecutionEngine
public partial class ApplicationEngine : ExecutionEngine
```

#### Neo.Persistence 依赖

| 文件 | 依赖类型 |
|------|----------|
| ApplicationEngine.cs | DataCache |
| ExecutionContextState.cs | DataCache |
| Helper.cs | DataCache |
| Native/*.cs (15 files) | DataCache, IReadOnlyStore |

#### Neo.Network.P2P.Payloads 依赖

| 文件 | 依赖类型 |
|------|----------|
| ApplicationEngine.cs | Block, Transaction, IVerifiable |
| LogEventArgs.cs | IVerifiable |
| NotifyEventArgs.cs | IVerifiable |
| Native/LedgerContract.cs | Block, Transaction, Header |
| Native/TransactionState.cs | Transaction |

---

## 2. 分层提取策略

### 2.1 提取层次

```
Layer 0: Neo.SmartContract.Abstractions (新模块)
├── 无 VM 依赖的接口和类型
├── ContractParameterType, CallFlags, TriggerType
└── 合约清单基础类型

Layer 1: Neo.SmartContract.Manifest (新模块)
├── ContractManifest, ContractAbi
├── ContractMethodDescriptor, ContractEventDescriptor
└── 依赖 Layer 0

Layer 2: Neo.SmartContract.Core (新模块)
├── ContractState, NefFile, StorageKey, StorageItem
├── IInteroperable (保留 VM 依赖)
└── 依赖 Layer 0, 1, Neo.VM

Layer 3: Neo.SmartContract.Native (新模块)
├── NativeContract 基类
├── 所有原生合约实现
└── 依赖 Layer 0, 1, 2, Neo.VM, Neo.Persistence

Layer 4: Neo.SmartContract.Execution (新模块)
├── ApplicationEngine
├── InteropService 注册
└── 依赖所有层
```

### 2.2 接口抽象策略

#### 已完成的接口

| 接口 | 位置 | 用途 |
|------|------|------|
| `IInteroperableBase` | Neo.Core | 无 VM 依赖的标记接口 |
| `IStackItemConverter<T,R>` | Neo.Core | VM 转换服务接口 |
| `IVerifiableBase` | Neo.Core | 无 DataCache 依赖的验证接口 |
| `IBlockData` | Neo.Core | 区块数据契约 |
| `ITransactionData` | Neo.Core | 交易数据契约 |

#### 需要创建的接口

| 接口 | 目标位置 | 用途 |
|------|----------|------|
| `IContractState` | Neo.Core | 合约状态数据契约 |
| `IStorageContext` | Neo.Core | 存储上下文抽象 |
| `IApplicationEngine` | Neo.Core | 执行引擎抽象 |
| `INativeContract` | Neo.Core | 原生合约抽象 |
| `IExecutionContext` | Neo.Core | 执行上下文抽象 |

---

## 3. 详细提取步骤

### Phase 1: 基础类型提取 (1 周)

#### Step 1.1: 创建 Neo.SmartContract.Abstractions

**目标文件:**
- `ContractParameterType.cs` → 枚举，无依赖
- `CallFlags.cs` → 枚举，无依赖
- `TriggerType.cs` → 枚举，无依赖
- `ContractBasicMethod.cs` → 常量类，无依赖
- `MethodToken.cs` → 结构体，无依赖

**操作:**
1. 创建 `src/Neo.SmartContract.Abstractions/` 项目
2. 移动上述文件
3. 添加 TypeForwarding 到 src/Neo/

#### Step 1.2: 提取 Manifest 类型

**目标文件:**
- `Manifest/ContractAbi.cs`
- `Manifest/ContractMethodDescriptor.cs`
- `Manifest/ContractEventDescriptor.cs`
- `Manifest/ContractParameterDefinition.cs`
- `Manifest/ContractGroup.cs`
- `Manifest/ContractPermission.cs`
- `Manifest/ContractPermissionDescriptor.cs`
- `Manifest/ContractManifest.cs`
- `Manifest/WildCardContainer.cs`

**依赖处理:**
- 这些类型实现 `IInteroperable`，需要保留 VM 依赖
- 创建 `IContractManifestData` 接口用于无 VM 场景

### Phase 2: 核心类型提取 (2 周)

#### Step 2.1: 提取存储相关类型

**目标文件:**
- `StorageKey.cs`
- `StorageItem.cs`
- `StorageContext.cs`
- `KeyBuilder.cs`

**依赖处理:**
- `StorageItem` 实现 `IInteroperable`
- 创建 `IStorageItem` 数据接口

#### Step 2.2: 提取合约状态类型

**目标文件:**
- `ContractState.cs`
- `NefFile.cs`
- `Contract.cs`
- `DeployedContract.cs`

**依赖处理:**
- `ContractState` 实现 `IInteroperable`
- 创建 `IContractStateData` 接口

### Phase 3: Native Contracts 提取 (2 周)

#### Step 3.1: 提取 NativeContract 基类

**目标文件:**
- `Native/NativeContract.cs`
- `Native/ContractMethodAttribute.cs`
- `Native/ContractEventAttribute.cs`
- `Native/ContractMethodMetadata.cs`
- `Native/IHardforkActivable.cs`

**依赖处理:**
- `NativeContract` 使用 `ScriptBuilder` 生成脚本
- 需要保留 VM 依赖

#### Step 3.2: 提取具体 Native Contracts

**按依赖顺序:**
1. `StdLib.cs` - 无其他 Native 依赖
2. `CryptoLib.cs` - 无其他 Native 依赖
3. `LedgerContract.cs` - 依赖 Block, Transaction
4. `PolicyContract.cs` - 依赖 LedgerContract
5. `ContractManagement.cs` - 依赖 PolicyContract
6. `NeoToken.cs` - 依赖 PolicyContract, LedgerContract
7. `GasToken.cs` - 依赖 NeoToken
8. `RoleManagement.cs` - 依赖 NeoToken
9. `OracleContract.cs` - 依赖多个
10. `Notary.cs` - 依赖多个
11. `Treasury.cs` - 依赖多个

### Phase 4: ApplicationEngine 提取 (2 周)

#### Step 4.1: 创建执行引擎抽象

**新接口:**
```csharp
// Neo.Core/Interfaces/IApplicationEngine.cs
public interface IApplicationEngine
{
    TriggerType Trigger { get; }
    IVerifiable? ScriptContainer { get; }
    long FeeConsumed { get; }
    long GasLeft { get; }
    UInt160? CurrentScriptHash { get; }
    UInt160? CallingScriptHash { get; }
    UInt160? EntryScriptHash { get; }
    IReadOnlyList<INotifyEventArgs> Notifications { get; }
}
```

#### Step 4.2: 提取 ApplicationEngine

**目标文件:**
- `ApplicationEngine.cs`
- `ApplicationEngine.Contract.cs`
- `ApplicationEngine.Crypto.cs`
- `ApplicationEngine.Helper.cs`
- `ApplicationEngine.Iterator.cs`
- `ApplicationEngine.OpCodePrices.cs`
- `ApplicationEngine.Runtime.cs`
- `ApplicationEngine.Storage.cs`

**依赖处理:**
- 继承 `Neo.VM.ExecutionEngine` - 无法解耦
- 使用 `DataCache` - 通过接口抽象
- 使用 `Block`, `Transaction` - 通过 `IBlockData`, `ITransactionData`

### Phase 5: 辅助类型提取 (1 周)

**目标文件:**
- `BinarySerializer.cs`
- `JsonSerializer.cs`
- `Helper.cs`
- `InteropDescriptor.cs`
- `InteropParameterDescriptor.cs`
- `ExecutionContextState.cs`
- `ContractParameter.cs`
- `ContractParametersContext.cs`
- `ContractTask.cs`
- `ContractTaskAwaiter.cs`
- `ContractTaskMethodBuilder.cs`
- `IDiagnostic.cs`
- `IApplicationEngineProvider.cs`
- `IInteroperableVerifiable.cs`
- `LogEventArgs.cs`
- `NotifyEventArgs.cs`
- `MaxLengthAttribute.cs`
- `ValidatorAttribute.cs`
- `IExecutionMetrics.cs`
- `NullExecutionMetrics.cs`
- `DefaultExecutionMetrics.cs`
- `Iterators/IIterator.cs`
- `Iterators/StorageIterator.cs`

---

## 4. TypeForwarding 策略

### 4.1 转发配置

```csharp
// src/Neo/TypeForwards.SmartContract.cs
[assembly: TypeForwardedTo(typeof(Neo.SmartContract.ContractParameterType))]
[assembly: TypeForwardedTo(typeof(Neo.SmartContract.CallFlags))]
[assembly: TypeForwardedTo(typeof(Neo.SmartContract.TriggerType))]
[assembly: TypeForwardedTo(typeof(Neo.SmartContract.Manifest.ContractManifest))]
// ... 所有提取的类型
```

### 4.2 命名空间保持

所有提取的类型保持原有命名空间 `Neo.SmartContract.*`，确保二进制兼容性。

---

## 5. 测试策略

### 5.1 现有测试

| 测试文件 | 覆盖范围 |
|----------|----------|
| `UT_ApplicationEngine.cs` | 执行引擎核心功能 |
| `UT_NativeContract.cs` | 原生合约基类 |
| `UT_NeoToken.cs` | NEO 代币合约 |
| `UT_GasToken.cs` | GAS 代币合约 |
| `UT_PolicyContract.cs` | 策略合约 |
| `UT_ContractManifest.cs` | 合约清单 |

### 5.2 迁移后测试

1. 所有现有测试必须通过
2. 添加新模块的单元测试
3. 添加跨模块集成测试
4. 验证 TypeForwarding 正确性

---

## 6. 风险评估

| 风险 | 概率 | 影响 | 缓解措施 |
|------|------|------|----------|
| VM 依赖无法完全解耦 | 高 | 中 | 接受 VM 依赖，专注于模块化 |
| Native Contract 循环依赖 | 中 | 高 | 按依赖顺序提取 |
| 测试覆盖不足 | 中 | 高 | 提取前增加测试 |
| TypeForwarding 破坏兼容性 | 低 | 高 | 全面测试转发类型 |
| 性能回归 | 低 | 中 | 基准测试验证 |

---

## 7. 时间线

| 阶段 | 时间 | 目标 | 交付物 |
|------|------|------|--------|
| Phase 1 | 第 1 周 | 基础类型提取 | Neo.SmartContract.Abstractions |
| Phase 2 | 第 2-3 周 | 核心类型提取 | 存储、合约状态类型 |
| Phase 3 | 第 4-5 周 | Native Contracts | 所有原生合约 |
| Phase 4 | 第 6-7 周 | ApplicationEngine | 执行引擎模块 |
| Phase 5 | 第 8 周 | 辅助类型 + 测试 | 完整模块 + 测试通过 |

---

## 8. 依赖图

```
Neo.Core
    ↑
Neo.SmartContract.Abstractions
    ↑
Neo.SmartContract.Manifest ──────────────┐
    ↑                                    │
Neo.SmartContract.Core ←── Neo.VM        │
    ↑                                    │
Neo.SmartContract.Native ←── Neo.Persistence
    ↑
Neo.SmartContract.Execution
    ↑
Neo (遗留模块，TypeForwarding)
```

---

## 9. 下一步行动

1. **立即:** 创建 `IContractStateData` 接口到 Neo.Core
2. **本周:** 开始 Phase 1 基础类型提取
3. **持续:** 更新架构文档，跟踪进度

---

## 10. 结论

SmartContract 模块提取是 NeoAN 重构中最复杂的任务。由于 `ApplicationEngine` 继承自 `Neo.VM.ExecutionEngine`，**完全解耦 VM 依赖是不可能的**。

**推荐策略:**
1. 接受 VM 依赖作为核心依赖
2. 专注于模块化和接口抽象
3. 使用 TypeForwarding 保持兼容性
4. 按层次逐步提取，确保每步测试通过

这种方法可以在保持功能完整性的同时，实现代码的模块化和可维护性提升。
