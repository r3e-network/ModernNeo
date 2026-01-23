// Copyright (C) 2015-2025 The Neo Project.
//
// INeoSystemRuntime.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Neo.Core.Interfaces
{
    /// <summary>
    /// Abstraction layer for NeoSystem runtime operations.
    /// Enables runtime abstraction for Orleans-backed implementations.
    /// </summary>
    public interface INeoSystemRuntime : IAsyncDisposable
    {
        /// <summary>
        /// Gets the blockchain operations interface.
        /// </summary>
        IBlockchainRuntime Blockchain { get; }

        /// <summary>
        /// Gets the memory pool operations interface.
        /// </summary>
        IMemoryPoolRuntime MemoryPool { get; }

        /// <summary>
        /// Gets the local node operations interface.
        /// </summary>
        ILocalNodeRuntime LocalNode { get; }

        /// <summary>
        /// Gets the task manager operations interface.
        /// </summary>
        ITaskManagerRuntime TaskManager { get; }

        /// <summary>
        /// Gets the consensus operations interface.
        /// </summary>
        IConsensusRuntime? Consensus { get; }

        /// <summary>
        /// Gets whether the runtime is Orleans-backed.
        /// </summary>
        bool IsOrleans { get; }

        /// <summary>
        /// Initializes the runtime.
        /// </summary>
        Task InitializeAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Starts the runtime.
        /// </summary>
        Task StartAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Stops the runtime.
        /// </summary>
        Task StopAsync(CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Runtime interface for blockchain operations.
    /// </summary>
    public interface IBlockchainRuntime
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
        /// Gets a block hash by index.
        /// </summary>
        Task<byte[]?> GetBlockHashByIndexAsync(uint index);

        /// <summary>
        /// Checks if a block exists.
        /// </summary>
        Task<bool> ContainsBlockAsync(byte[] hash);

        /// <summary>
        /// Checks if a transaction exists.
        /// </summary>
        Task<bool> ContainsTransactionAsync(byte[] hash);

        /// <summary>
        /// Sends a message to the blockchain actor/grain.
        /// </summary>
        /// <typeparam name="T">The message type.</typeparam>
        /// <param name="message">The message to send.</param>
        void Tell<T>(T message) where T : class;

        /// <summary>
        /// Sends a message and awaits a response.
        /// </summary>
        /// <typeparam name="TRequest">The request type.</typeparam>
        /// <typeparam name="TResponse">The response type.</typeparam>
        /// <param name="message">The message to send.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The response.</returns>
        Task<TResponse> AskAsync<TRequest, TResponse>(TRequest message, CancellationToken cancellationToken = default)
            where TRequest : class
            where TResponse : class;
    }

    /// <summary>
    /// Runtime interface for memory pool operations.
    /// </summary>
    public interface IMemoryPoolRuntime
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
    /// Runtime interface for local node operations.
    /// </summary>
    public interface ILocalNodeRuntime
    {
        /// <summary>
        /// Starts the local node with the specified configuration.
        /// </summary>
        Task StartAsync(RuntimeNodeConfig config);

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

        /// <summary>
        /// Sends a message to the local node actor/grain.
        /// </summary>
        /// <typeparam name="T">The message type.</typeparam>
        /// <param name="message">The message to send.</param>
        void Tell<T>(T message) where T : class;
    }

    /// <summary>
    /// Configuration for starting the runtime node.
    /// </summary>
    public record RuntimeNodeConfig(
        int TcpPort,
        int WsPort,
        int MinDesiredConnections,
        int MaxConnections,
        int MaxConnectionsPerAddress);

    /// <summary>
    /// Runtime interface for task manager operations.
    /// Coordinates block synchronization and inventory requests.
    /// </summary>
    public interface ITaskManagerRuntime
    {
        /// <summary>
        /// Registers a new remote node for task management.
        /// </summary>
        /// <param name="nodeId">The unique identifier for the remote node.</param>
        Task RegisterNodeAsync(string nodeId);

        /// <summary>
        /// Unregisters a remote node from task management.
        /// </summary>
        /// <param name="nodeId">The unique identifier for the remote node.</param>
        Task UnregisterNodeAsync(string nodeId);

        /// <summary>
        /// Requests blocks from a specific index.
        /// </summary>
        /// <param name="startIndex">The starting block index.</param>
        /// <param name="count">The number of blocks to request.</param>
        Task RequestBlocksAsync(uint startIndex, int count);

        /// <summary>
        /// Requests headers from a specific index.
        /// </summary>
        /// <param name="startIndex">The starting header index.</param>
        Task RequestHeadersAsync(uint startIndex);

        /// <summary>
        /// Gets the count of pending tasks.
        /// </summary>
        Task<int> GetPendingTaskCountAsync();

        /// <summary>
        /// Notifies the task manager of received headers.
        /// </summary>
        /// <param name="headerCount">The number of headers received.</param>
        Task NotifyHeadersReceivedAsync(int headerCount);

        /// <summary>
        /// Sends a message to the task manager actor/grain.
        /// </summary>
        /// <typeparam name="T">The message type.</typeparam>
        /// <param name="message">The message to send.</param>
        void Tell<T>(T message) where T : class;
    }

    /// <summary>
    /// Runtime interface for consensus operations.
    /// Manages dBFT consensus protocol.
    /// </summary>
    public interface IConsensusRuntime
    {
        /// <summary>
        /// Initializes the consensus with validator configuration.
        /// </summary>
        /// <param name="myIndex">This node's validator index.</param>
        /// <param name="validatorCount">Total number of validators.</param>
        Task InitializeAsync(int myIndex, int validatorCount);

        /// <summary>
        /// Starts the consensus process.
        /// </summary>
        Task StartAsync();

        /// <summary>
        /// Stops the consensus process.
        /// </summary>
        Task StopAsync();

        /// <summary>
        /// Handles a consensus message from a peer.
        /// </summary>
        /// <param name="message">The serialized consensus message.</param>
        /// <param name="senderAddress">The sender's address.</param>
        Task OnMessageAsync(byte[] message, string senderAddress);

        /// <summary>
        /// Gets the current view number.
        /// </summary>
        Task<byte> GetViewNumberAsync();

        /// <summary>
        /// Gets the current block index being processed.
        /// </summary>
        Task<uint> GetBlockIndexAsync();

        /// <summary>
        /// Checks if this node is the primary for the current view.
        /// </summary>
        Task<bool> IsPrimaryAsync();

        /// <summary>
        /// Gets whether consensus is currently running.
        /// </summary>
        Task<bool> IsRunningAsync();

        /// <summary>
        /// Sends a message to the consensus actor/grain.
        /// </summary>
        /// <typeparam name="T">The message type.</typeparam>
        /// <param name="message">The message to send.</param>
        void Tell<T>(T message) where T : class;
    }
}
