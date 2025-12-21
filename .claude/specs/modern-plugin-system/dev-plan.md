# Sprint 38: 现代插件系统 - 开发计划

**版本:** 1.0
**日期:** 2025-12-21
**目标:** 完善现代插件系统，实现热加载、DI集成、ALC隔离，测试覆盖率≥90%

---

## 任务分解

### Task 1: T1-CORE - 核心插件管理器增强
**描述:** 扩展 PluginManager 生产级功能
**文件范围:**
- `src/Neo.Plugins/PluginManager.cs`
- `src/Neo.Plugins/PluginMetadata.cs` (新建)
- `src/Neo.Plugins/PluginDependencyGraph.cs` (新建)

**功能:**
- 插件元数据发现 (名称、版本、依赖)
- 插件依赖图验证
- 插件兼容性检查
- 插件启用/禁用 (无需卸载)
- 插件配置重载支持

**测试命令:** `dotnet test tests/Neo.UnitTests --filter "FullyQualifiedName~Plugins"`
**依赖:** 无
**预估:** 2天

---

### Task 2: T2-TESTS - 综合测试套件
**描述:** 实现 90%+ 测试覆盖率
**文件范围:**
- `tests/Neo.UnitTests/Plugins/UT_PluginManager.cs`
- `tests/Neo.UnitTests/Plugins/UT_PluginLifecycle.cs` (新建)
- `tests/Neo.UnitTests/Plugins/UT_HotReload.cs` (新建)
- `tests/Neo.UnitTests/Plugins/UT_PluginIsolation.cs` (新建)
- `tests/Neo.UnitTests/Plugins/TestPlugins/` (新建)

**测试场景:**
- 热重载场景 (文件变更/删除/快速变更)
- AssemblyLoadContext 卸载验证
- DI 解析失败处理
- 并发加载/卸载压力测试
- 插件异常处理 (所有异常策略)
- 事件触发验证
- 内存泄漏检测 (GC 压力测试)
- 插件生命周期边界情况

**测试命令:** `dotnet test tests/Neo.UnitTests --filter "FullyQualifiedName~Plugins" --collect:"XPlat Code Coverage"`
**依赖:** T1-CORE (部分)
**预估:** 2.5天

---

### Task 3: T3-DISCOVERY - 插件发现与元数据系统
**描述:** 自动插件检测和编目
**文件范围:**
- `src/Neo.Plugins/IPluginMetadata.cs` (新建)
- `src/Neo.Plugins/PluginAttribute.cs` (新建)
- `src/Neo.Plugins/PluginRegistry.cs` (新建)
- `src/Neo.Plugins/PluginVersionResolver.cs` (新建)

**功能:**
- IPluginMetadata 接口
- 基于属性的插件发现
- 插件注册表/目录
- 插件搜索/过滤功能
- 插件版本解析

**测试命令:** `dotnet test tests/Neo.UnitTests --filter "FullyQualifiedName~PluginDiscovery"`
**依赖:** T1-CORE (部分)
**预估:** 1.5天

---

### Task 4: T4-DI - DI集成与服务隔离
**描述:** 高级 DI 模式支持
**文件范围:**
- `src/Neo.Plugins/PluginServiceScope.cs` (新建)
- `src/Neo.Plugins/IPluginServiceRegistrar.cs` (新建)
- `src/Neo.Plugins/PluginConfigurationProvider.cs` (新建)

**功能:**
- 每插件 IServiceProvider 作用域
- 插件服务注册钩子
- 插件特定配置注入
- 支持插件工厂模式
- 服务生命周期管理

**测试命令:** `dotnet test tests/Neo.UnitTests --filter "FullyQualifiedName~PluginDI"`
**依赖:** T1-CORE (部分)
**预估:** 1.5天

---

## 依赖关系图

```
T1-CORE ─────┬──────────────────────────────────────┐
             │                                       │
             ├──→ T2-TESTS (部分依赖)                │
             │                                       │
             ├──→ T3-DISCOVERY (部分依赖)            │
             │                                       │
             └──→ T4-DI (部分依赖)                   │
                                                     ↓
                                              最终集成验证
```

## 并行执行策略

**Phase 1 (并行):**
- T1-CORE: 核心功能增强
- T3-DISCOVERY: 元数据系统 (可独立开发接口)

**Phase 2 (并行):**
- T2-TESTS: 综合测试 (依赖 T1 完成)
- T4-DI: DI 集成 (依赖 T1 完成)

---

## 验收标准

| 指标 | 目标 |
|------|------|
| 核心组件覆盖率 | ≥90% |
| 热加载功能 | 完整实现 |
| ALC 隔离 | 验证通过 |
| DI 集成 | MS DI 完整支持 |
| 所有测试 | 通过 |

---

## 关键文件参考

**现有实现:**
- `/home/neo/git/ModernNeo/src/Neo.Plugins/IPlugin.cs`
- `/home/neo/git/ModernNeo/src/Neo.Plugins/PluginManager.cs`
- `/home/neo/git/ModernNeo/tests/Neo.UnitTests/Plugins/UT_PluginManager.cs`

**集成点:**
- `/home/neo/git/ModernNeo/src/Neo/NeoSystem.cs`
- `/home/neo/git/ModernNeo/src/Neo.Node/Program.cs`
