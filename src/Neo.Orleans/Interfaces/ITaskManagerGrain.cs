// Copyright (C) 2015-2025 The Neo Project.
//
// ITaskManagerGrain.cs file belongs to the neo project and is free
// software distributed under the MIT software license.

namespace Neo.Orleans.Interfaces;

/// <summary>
/// Orleans Grain interface for managing synchronization tasks.
/// Replaces Akka.NET TaskManager Actor with inventory task tracking.
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
