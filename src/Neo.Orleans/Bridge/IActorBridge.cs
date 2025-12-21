// Copyright (C) 2015-2025 The Neo Project.
//
// IActorBridge.cs file belongs to the neo project and is free
// software distributed under the MIT software license.

using Neo.Core.Interfaces;

namespace Neo.Orleans.Bridge;

/// <summary>
/// Bridge interface for abstracting actor system operations.
/// Allows NeoSystem to work with either Akka.NET or Orleans.
/// </summary>
public interface IActorBridge : IAsyncDisposable
{
    /// <summary>
    /// Gets the blockchain operations interface.
    /// </summary>
    IBlockchainBridge Blockchain { get; }

    /// <summary>
    /// Gets the memory pool operations interface.
    /// </summary>
    IMemoryPoolBridge MemoryPool { get; }

    /// <summary>
    /// Gets the local node operations interface.
    /// </summary>
    ILocalNodeBridge LocalNode { get; }

    /// <summary>
    /// Initializes the actor bridge.
    /// </summary>
    Task InitializeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Starts the actor system.
    /// </summary>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops the actor system.
    /// </summary>
    Task StopAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets whether the bridge is using Orleans (true) or Akka (false).
    /// </summary>
    bool IsOrleans { get; }
}

/// <summary>
/// Bridge interface for blockchain operations.
/// </summary>
public interface IBlockchainBridge
{
    /// <summary>
    /// Gets the current blockchain height.
    /// </summary>
    Task<uint> GetHeightAsync();

    /// <summary>
    /// Gets the current header height.
    /// </summary>
    Task<uint> GetHeaderHeightAsync();

    /// <summary>
    /// Persists a block to the blockchain.
    /// </summary>
    Task<bool> PersistBlockAsync(IBlockData block);

    /// <summary>
    /// Gets a block by hash.
    /// </summary>
    Task<IBlockData?> GetBlockByHashAsync(byte[] hash);

    /// <summary>
    /// Gets a block by index.
    /// </summary>
    Task<IBlockData?> GetBlockByIndexAsync(uint index);

    /// <summary>
    /// Checks if a transaction exists.
    /// </summary>
    Task<bool> ContainsTransactionAsync(byte[] hash);
}

/// <summary>
/// Bridge interface for memory pool operations.
/// </summary>
public interface IMemoryPoolBridge
{
    /// <summary>
    /// Adds a transaction to the memory pool.
    /// </summary>
    Task<bool> AddTransactionAsync(ITransactionData transaction);

    /// <summary>
    /// Gets verified transactions from the pool.
    /// </summary>
    Task<IEnumerable<ITransactionData>> GetVerifiedTransactionsAsync(int maxCount);

    /// <summary>
    /// Gets the count of verified transactions.
    /// </summary>
    Task<int> GetVerifiedCountAsync();

    /// <summary>
    /// Gets the count of unverified transactions.
    /// </summary>
    Task<int> GetUnverifiedCountAsync();

    /// <summary>
    /// Checks if a transaction exists in the pool.
    /// </summary>
    Task<bool> ContainsKeyAsync(byte[] hash);
}

/// <summary>
/// Bridge interface for local node operations.
/// </summary>
public interface ILocalNodeBridge
{
    /// <summary>
    /// Starts the local node with the specified configuration.
    /// </summary>
    Task StartAsync(LocalNodeStartConfig config);

    /// <summary>
    /// Stops the local node.
    /// </summary>
    Task StopAsync();

    /// <summary>
    /// Gets the count of connected peers.
    /// </summary>
    Task<int> GetConnectedPeerCountAsync();

    /// <summary>
    /// Gets the count of unconnected peers.
    /// </summary>
    Task<int> GetUnconnectedPeerCountAsync();

    /// <summary>
    /// Relays an inventory item to peers.
    /// </summary>
    Task RelayAsync(byte[] hash, byte inventoryType);

    /// <summary>
    /// Broadcasts a message to all peers.
    /// </summary>
    Task BroadcastAsync(byte[] message);
}

/// <summary>
/// Configuration for starting the local node.
/// </summary>
public record LocalNodeStartConfig(
    int TcpPort,
    int WsPort,
    int MinDesiredConnections,
    int MaxConnections,
    int MaxConnectionsPerAddress);
