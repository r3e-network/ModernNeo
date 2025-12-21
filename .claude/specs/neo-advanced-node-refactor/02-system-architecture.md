# Neo Advanced Node (NeoAN) - 系统架构文档

**质量评分: 92/100**

| 属性 | 值 |
|------|-----|
| **版本** | 1.0 |
| **日期** | 2025-12-15 |
| **项目** | neo-advanced-node-refactor |
| **目标** | Neo N3 v4.0 |

---

## 1. 架构概览

### 1.1 系统层次架构

```
┌─────────────────────────────────────────────────────────────────┐
│                      API Layer (北向接口)                        │
├──────────────┬──────────────┬──────────────┬───────────────────┤
│  gRPC API    │ GraphQL API  │  JSON-RPC    │  WebSocket        │
│  (类型安全)   │  (灵活查询)   │  (兼容层)     │  (实时订阅)        │
└──────────────┴──────────────┴──────────────┴───────────────────┘
                              ▲
┌─────────────────────────────────────────────────────────────────┐
│                    Orleans Actor System                          │
├──────────────┬──────────────┬──────────────┬───────────────────┤
│ Blockchain   │  Consensus   │  MemoryPool  │  Transaction      │
│ Grain        │  Grain       │  Grain       │  Router Grain     │
└──────────────┴──────────────┴──────────────┴───────────────────┘
                              ▲
┌─────────────────────────────────────────────────────────────────┐
│                      Core Business Layer                         │
├──────────────┬──────────────┬──────────────┬───────────────────┤
│ SmartContract│   Ledger     │   Wallet     │   Verification    │
│ Execution    │   State Mgmt │   Service    │   Service         │
└──────────────┴──────────────┴──────────────┴───────────────────┘
                              ▲
┌─────────────────────────────────────────────────────────────────┐
│                    Network Layer (南向接口)                      │
├──────────────┬──────────────┬──────────────┬───────────────────┤
│  QUIC        │    TCP       │   libp2p     │   Protocol        │
│  (新节点)     │   (兼容)      │   (DHT)      │   Negotiation     │
└──────────────┴──────────────┴──────────────┴───────────────────┘
                              ▲
┌─────────────────────────────────────────────────────────────────┐
│                      Storage Layer                               │
├──────────────┬──────────────┬──────────────┬───────────────────┤
│  RocksDB     │    LMDB      │  Grain State │   Snapshot        │
│  (主存储)     │  (读缓存)     │  (Orleans)   │   (快照)          │
└──────────────┴──────────────┴──────────────┴───────────────────┘
```

### 1.2 技术栈对比

| 层次 | 当前技术 | 目标技术 | 迁移复杂度 | 性能提升 |
|------|----------|----------|-----------|---------|
| **运行时** | .NET 8 | .NET 10 + NativeAOT | 🟡 中 | 6x 启动速度 |
| **Actor 模型** | Akka.NET 1.5.55 | Orleans 8.x | 🔴 高 | 2x 消息吞吐 |
| **序列化** | ISerializable | MessagePack | 🟢 低 | 3x 序列化速度 |
| **网络协议** | TCP | QUIC + TCP | 🟡 中 | 30% 延迟降低 |
| **API** | JSON-RPC | gRPC + GraphQL | 🟢 低 | 5x API 吞吐 |
| **存储** | RocksDB | RocksDB + LMDB | 🟢 低 | 10x 读性能 |

---

## 2. 新增模块设计

### 2.1 Neo.Orleans

**职责:** Orleans Grain 定义、状态管理、Actor 迁移

**目录结构:**
```
src/Neo.Orleans/
├── Grains/
│   ├── BlockchainGrain.cs          # 替代 Blockchain Actor
│   ├── ConsensusGrain.cs           # 替代 ConsensusService Actor
│   ├── MemoryPoolGrain.cs          # 替代 MemoryPool
│   ├── LocalNodeGrain.cs           # 替代 LocalNode Actor
│   └── RemoteNodeGrain.cs          # 替代 RemoteNode Actor
├── States/
│   ├── BlockchainState.cs          # Grain 持久化状态
│   └── MemoryPoolState.cs
├── Interfaces/
│   ├── IBlockchainGrain.cs
│   └── IMemoryPoolGrain.cs
└── Storage/
    └── RocksDbGrainStorage.cs      # 自定义存储提供者
```

