# Sprint 9: NativeAOT 准备 - 开发计划

**版本:** 1.0
**日期:** 2025-12-21
**Sprint:** 9 (第17-18周)
**故事点:** 18

---

## 1. 目标

准备代码库支持 NativeAOT 编译，消除反射依赖，修复 Trim 警告。

---

## 2. 用户故事

| ID | 用户故事 | 故事点 | 优先级 |
|----|----------|--------|--------|
| US-9.1 | NativeAOT 兼容性审计 | 5 | P0 |
| US-9.2 | 反射消除 | 8 | P0 |
| US-9.3 | Trim 警告修复 | 5 | P1 |

---

## 3. 任务分解

### Task 1: Neo.IO 反射消除 (US-9.2)
**ID:** T9-1
**文件范围:**
- `src/Neo.IO/Caching/ReflectionCache.cs`
- `src/Neo.IO/Caching/ReflectionCacheAttribute.cs`
- `src/Neo.IO/MemoryExtensions.cs`

**工作内容:**
1. 将 `ReflectionCache<T>` 改为编译时类型注册
2. 移除 `Type.GetType(TypeName)` 动态类型解析
3. 为 `MemoryExtensions.AsSerializable<T>()` 添加 `new()` 约束
4. 添加 `[DynamicallyAccessedMembers]` 注解

**测试命令:**
```bash
dotnet test tests/Neo.UnitTests --filter "FullyQualifiedName~IO" --collect:"XPlat Code Coverage"
```

**依赖:** 无
**估计:** 3 故事点

---

### Task 2: Neo.Plugins AOT 注解 (US-9.2, US-9.3)
**ID:** T9-2
**文件范围:**
- `src/Neo.Plugins/PluginManager.cs`
- `src/Neo.Plugins/IPluginFactory.cs`
- `src/Neo/Plugins/Plugin.cs`

**工作内容:**
1. 为插件加载方法添加 `[RequiresUnreferencedCode]` 注解
2. 为反射调用添加 `[DynamicallyAccessedMembers]` 注解
3. 创建 AOT 兼容的插件工厂接口
4. 添加编译时插件注册机制

**测试命令:**
```bash
dotnet test tests/Neo.UnitTests --filter "FullyQualifiedName~Plugins" --collect:"XPlat Code Coverage"
```

**依赖:** 无
**估计:** 5 故事点

---

### Task 3: SmartContract 反射优化 (US-9.2)
**ID:** T9-3
**文件范围:**
- `src/Neo/SmartContract/ApplicationEngine.cs`
- `src/Neo/SmartContract/Native/NativeContract.cs`
- `src/Neo/SmartContract/ContractParametersContext.cs`

**工作内容:**
1. 为 `ApplicationEngine` 的 `GetMethod/GetProperty` 添加 `[DynamicDependency]`
2. 将 `NativeContract` 构造函数反射改为静态初始化
3. 为 `ContractParametersContext` 添加类型注解

**测试命令:**
```bash
dotnet test tests/Neo.UnitTests --filter "FullyQualifiedName~SmartContract" --collect:"XPlat Code Coverage"
```

**依赖:** 无
**估计:** 5 故事点

---

### Task 4: Trim 警告修复 (US-9.3)
**ID:** T9-4
**文件范围:**
- `src/Neo.Extensions/Exceptions/TryCatchExtensions.cs`
- `src/Neo.SmartContract.Core/IInteroperable.cs`
- 所有项目的 `.csproj` 文件

**工作内容:**
1. 为 `TryCatchExtensions` 的异常创建添加类型约束
2. 为 `IInteroperable.Clone()` 添加 AOT 注解
3. 在所有项目中启用 `<EnableTrimAnalyzer>true</EnableTrimAnalyzer>`
4. 修复所有 IL2xxx 警告

**测试命令:**
```bash
dotnet build src/Neo.Node.AOT -c Release -p:PublishAot=true 2>&1 | grep -E "IL2|warning"
```

**依赖:** T9-1, T9-2, T9-3
**估计:** 5 故事点

---

## 4. 验收标准

- [ ] 所有 `Activator.CreateInstance` 调用都有 AOT 注解或替代方案
- [ ] 所有 `Type.GetType` 调用被移除或有编译时替代
- [ ] 所有 `GetMethod/GetProperty` 调用有 `[DynamicDependency]` 注解
- [ ] `dotnet build -p:PublishAot=true` 无 IL2xxx 警告
- [ ] 单元测试覆盖率 ≥90%
- [ ] Neo.Node.AOT 可成功编译为原生二进制

---

## 5. 任务依赖图

```
T9-1 (Neo.IO) ─────────────────┐
                               │
T9-2 (Neo.Plugins) ────────────┼──→ T9-4 (Trim 警告)
                               │
T9-3 (SmartContract) ──────────┘
```

**并行执行:** T9-1, T9-2, T9-3 可并行
**串行执行:** T9-4 依赖前三个任务

---

## 6. 风险与缓解

| 风险 | 影响 | 缓解措施 |
|------|------|----------|
| 插件系统需要动态加载 | 高 | 保留反射但添加 AOT 注解，提供静态注册替代 |
| 第三方库不支持 AOT | 中 | 使用条件编译，AOT 模式禁用不兼容功能 |
| 测试覆盖率不足 | 中 | 为每个修改添加专门的 AOT 兼容性测试 |

---

## 7. 完成定义 (DoD)

- [x] 代码完成并通过评审
- [ ] 单元测试 (≥90% 覆盖率)
- [ ] 集成测试通过
- [ ] NativeAOT 编译成功
- [ ] 无 IL2xxx Trim 警告
- [ ] 文档更新
