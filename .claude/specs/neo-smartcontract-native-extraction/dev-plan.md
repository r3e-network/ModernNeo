# Neo.SmartContract.Native 模块提取 - 开发计划

## 概述
将 Neo.SmartContract 中的 Native 合约实现提取到独立的 Neo.SmartContract.Native 项目中，以实现更清晰的模块边界和依赖管理。保持 NativeContract 基类在 Neo.SmartContract 中，因其与 ApplicationEngine 的紧密耦合。

## 架构约束分析

### 核心依赖关系
1. **ApplicationEngine ↔ NativeContract 循环依赖**:
   - ApplicationEngine.System_Contract_CallNative (InteropDescriptor) 被 NativeContract.GetAllowedMethods() 使用
   - NativeContract.Invoke() 需要 ApplicationEngine 执行合约方法
   - **决策**: 保持 NativeContract 基类在 Neo.SmartContract，仅提取具体实现

2. **Native 合约静态单例**:
   - NativeContract.NEO, GAS, Policy 等 11 个静态属性
   - 被整个代码库广泛引用 (42+ 文件)
   - **决策**: 使用 TypeForward 保持向后兼容

3. **ApplicationEngine 依赖强度**:
   - 13 个 Native 文件中有 115 处引用 ApplicationEngine
   - 主要在具体合约实现的业务逻辑中
   - **决策**: Neo.SmartContract.Native 项目引用 Neo.SmartContract

### 待提取文件清单 (24 个文件)

#### 第一层: 数据状态类 (无 ApplicationEngine 依赖)
1. AccountState.cs - 账户状态基类
2. TransactionState.cs - 交易状态
3. HashIndexState.cs - 哈希索引状态
4. TrimmedBlock.cs - 精简区块表示
5. OracleRequest.cs - Oracle 请求数据
6. InteroperableList.cs - 可互操作列表

#### 第二层: 元数据和特性类
7. ContractMethodMetadata.cs - 方法元数据
8. ContractMethodAttribute.cs - 方法特性
9. ContractEventAttribute.cs - 事件特性
10. IHardforkActivable.cs - 硬分叉接口

#### 第三层: 工具合约 (最小 ApplicationEngine 依赖)
11. CryptoLib.cs - 密码学库
12. CryptoLib.BLS12_381.cs - BLS12_381 实现
13. StdLib.cs - 标准库

#### 第四层: 代币合约
14. FungibleToken.cs - 可替代代币基类
15. NeoToken.cs - NEO 代币
16. GasToken.cs - GAS 代币

#### 第五层: 系统合约
17. ContractManagement.cs - 合约管理
18. LedgerContract.cs - 账本访问
19. PolicyContract.cs - 策略设置

#### 第六层: 服务合约
20. OracleContract.cs - Oracle 服务
21. Notary.cs - 公证服务
22. Treasury.cs - 国库管理
23. RoleManagement.cs - 角色管理

#### 保留在 Neo.SmartContract 中
24. NativeContract.cs - 基类 (因 ApplicationEngine 耦合)

## 任务分解

### 任务 1: 创建 Neo.SmartContract.Native 项目
- **ID**: task-1
- **描述**: 创建新项目文件，配置依赖关系和项目引用
- **文件范围**:
  - `src/Neo.SmartContract.Native/Neo.SmartContract.Native.csproj` (新建)
  - `neo.sln` (修改，添加项目引用)
- **依赖**: 无
- **测试命令**: `dotnet build src/Neo.SmartContract.Native/Neo.SmartContract.Native.csproj`
- **测试重点**:
  - 项目成功编译
  - 依赖关系正确: Neo.SmartContract, Neo.Core, Neo.VM, Neo.Cryptography
  - TargetFramework 为 net10.0
  - InternalsVisibleTo 配置: Neo, Neo.UnitTests

### 任务 2: 提取数据状态类和元数据类 (第一层 + 第二层)
- **ID**: task-2
- **描述**: 移动 10 个无 ApplicationEngine 依赖的数据类到 Neo.SmartContract.Native
- **文件范围**:
  - `src/Neo.SmartContract.Native/AccountState.cs` (移动)
  - `src/Neo.SmartContract.Native/TransactionState.cs` (移动)
  - `src/Neo.SmartContract.Native/HashIndexState.cs` (移动)
  - `src/Neo.SmartContract.Native/TrimmedBlock.cs` (移动)
  - `src/Neo.SmartContract.Native/OracleRequest.cs` (移动)
  - `src/Neo.SmartContract.Native/InteroperableList.cs` (移动)
  - `src/Neo.SmartContract.Native/ContractMethodMetadata.cs` (移动)
  - `src/Neo.SmartContract.Native/ContractMethodAttribute.cs` (移动)
  - `src/Neo.SmartContract.Native/ContractEventAttribute.cs` (移动)
  - `src/Neo.SmartContract.Native/IHardforkActivable.cs` (移动)
