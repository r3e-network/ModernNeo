# Development Plan: Orleans Integration into NeoSystem

## Overview
Integrate Orleans runtime into NeoSystem using the Strangler Pattern for gradual Akka→Orleans migration.

## Analysis Summary
- **Current State**: NeoSystem tightly coupled to Akka.NET with 4 IActorRef properties
- **Target State**: Runtime-switchable between Akka and Orleans
- **Strategy**: Inject `IActorBridge` into NeoSystem constructor

## Task Breakdown

### T1: Create INeoSystemRuntime Interface
**Status**: Pending
**Priority**: High (Blocking)
**File Scope**:
- `src/Neo.Core/Interfaces/INeoSystemRuntime.cs` (NEW)

**Description**:
Define abstraction layer for NeoSystem runtime operations to decouple from specific actor implementations.

**Interface Contract**:
```csharp
public interface INeoSystemRuntime : IAsyncDisposable
{
    IBlockchainBridge Blockchain { get; }
    IMemoryPoolBridge MemoryPool { get; }
    ILocalNodeBridge LocalNode { get; }
    bool IsOrleans { get; }
    Task StartAsync(CancellationToken ct = default);
    Task StopAsync(CancellationToken ct = default);
}
```

**Test Command**: `dotnet test tests/Neo.UnitTests --filter INeoSystemRuntime`

**Acceptance Criteria**:
- [ ] Interface defined with all required members
- [ ] XML documentation complete
- [ ] Build passes

---

### T2: Modify NeoSystem for IActorBridge Injection
**Status**: Pending
**Priority**: High
**File Scope**:
- `src/Neo.Node.Core/NeoSystem.cs` (MODIFY)

**Description**:
Add optional `IActorBridge` parameter to NeoSystem constructor. When provided, use bridge instead of creating Akka actors.

**Changes**:
1. Add `IActorBridge? _actorBridge` field
2. Add constructor overload accepting `IActorBridge`
3. Modify actor property getters to delegate to bridge when available
4. Update `Dispose()` to handle bridge disposal
5. Update `StartNode()` to use bridge when available

**Test Command**: `dotnet test tests/Neo.UnitTests --filter NeoSystem`

**Acceptance Criteria**:
- [ ] Constructor accepts optional IActorBridge
- [ ] Default behavior unchanged (Akka)
- [ ] Bridge operations work when injected
- [ ] All existing tests pass

---

### T3: Create AkkaActorBridge Implementation
**Status**: Pending
**Priority**: Medium
**Dependencies**: T1
**File Scope**:
- `src/Neo.Node.Core/Bridge/AkkaActorBridge.cs` (NEW)
- `src/Neo.Node.Core/Bridge/AkkaBlockchainBridge.cs` (NEW)
- `src/Neo.Node.Core/Bridge/AkkaMemoryPoolBridge.cs` (NEW)
- `src/Neo.Node.Core/Bridge/AkkaLocalNodeBridge.cs` (NEW)

**Description**:
Wrap existing Akka actors in IActorBridge interface for consistency and future migration.

**Test Command**: `dotnet test tests/Neo.UnitTests --filter AkkaActorBridge`

**Acceptance Criteria**:
- [ ] All bridge interfaces implemented
- [ ] Delegates to Akka IActorRef correctly
- [ ] Unit tests cover all operations

---

### T4: Runtime Switching Integration Tests
**Status**: Pending
**Priority**: High
**Dependencies**: T2, T3
**File Scope**:
- `tests/Neo.UnitTests/Integration/UT_RuntimeSwitching.cs` (NEW)

**Description**:
Integration tests verifying NeoSystem can switch between Akka and Orleans at runtime.

**Test Cases**:
1. `Test_NeoSystem_DefaultUsesAkka` - No bridge = Akka
2. `Test_NeoSystem_WithOrleansBridge` - Orleans bridge works
3. `Test_NeoSystem_WithAkkaBridge` - Akka bridge works
4. `Test_RuntimeSwitch_PreservesState` - State consistent across runtimes

**Test Command**: `dotnet test tests/Neo.UnitTests --filter RuntimeSwitching`

**Acceptance Criteria**:
- [ ] All 4 test cases pass
- [ ] Coverage ≥90% for new code
- [ ] No regression in existing tests

---

## Execution Order

```
T1 (Interface) ──┐
                 ├──> T2 (NeoSystem Modification)
                 │              │
                 ├──> T3 (AkkaActorBridge)
                 │              │
                 └──────────────┴──> T4 (Integration Tests)
```

**Parallelizable**: T1 alone, then T2+T3 in parallel, finally T4

## Test Commands Summary

| Task | Command |
|------|---------|
| T1 | `dotnet test tests/Neo.UnitTests --filter INeoSystemRuntime` |
| T2 | `dotnet test tests/Neo.UnitTests --filter NeoSystem` |
| T3 | `dotnet test tests/Neo.UnitTests --filter AkkaActorBridge` |
| T4 | `dotnet test tests/Neo.UnitTests --filter RuntimeSwitching` |
| All | `dotnet test tests/Neo.UnitTests -c Release` |

## Coverage Requirement
- Minimum: 90% line coverage for all new code
- Tool: `dotnet test --collect:"XPlat Code Coverage"`
