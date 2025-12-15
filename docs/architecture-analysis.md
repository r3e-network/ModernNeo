# Neo 架构分析与重构路线图

## 当前状态分析

### 已完成的模块化工作

| 模块              | 状态      | 文件数 | 描述                                                            |
| ----------------- | --------- | ------ | --------------------------------------------------------------- |
| Neo.Core          | ✅ 完成   | 10     | 基础类型 + 接口抽象 (IVerifiableBase, IInteroperableBase, etc.) |
| Neo.Cryptography  | ✅ 完成   | 16     | 加密算法 (ECC, Hasher, MerkleTree, Base58)                      |
| Neo.Storage       | ✅ 完成   | 11     | 存储抽象 (IStore, MemoryStore, StoreFactory)                    |
| Neo.Observability | ✅ 完成   | 11     | 可观测性 (Metrics, Logging interfaces)                          |
| Neo.IO            | ✅ 已存在 | -      | 序列化接口                                                      |
| Neo.Json          | ✅ 已存在 | -      | JSON 处理                                                       |
| Neo.Extensions    | ✅ 已存在 | -      | 扩展方法                                                        |
| Neo.Node          | ✅ 增强   | 1      | 最小可运行节点 (Health, Metrics, Logging)                       |

### 待重构的核心模块 (src/Neo/)

| 目录            | 文件数 | 循环依赖                             | 重构难度 |
| --------------- | ------ | ------------------------------------ | -------- |
| Network/        | 61     | SmartContract, Ledger, VM            | 🔴 高    |
| SmartContract/  | 81     | Network, Ledger, VM, Persistence     | 🔴 高    |
| Ledger/         | 12     | Network, SmartContract               | 🔴 高    |
| Wallets/        | 13     | Network, SmartContract, Cryptography | 🟡 中    |
| Extensions/     | 17     | 多模块依赖                           | 🟡 中    |
| Builders/       | 8      | Network, Cryptography                | 🟡 中    |
| IEventHandlers/ | 10     | 多模块依赖                           | 🟡 中    |
| IO/             | 4      | 少量依赖                             | 🟢 低    |
| Plugins/        | 3      | 核心模块依赖                         | 🟡 中    |
| Sign/           | 3      | Network, Cryptography                | 🟡 中    |

## 循环依赖分析

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

### 问题根源

1. **IVerifiable.GetScriptHashesForVerifying(DataCache)** - 验证逻辑嵌入接口
2. **IInteroperable.ToStackItem(IReferenceCounter)** - VM 类型嵌入接口
3. **Transaction/Block** 同时实现两个接口，导致 Network 依赖 SmartContract 和 VM

## 推荐重构策略

### Phase 3.1: 接口分离 ✅ 已完成

1. **创建 IVerifiableBase** (无 DataCache 依赖) ✅
    - `src/Neo.Core/Interfaces/IVerifiableBase.cs`
    - 只包含序列化方法 (Hash, Witnesses, DeserializeUnsigned, SerializeUnsigned)
    - 验证逻辑移到 IVerificationService

2. **创建 IInteroperableBase** (无 VM 依赖) ✅
    - `src/Neo.Core/Interfaces/IInteroperableBase.cs`
    - 只包含基础转换方法 (Clone, FromReplica)
    - VM 转换移到 IStackItemConverter

3. **创建服务接口** ✅
    - `src/Neo.Core/Interfaces/IVerificationService.cs`: 处理验证逻辑
    - `src/Neo.Core/Interfaces/IStackItemConverter.cs`: 处理 VM 转换
    - `src/Neo.Core/Interfaces/IWitness.cs`: Witness 抽象接口

### Phase 3.2: 协议层提取 (预计 3-4 周)

1. **创建 Neo.Protocol 模块**
    - 包含 Transaction, Block, Witness 等核心类型
    - 只依赖 Neo.Core, Neo.IO, Neo.Cryptography

2. **更新依赖关系**
    - Neo.Network 依赖 Neo.Protocol
    - Neo.SmartContract 依赖 Neo.Protocol
    - Neo.Ledger 依赖 Neo.Protocol