- **依赖**: task-1
- **测试命令**: `dotnet test tests/Neo.UnitTests/SmartContract/Native/UT_ContractEventAttribute.cs --no-build && dotnet test tests/Neo.UnitTests/SmartContract/Native/UT_ContractMethodAttribute.cs --no-build`
- **测试重点**:
  - 所有类编译通过，命名空间为 Neo.SmartContract.Native
  - IInteroperable 接口正常工作
  - 特性反射功能正常
  - 硬分叉激活逻辑正确

### 任务 3: 提取工具合约 (第三层)
- **ID**: task-3
- **描述**: 移动 CryptoLib 和 StdLib 到 Neo.SmartContract.Native
- **文件范围**:
  - `src/Neo.SmartContract.Native/CryptoLib.cs` (移动)
  - `src/Neo.SmartContract.Native/CryptoLib.BLS12_381.cs` (移动)
  - `src/Neo.SmartContract.Native/StdLib.cs` (移动)
  - `tests/Neo.UnitTests/SmartContract/Native/UT_CryptoLib.cs` (更新引用)
  - `tests/Neo.UnitTests/SmartContract/Native/UT_StdLib.cs` (更新引用)
- **依赖**: task-2
- **测试命令**: `dotnet test tests/Neo.UnitTests/SmartContract/Native/UT_CryptoLib.cs --no-build && dotnet test tests/Neo.UnitTests/SmartContract/Native/UT_StdLib.cs --no-build`
- **测试重点**:
  - BLS12_381 密码学操作正确
  - secp256k1 签名恢复功能正常
  - Base58/Base64 编解码正确
  - JSON 序列化/反序列化功能正常
  - 所有加密哈希算法测试通过

### 任务 4: 提取代币合约 (第四层)
- **ID**: task-4
- **描述**: 移动 FungibleToken 基类及 NEO/GAS 代币实现
- **文件范围**:
  - `src/Neo.SmartContract.Native/FungibleToken.cs` (移动)
  - `src/Neo.SmartContract.Native/NeoToken.cs` (移动)
  - `src/Neo.SmartContract.Native/GasToken.cs` (移动)
  - `tests/Neo.UnitTests/SmartContract/Native/UT_FungibleToken.cs` (更新引用)
  - `tests/Neo.UnitTests/SmartContract/Native/UT_NeoToken.cs` (更新引用)
  - `tests/Neo.UnitTests/SmartContract/Native/UT_GasToken.cs` (更新引用)
- **依赖**: task-3
- **测试命令**: `dotnet test tests/Neo.UnitTests/SmartContract/Native/UT_FungibleToken.cs tests/Neo.UnitTests/SmartContract/Native/UT_NeoToken.cs tests/Neo.UnitTests/SmartContract/Native/UT_GasToken.cs --no-build`
- **测试重点**:
  - NEP-17 标准兼容性
  - Transfer 事件触发正确
  - Mint/Burn 逻辑正确
  - NEO 投票功能 (registerCandidate, vote, getCommittee)
  - GAS 分发机制
  - 账户余额查询准确性

### 任务 5: 提取系统合约 (第五层)
- **ID**: task-5
- **描述**: 移动 ContractManagement, LedgerContract, PolicyContract
- **文件范围**:
  - `src/Neo.SmartContract.Native/ContractManagement.cs` (移动)
  - `src/Neo.SmartContract.Native/LedgerContract.cs` (移动)
  - `src/Neo.SmartContract.Native/PolicyContract.cs` (移动)
  - `tests/Neo.UnitTests/SmartContract/Native/UT_NativeContract.cs` (更新引用)
  - `tests/Neo.UnitTests/SmartContract/Native/UT_PolicyContract.cs` (更新引用)
- **依赖**: task-4
- **测试命令**: `dotnet test tests/Neo.UnitTests/SmartContract/Native/UT_NativeContract.cs tests/Neo.UnitTests/SmartContract/Native/UT_PolicyContract.cs --no-build --verbosity normal`
- **测试重点**:
  - ContractManagement: deploy, update, destroy 功能
  - LedgerContract: getBlock, getTransaction, currentIndex 查询
  - PolicyContract: 费率设置、账户封锁、白名单管理
  - 委员会签名验证
  - 所有 Native 合约状态序列化/反序列化测试通过

### 任务 6: 提取服务合约 (第六层)
- **ID**: task-6
- **描述**: 移动 OracleContract, Notary, Treasury, RoleManagement
- **文件范围**:
  - `src/Neo.SmartContract.Native/OracleContract.cs` (移动)
  - `src/Neo.SmartContract.Native/Notary.cs` (移动)
  - `src/Neo.SmartContract.Native/Treasury.cs` (移动)
  - `src/Neo.SmartContract.Native/RoleManagement.cs` (移动)
  - `tests/Neo.UnitTests/SmartContract/Native/UT_Notary.cs` (更新引用)
  - `tests/Neo.UnitTests/SmartContract/Native/UT_RoleManagement.cs` (更新引用)
- **依赖**: task-5
- **测试命令**: `dotnet test tests/Neo.UnitTests/SmartContract/Native/UT_Notary.cs tests/Neo.UnitTests/SmartContract/Native/UT_RoleManagement.cs --no-build --verbosity normal`
- **测试重点**:
  - Oracle: 请求创建、响应处理、费用计算
  - Notary: P2PSigExtensions 支持、多签协助
  - Treasury: GAS 分配逻辑
  - RoleManagement: 角色指定 (Oracle, StateValidator, etc.)
  - 硬分叉激活逻辑 (Notary 在 HF_Cockatrice 激活)

