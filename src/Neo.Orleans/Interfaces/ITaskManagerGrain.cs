// Copyright (C) 2015-2025 The Neo Project.
//
// ITaskManagerGrain.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

namespace Neo.Orleans.Interfaces
{
    /// <summary>
    /// Orleans Grain interface for managing synchronization tasks.
    /// Tracks inventory task scheduling and completion.
    /// </summary>
    public interface ITaskManagerGrain : IGrainWithIntegerKey
    {
        /// <summary>
        /// Registers a new peer session with its version info.
        /// </summary>
        Task RegisterSessionAsync(string peerId, uint startHeight, string userAgent);

        /// <summary>
        /// Unregisters a peer session.
        /// </summary>
        Task UnregisterSessionAsync(string peerId);

        /// <summary>
        /// Updates the last known block index for a peer.
        /// </summary>
        Task UpdatePeerHeightAsync(string peerId, uint blockIndex);

        /// <summary>
        /// Adds new inventory tasks to be fetched.
        /// </summary>
        Task<int> AddTasksAsync(IEnumerable<byte[]> hashes, byte inventoryType);

        /// <summary>
        /// Adds new inventory tasks announced by a specific peer.
        /// </summary>
        Task NewTasksAsync(string peerId, byte inventoryType, IEnumerable<byte[]> hashes);

        /// <summary>
        /// Marks a hash as known (already received).
        /// </summary>
        Task MarkKnownAsync(byte[] hash);

        /// <summary>
        /// Checks if a hash is already known.
        /// </summary>
        Task<bool> IsKnownAsync(byte[] hash);

        /// <summary>
        /// Gets pending tasks for a specific peer.
        /// </summary>
        Task<IReadOnlyList<TaskItem>> GetPendingTasksAsync(string peerId, int maxCount);

        /// <summary>
        /// Marks a task as completed.
        /// </summary>
        Task CompleteTaskAsync(string peerId, byte[] hash);

        /// <summary>
        /// Marks a block task as completed and tracks the block index.
        /// </summary>
        Task CompleteBlockAsync(string peerId, byte[] hash, uint blockIndex);

        /// <summary>
        /// Restarts failed or timed out tasks.
        /// </summary>
        Task<int> RestartTasksAsync(IEnumerable<byte[]> hashes, byte inventoryType);

        /// <summary>
        /// Processes timeout for stale tasks.
        /// </summary>
        Task<int> ProcessTimeoutsAsync();

        /// <summary>
        /// Gets the task manager state summary.
        /// </summary>
        Task<TaskManagerStateSummary> GetStateSummaryAsync();

        /// <summary>
        /// Notifies the task manager that headers were received from a peer.
        /// </summary>
        Task NotifyHeadersAsync(string peerId);

        /// <summary>
        /// Notifies the task manager that a block was persisted.
        /// </summary>
        Task NotifyPersistCompletedAsync(byte[] hash, uint blockIndex);

        /// <summary>
        /// Notifies the task manager that a block was invalid.
        /// </summary>
        Task NotifyInvalidBlockAsync(byte[] hash, uint blockIndex);

        /// <summary>
        /// Clears all tasks and sessions.
        /// </summary>
        Task ClearAsync();
    }

    /// <summary>
    /// Represents a pending task item.
    /// </summary>
    [GenerateSerializer]
    public record TaskItem(
        [property: Id(0)] byte[] Hash,
        [property: Id(1)] byte InventoryType,
        [property: Id(2)] DateTime CreatedAt,
        [property: Id(3)] int RetryCount);

    /// <summary>
    /// Summary of task manager state.
    /// </summary>
    [GenerateSerializer]
    public record TaskManagerStateSummary(
        [property: Id(0)] int SessionCount,
        [property: Id(1)] int PendingTaskCount,
        [property: Id(2)] int KnownHashCount,
        [property: Id(3)] uint LastSeenBlockIndex);
}
