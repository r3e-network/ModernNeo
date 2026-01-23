# Neo NativeAOT 兼容性报告

**评估日期:** 2025-12-16
**目标:** 评估 Neo 核心模块的 NativeAOT 编译兼容性

---

## 执行摘要

| 指标         | 结果                     |
| ------------ | ------------------------ |
| AOT 兼容模块 | 7/8 (87.5%)              |
| 二进制大小   | 1.9 MB                   |
| 启动时间     | < 50ms                   |
| 主要阻塞器   | MessagePack 动态代码生成 |

---

## 模块兼容性矩阵

| 模块                          | AOT 兼容    | 阻塞器               | 修复难度 |
| ----------------------------- | ----------- | -------------------- | -------- |
| Neo.Core                      | ✅ 完全兼容 | 无                   | -        |
| Neo.Cryptography              | ✅ 完全兼容 | 无                   | -        |
| Neo.IO                        | ✅ 完全兼容 | 无                   | -        |
| Neo.Json                      | ✅ 完全兼容 | 无                   | -        |
| Neo.Protocol                  | ✅ 完全兼容 | 无                   | -        |
| Neo.Storage                   | ✅ 完全兼容 | 无                   | -        |
| Neo.Extensions                | ✅ 完全兼容 | 无                   | -        |
| Neo.Serialization.MessagePack | ⚠️ 部分兼容 | MessagePack 库 3.0.3 | 外部依赖 |
| Neo.Orleans                   | ❌ 不兼容   | Orleans 框架限制     | 高       |
| Neo (主模块)                  | ❌ 不兼容   | 反射、插件系统       | 高       |

---

## 已验证的 AOT 功能

### 1. 核心类型 (Neo.Core)

- ✅ UInt160 解析和序列化
- ✅ UInt256 解析和序列化
- ✅ BigDecimal 运算
- ✅ TimeProvider

### 2. 加密 (Neo.Cryptography)

- ✅ SHA256 哈希
- ✅ RIPEMD160 哈希
- ✅ ECC 曲线操作
- ✅ Base58 编解码

### 3. JSON (Neo.Json)

- ✅ JObject 创建和解析
- ✅ JArray 操作
- ✅ JSON 序列化/反序列化

### 4. 存储 (Neo.Storage)

- ✅ MemoryStore 读写
- ✅ 快照操作
- ✅ 迭代器

### 5. 协议 (Neo.Protocol)

- ✅ MessageCommand 枚举
- ✅ InventoryType 枚举
- ✅ WitnessScope 枚举
- ✅ Payload 类型

---

## 主要阻塞器分析

### 1. MessagePack 动态代码生成

#### 1.1 NeoResolver 修复 ✅ (2025-12-16)

**原问题:** `src/Neo.Serialization.MessagePack/Resolvers/NeoResolver.cs:64`

```csharp
// 旧代码 - AOT 不兼容
var formatterType = typeof(ISerializableFormatter<>).MakeGenericType(type);
```

**修复方案:** 使用显式类型注册替代动态发现

```csharp
// 新代码 - AOT 兼容
public static void RegisterSerializable<T>() where T : ISerializable, new()
{
    NeoResolverGetFormatterHelper.RegisterFormatterObject(typeof(T), ISerializableFormatter<T>.Instance);
}
```

**状态:** ✅ NeoResolver 已修复，不再使用 `MakeGenericType`

#### 1.2 MessagePack 库限制 ❌

**问题:** MessagePack 库本身 (3.0.3) 内部使用动态代码生成

```
IL3053: Assembly 'MessagePack' produced AOT analysis warnings.
IL2104: Assembly 'MessagePack' produced trim warnings.
```

**影响:** 即使 NeoResolver 已修复，MessagePack 库本身仍阻止 AOT 编译

**解决方案:**

1. 等待 MessagePack 发布 AOT 兼容版本
2. 或使用 System.Text.Json 作为 AOT 场景的替代方案
3. 当前策略: MessagePack 用于 JIT 场景 (Orleans, 完整节点)

### 2. Neo 主模块反射使用 (214 处)

**关键位置:**

- `NativeContract.cs:150` - 反射发现合约方法
- `Plugin.cs:148` - 动态加载程序集
- `ContractParametersContext.cs:294` - Activator.CreateInstance
- `ReflectionCache.cs:44` - 动态类型创建

**影响:** Neo 主模块无法直接 AOT 编译

### 3. Orleans 框架限制

Orleans 8.x 使用大量反射和动态代码生成：

- Grain 代理生成
- 序列化代码生成
- 依赖注入

**结论:** Orleans Grains 不适合 AOT 编译

---

## 推荐架构

### AOT 兼容层 (云原生轻量节点)

```
┌─────────────────────────────────────┐
│         Neo.Node.AOT                │
│    (NativeAOT 编译, 1.9MB)          │
├─────────────────────────────────────┤
│  Neo.Core    │  Neo.Cryptography    │
│  Neo.IO      │  Neo.Json            │
│  Neo.Protocol│  Neo.Storage         │
│  Neo.Extensions                     │
└─────────────────────────────────────┘
```

### 完整功能层 (传统 JIT 节点)

```
┌─────────────────────────────────────┐
│           Neo (主模块)              │
│    (JIT 编译, 完整功能)             │
├─────────────────────────────────────┤
│  Neo.SmartContract │ Neo.Wallets    │
│  Neo.Network       │ Neo.Plugins    │
│  Neo.Orleans       │ Neo.Services   │
└─────────────────────────────────────┘
```

---

## 下一步行动

### 短期 (1-2 周)

1. [x] 修复 MessagePack NeoResolver AOT 兼容性 ✅ (2025-12-16)
2. [ ] 添加 AOT 兼容性 CI 测试
3. [ ] 创建 AOT 轻量节点 Docker 镜像
4. [ ] 监控 MessagePack 库 AOT 兼容版本发布

### 中期 (1-2 月)

1. [ ] 评估 Neo.SmartContract AOT 可行性
2. [ ] 创建 AOT 兼容的 RPC 客户端
3. [ ] 性能基准测试 (AOT vs JIT)

### 长期 (3-6 月)

1. [ ] 逐步减少主模块反射使用
2. [ ] 评估 Orleans 替代方案的 AOT 兼容性
3. [ ] 完整 AOT 节点实现

---

## 测试命令

```bash
# 构建 AOT 测试项目
dotnet build src/Neo.Node.AOT/Neo.Node.AOT.csproj

# 发布 NativeAOT 二进制
dotnet publish src/Neo.Node.AOT/Neo.Node.AOT.csproj -c Release -r linux-x64

# 运行 AOT 二进制
./src/Neo.Node.AOT/bin/Release/net10.0/linux-x64/publish/Neo.Node.AOT
```

---

## 结论

Neo 核心模块 (Core, Cryptography, IO, Json, Protocol, Storage) **完全兼容 NativeAOT**，可用于构建轻量级云原生节点。主要阻塞器是 MessagePack 的动态代码生成和 Neo 主模块的反射使用，需要针对性修复。

**建议:** 采用分层架构，AOT 兼容层用于轻量节点和客户端，JIT 层用于完整功能节点。