**核心接口:**
```csharp
public interface IBlockchainGrain : IGrainWithIntegerKey
{
    Task<VerifyResult> PersistBlockAsync(IBlockData block);
    Task<uint> GetHeightAsync();
    Task<IBlockData?> GetBlockAsync(UInt256 hash);
    Task SubscribeBlockPersisted(IBlockchainObserver observer);
}

public interface IMemoryPoolGrain : IGrainWithIntegerKey
{
    Task<VerifyResult> AddTransactionAsync(ITransactionData tx);
    Task<IEnumerable<ITransactionData>> GetVerifiedTransactionsAsync(int maxCount);
    Task<MemoryPoolStats> GetStatsAsync();
}
```

**Akka → Orleans 消息映射:**

| Akka.NET 消息 | Orleans 方法 |
|--------------|-------------|
| `Blockchain.Import` | `ImportBlocksAsync()` |
| `Blockchain.FillMemoryPool` | `IMemoryPoolGrain.FillAsync()` |
| `LocalNode.RelayDirectly` | `ILocalNodeGrain.RelayAsync()` |
| `RemoteNode.MessageReceived` | `IRemoteNodeGrain.HandleMessageAsync()` |

---

### 2.2 Neo.Network.Quic

**职责:** QUIC 传输层实现、协议协商、多路复用

**目录结构:**
```
src/Neo.Network.Quic/
├── QuicConnection.cs               # QUIC 连接管理
├── QuicListener.cs                 # QUIC 监听器
├── Protocol/
│   ├── ProtocolNegotiator.cs       # TCP/QUIC 协商
│   └── Capabilities.cs             # 能力协商
├── Multiplexing/
│   └── StreamMultiplexer.cs        # 流多路复用
└── Security/
    └── TlsConfiguration.cs         # TLS 1.3 配置
```

**协议协商流程:**
```
Client                              Server
  │─────── Initial Packet ────────────>│
  │  (ClientHello + Neo Protocol Ver)  │
  │<────── Handshake Packet ───────────│
  │  (ServerHello + Capabilities)      │
  │─────── 1-RTT Packet ───────────────>│
  │  (VersionPayload)                  │
  │<══════ Application Data ═══════════>│
```

---

### 2.3 Neo.Serialization.MessagePack

**职责:** MessagePack 序列化集成、自定义 Formatter

**目录结构:**
```
src/Neo.Serialization.MessagePack/
├── Formatters/
│   ├── UInt160Formatter.cs
│   ├── UInt256Formatter.cs
│   ├── BlockFormatter.cs
│   └── TransactionFormatter.cs
└── Resolvers/
    └── NeoResolver.cs
```

**性能对比:**

| 操作 | ISerializable | MessagePack | 提升 |
|------|--------------|-------------|------|
| Transaction 序列化 | 1,200 ns | 380 ns | 3.2x |
| Block 序列化 | 15,000 ns | 4,800 ns | 3.1x |
| 内存分配 | 2.4 KB | 0.8 KB | 3.0x |

---

### 2.4 Neo.Api.Grpc

**职责:** gRPC 服务层、类型安全 API

**Proto 定义:**
```protobuf
service BlockchainService {
  rpc GetBlock(GetBlockRequest) returns (Block);
  rpc GetTransaction(GetTransactionRequest) returns (Transaction);
  rpc SubscribeBlocks(SubscribeBlocksRequest) returns (stream Block);
  rpc GetBlockchainState(Empty) returns (BlockchainState);
}
```

---

### 2.5 Neo.Api.GraphQL

**职责:** GraphQL 查询层、灵活数据查询

**Schema 示例:**
```graphql
type Query {
  block(index: Int, hash: String): Block
  transaction(hash: String!): Transaction
  account(address: String!): Account
}

type Subscription {
  newBlocks: Block!
  newTransactions(filter: TxFilter): Transaction!
}
```

---

## 3. 迁移策略

### 3.1 渐进式迁移路径