### Phase 3.3: 完整模块化 (预计 4-6 周)

1. **Neo.Network** - P2P 网络层
2. **Neo.Execution** - 智能合约执行
3. **Neo.Ledger** - 账本管理
4. **Neo.Wallet** - 钱包服务

## 当前建议

鉴于循环依赖的复杂性，建议：

1. **保持当前架构稳定** - 已完成的模块化工作已提供良好基础
2. **渐进式改进** - 通过接口抽象逐步解耦
3. **优先保证兼容性** - 所有 1168 个测试必须通过
4. **文档先行** - 在大规模重构前完善架构文档

## 已验证的功能

- ✅ 构建成功 (0 errors, 0 warnings)
- ✅ 1175 个测试全部通过 (Neo.Json: 92, Neo.Extensions: 89, Neo.UnitTests: 994)
- ✅ Neo.Node 可启动并连接网络
- ✅ Health/Metrics/Logging 端点正常工作
- ✅ TypeForwarding 保持二进制兼容
- ✅ Phase 3.1 接口分离完成 (2025-12-15)
- ✅ Phase 3.3 协议层提取第二阶段完成 (2025-12-15)

## Neo.Core/Interfaces 目录结构

```
src/Neo.Core/Interfaces/
├── IInteroperableBase.cs    # VM 无关的互操作基础接口
├── IStackItemConverter.cs   # VM 转换服务接口
├── IVerifiableBase.cs       # 持久化无关的验证基础接口
├── IVerificationService.cs  # 验证服务接口
└── IWitness.cs              # 见证人抽象接口
```

## Phase 3.2 接口继承 ✅ 已完成 (2025-12-15)

已完成的工作：

1. **IVerifiable 继承 IVerifiableBase** ✅
    - `src/Neo/Network/P2P/Payloads/IVerifiable.cs` 现在继承 `IVerifiableBase`
    - 保持向后兼容，所有实现类无需修改

2. **IInteroperable 继承 IInteroperableBase** ✅
    - `src/Neo/SmartContract/IInteroperable.cs` 现在继承 `IInteroperableBase`
    - IInteroperableBase 作为标记接口，不影响现有实现

3. **Witness 实现 IWitness** ✅
    - `src/Neo/Network/P2P/Payloads/Witness.cs` 现在实现 `IWitness`
    - 字段改为属性以满足接口要求

4. **测试验证** ✅
    - 修复了 TestVerifiable 和 ManualWitness 测试类
    - 全部 1168 个测试通过

## Phase 3.3 协议层提取 - 第二阶段完成 (2025-12-15)

### 已完成的迁移 (18 个类型)

#### P2P 协议类型 (7 个)

| 类型               | 源命名空间                   | 类别    | 状态 |
| ------------------ | ---------------------------- | ------- | ---- |
| WitnessRuleAction  | Neo.Network.P2P.Payloads     | 枚举    | ✅   |
| WitnessScope       | Neo.Network.P2P.Payloads     | 枚举    | ✅   |
| OracleResponseCode | Neo.Network.P2P.Payloads     | 枚举    | ✅   |
| FilterAddPayload   | Neo.Network.P2P.Payloads     | Payload | ✅   |
| FilterLoadPayload  | Neo.Network.P2P.Payloads     | Payload | ✅   |
| GetBlocksPayload   | Neo.Network.P2P.Payloads     | Payload | ✅   |
| NodeCapabilityType | Neo.Network.P2P.Capabilities | 枚举    | ✅   |
| MessageFlags       | Neo.Network.P2P              | 枚举    | ✅   |

#### SmartContract 类型 (5 个)

| 类型                  | 源命名空间               | 类别 | 状态 |
| --------------------- | ------------------------ | ---- | ---- |
| ContractParameterType | Neo.SmartContract        | 枚举 | ✅   |
| CallFlags             | Neo.SmartContract        | 枚举 | ✅   |
| FindOptions           | Neo.SmartContract        | 枚举 | ✅   |
| NamedCurveHash        | Neo.SmartContract.Native | 枚举 | ✅   |
| Role                  | Neo.SmartContract.Native | 枚举 | ✅   |