### 任务 7: 添加 TypeForward 保持向后兼容
- **ID**: task-7
- **描述**: 在 Neo.SmartContract 中添加类型转发，保持静态单例和公共 API 可访问性
- **文件范围**:
  - `src/Neo.SmartContract/TypeForwards.cs` (修改/新建)
  - `src/Neo.SmartContract/Neo.SmartContract.csproj` (添加 ProjectReference)
- **依赖**: task-6
- **测试命令**: `dotnet build src/Neo.SmartContract/Neo.SmartContract.csproj && dotnet test tests/Neo.UnitTests --filter "FullyQualifiedName~Native" --no-build`
- **测试重点**:
  - `NativeContract.NEO` 静态属性可访问
  - `NativeContract.GAS` 静态属性可访问
  - `NativeContract.Policy` 等所有静态属性可访问
  - 现有代码无需修改 using 语句
  - 所有 42+ 个依赖文件编译通过

### 任务 8: 更新依赖项目和集成测试
- **ID**: task-8
- **描述**: 更新所有依赖 Native 合约的项目，运行完整集成测试
- **文件范围**:
  - `src/Neo.Node.Core/Neo.Node.Core.csproj` (添加 ProjectReference)
  - `src/Neo.Wallets/Neo.Wallets.csproj` (可能需要添加)
  - `src/Neo.Node/Rpc/*.cs` (42 个 RPC 方法文件，验证编译)
  - `tests/Neo.UnitTests/**/*.cs` (运行完整测试套件)
- **依赖**: task-7
- **测试命令**: `dotnet build neo.sln && dotnet test tests/Neo.UnitTests/Neo.UnitTests.csproj --verbosity normal --collect:"XPlat Code Coverage" -- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=opencover`
- **测试重点**:
  - 所有 RPC 方法正常工作 (GetNativeContractsRpcMethod, etc.)
  - Neo.Node.Core.Blockchain 集成正确
  - Neo.Wallets 中的 NativeContract 引用正常
  - TestBlockchain.GetTestSnapshotCache() 正常初始化
  - 所有 Native 合约单元测试通过
  - 代码覆盖率 ≥90%

## 验收标准
- [ ] Neo.SmartContract.Native 项目成功创建并编译
- [ ] 所有 23 个 Native 合约实现文件成功移动
- [ ] NativeContract 基类保留在 Neo.SmartContract 中
- [ ] TypeForward 配置正确，静态单例可访问
- [ ] 所有 Native 合约单元测试通过 (11 个测试文件)
- [ ] 42+ 个依赖文件编译通过，无 breaking changes
- [ ] 代码覆盖率 ≥90%
- [ ] 集成测试通过，RPC 方法正常工作
- [ ] 文档更新: 项目结构、依赖关系、迁移指南

## 技术决策说明

### 为何保留 NativeContract 基类在 Neo.SmartContract
1. **ApplicationEngine.System_Contract_CallNative 依赖**: NativeContract.GetAllowedMethods() 中使用 `sb.EmitSysCall(ApplicationEngine.System_Contract_CallNative)`，需要访问 InteropDescriptor
2. **Invoke 方法复杂度**: NativeContract.Invoke() 是一个 432 行的异步方法，深度集成 ApplicationEngine 的执行上下文、费用计算、参数转换
3. **最小化循环依赖**: 如果移动基类，需要 Neo.SmartContract.Native ← Neo.SmartContract 的循环依赖
4. **向后兼容**: 静态单例 (NEO, GAS, Policy) 通过 TypeForward 可以无缝保持在 `Neo.SmartContract.Native` 命名空间

### 依赖注入策略
- Neo.SmartContract.Native 直接引用 Neo.SmartContract (单向依赖)
- 具体合约实现继承 NativeContract 基类
- 通过 partial class 或 extension methods 可能进一步解耦 (后续优化)

### 测试策略
1. **单元测试**: 每层迁移后立即运行对应测试，快速失败
2. **集成测试**: 最后阶段运行完整测试套件，验证端到端功能
3. **覆盖率要求**: 所有 Native 合约必须达到 ≥90% 覆盖率
4. **回归测试**: 确保 42+ 个依赖文件的功能不受影响

### 风险缓解
- **风险 1: 循环依赖** → 保持 NativeContract 基类在 Neo.SmartContract
- **风险 2: 破坏性变更** → 使用 TypeForward 保持 API 兼容
- **风险 3: 测试失败** → 分层迁移，每层独立验证
- **风险 4: 性能回归** → 对比迁移前后的基准测试 (如有必要)

## 下一步
完成此提取后，可以进一步优化:
1. 将 NativeContract 基类拆分为 interface + implementation
2. 引入 INativeContractRegistry 接口解耦静态单例
3. 使用 DI 容器注入 Native 合约实例 (替代静态属性)