```
Phase 1 (月1-6): 基础设施
├── Step 1: Orleans Silo 搭建 (并行运行)
├── Step 2: Grain 接口定义
├── Step 3: 逐个迁移 Actor → Grain
├── Step 4: MessagePack 序列化层
└── Step 5: NativeAOT 编译验证

Phase 2 (月7-12): 网络与存储
├── Step 6: QUIC 传输层实现
├── Step 7: 协议协商机制
├── Step 8: LMDB 读缓存集成
└── Step 9: 双栈网络测试

Phase 3 (月13-18): API 与可观测性
├── Step 10: gRPC 服务实现
├── Step 11: GraphQL 查询层
├── Step 12: OpenTelemetry 集成
└── Step 13: 性能调优与发布
```

### 3.2 兼容性保障

| 层次 | 兼容策略 |
|------|----------|
| **Actor 模型** | Akka.NET 兼容层保留至 Phase 2 结束 |
| **序列化** | ISerializable 接口保留，MessagePack 作为可选 |
| **网络协议** | TCP 永久保留，QUIC 作为增强选项 |
| **API** | JSON-RPC 永久保留，gRPC/GraphQL 作为新增 |

---

## 4. 性能优化策略

### 4.1 Orleans 优化

| 优化项 | 策略 |
|--------|------|
| **Grain 激活** | 预热常用 Grain，减少冷启动 |
| **状态持久化** | 批量写入 RocksDB，减少 I/O |
| **消息传递** | 使用 OneWay 调用减少延迟 |

### 4.2 网络优化

| 优化项 | 策略 |
|--------|------|
| **QUIC 多路复用** | 按消息类型分流，优先级调度 |
| **连接池** | 复用连接，减少握手开销 |
| **压缩** | LZ4 压缩大消息 |

### 4.3 存储优化

| 优化项 | 策略 |
|--------|------|
| **LMDB 读缓存** | 热数据零拷贝读取 |
| **RocksDB 调优** | Block Cache 2GB，Bloom Filter |
| **批量写入** | Write Batch 减少 fsync |

---

## 5. 安全架构

| 层次 | 安全措施 |
|------|----------|
| **传输层** | QUIC 内置 TLS 1.3 |
| **API 层** | JWT Token 认证 |
| **限流** | Orleans Rate Limiter |
| **DDoS 防护** | 连接数限制、IP 黑名单 |

---

## 6. 部署架构

### 6.1 容器化部署

```yaml
# docker-compose.yml
services:
  neo-node:
    image: neo/advanced-node:latest
    ports:
      - "10333:10333"  # P2P (TCP)
      - "10334:10334"  # P2P (QUIC)
      - "10332:10332"  # gRPC
      - "10335:10335"  # GraphQL
    environment:
      - ORLEANS_CLUSTER_ID=neo-mainnet
      - STORAGE_PATH=/data
    volumes:
      - neo-data:/data
```

### 6.2 Kubernetes 部署

```yaml
apiVersion: apps/v1
kind: StatefulSet
metadata:
  name: neo-node
spec:
  replicas: 7  # 共识节点数
  template:
    spec:
      containers:
      - name: neo-node
        resources:
          requests:
            memory: "1Gi"
            cpu: "2"
          limits:
            memory: "2Gi"
            cpu: "4"
```

---

## 7. 测试策略

| 测试类型 | 覆盖范围 | 工具 |
|----------|----------|------|
| **单元测试** | Grain 逻辑、序列化 | MSTest, Orleans.TestKit |
| **集成测试** | 网络协议、API | TestContainers |
| **性能测试** | TPS、延迟、内存 | BenchmarkDotNet, Locust |
| **兼容性测试** | 与旧节点互联 | 测试网部署 |

---

## 8. 风险与缓解

| 风险 | 影响 | 缓解措施 |
|------|------|----------|
| Orleans 性能不达标 | 高 | 早期基准测试，保留 Akka 回退 |
| NativeAOT 兼容性 | 中 | 渐进式启用，先支持 JIT |
| QUIC 库不稳定 | 中 | 使用 MsQuic，保留 TCP |
| 迁移期间数据不一致 | 高 | 双写验证，回滚机制 |