#### 其他类型 (6 个)

| 类型                     | 源命名空间        | 类别 | 状态 |
| ------------------------ | ----------------- | ---- | ---- |
| TransactionRemovalReason | Neo.Ledger        | 枚举 | ✅   |
| VerifyResult             | Neo.Ledger        | 枚举 | ✅   |
| UnhandledExceptionPolicy | Neo.Plugins       | 枚举 | ✅   |
| IPluginSettings          | Neo.Plugins       | 接口 | ✅   |
| TriggerType              | Neo.SmartContract | 枚举 | ✅   |

### Neo.IO 基础设施扩展

为支持 Payload 类型迁移，以下扩展方法已迁移到 Neo.IO：

| 类型                   | 方法                                       | 状态 |
| ---------------------- | ------------------------------------------ | ---- |
| BinaryWriterExtensions | WriteVarBytes, WriteVarInt, WriteVarString | ✅   |
| MemoryExtensions       | GetVarSize(ReadOnlyMemory<byte>)           | ✅   |
| MemoryReaderExtensions | ReadSerializable<T>, ReadSerializableArray | ✅   |

### Neo.Protocol 模块结构

```
src/Neo.Protocol/
├── Neo.Protocol.csproj
├── CallFlags.cs
├── ContractParameterType.cs
├── FilterAddPayload.cs
├── FilterLoadPayload.cs
├── FindOptions.cs
├── GetBlocksPayload.cs
├── IPluginSettings.cs
├── MessageFlags.cs
├── NamedCurveHash.cs
├── NodeCapabilityType.cs
├── OracleResponseCode.cs
├── Role.cs
├── TransactionRemovalReason.cs
├── TriggerType.cs
├── UnhandledExceptionPolicy.cs
├── VerifyResult.cs
├── WitnessRuleAction.cs
└── WitnessScope.cs
```

### 迁移统计

- **枚举**: 14 个
- **Payload 类**: 3 个
- **接口**: 1 个
- **总计**: 18 个类型

### 阻塞的类型 (循环依赖)

以下类型因循环依赖暂无法迁移：

| 类型                     | 阻塞原因                                      |
| ------------------------ | --------------------------------------------- |
| MessageCommand           | 依赖多个 Payload 类型的 ReflectionCache       |
| InventoryType            | 依赖 MessageCommand 枚举值                    |
| TransactionAttributeType | 依赖 ReflectionCache + 多个属性类             |
| WitnessConditionType     | 依赖 ReflectionCache + 多个条件类             |
| Header/Block/Transaction | 核心协议类型，依赖链复杂                      |
| 其他 Payload 类          | 依赖 Header, Block, NetworkAddressWithTime 等 |

### TypeForwards 配置

所有迁移的类型都在 `src/Neo/TypeForwards.cs` 中配置了类型转发，确保二进制兼容性。

## 服务接口实现 ✅ 已完成 (2025-12-15)

### 已实现的服务

| 服务                | 位置              | 描述                           |
| ------------------- | ----------------- | ------------------------------ |
| VerificationService | src/Neo/Services/ | 实现 IVerificationService 接口 |
| StackItemConverter  | src/Neo/Services/ | 实现 IStackItemConverter 接口  |

### 单元测试

| 测试类                 | 测试数 | 状态 |
| ---------------------- | ------ | ---- |
| UT_VerificationService | 4      | ✅   |
| UT_StackItemConverter  | 3      | ✅   |

## 下一步工作

1. ~~**解决 ReflectionCache 依赖**~~ ✅ 已迁移到 Neo.IO
2. ~~**实现服务接口**~~ ✅ VerificationService 和 StackItemConverter 已实现
3. **迁移核心协议类型** - Transaction, Block, Header (需要整体迁移，依赖链复杂)
4. **考虑创建 Neo.Network 模块** - 分离 P2P 网络层
5. **解决 ReflectionCache 属性依赖** - MessageCommand, TransactionAttributeType, WitnessConditionType 需要先迁移关联的 Payload/Attribute/Condition 类
