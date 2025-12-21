// Copyright (C) 2015-2025 The Neo Project.
//
// TaskManagerGrain.cs file belongs to the neo project and is free
// software distributed under the MIT software license.

using Neo.Orleans.Interfaces;
using Orleans.Runtime;

namespace Neo.Orleans.Grains;

/// <summary>
/// Orleans Grain implementation for managing synchronization tasks.
/// Replaces Akka.NET TaskManager Actor with inventory task tracking.
/// </summary>
public class TaskManagerGrain : Grain, ITaskManagerGrain
{
    private readonly IPersistentState<TaskManagerState> _state;

    public TaskManagerGrain(
        [PersistentState("taskmanager", "TaskManagerStore")]
        IPersistentState<TaskManagerState> state)
    {
        _state = state;
    }

    public Task RegisterSessionAsync(string peerId, uint startHeight, string userAgent)
    {
        _state.State.Sessions[peerId] = new TaskSession
        {
            PeerId = peerId,
            StartHeight = startHeight,
            UserAgent = userAgent,
            LastHeight = startHeight,
            RegisteredAt = DateTime.UtcNow,
            PendingTasks = new HashSet<string>()
        };
        return _state.WriteStateAsync();
    }

    public Task UnregisterSessionAsync(string peerId)
    {
        if (_state.State.Sessions.TryGetValue(peerId, out var session))
        {
            // Release any pending tasks back to global pool
            foreach (var hashHex in session.PendingTasks)
            {
                if (_state.State.GlobalTasks.TryGetValue(hashHex, out var task))
                {
                    task.AssignedPeer = null;
                }
            }
            _state.State.Sessions.Remove(peerId);
        }
        return _state.WriteStateAsync();
    }

    public Task UpdatePeerHeightAsync(string peerId, uint blockIndex)
    {
        if (_state.State.Sessions.TryGetValue(peerId, out var session))
        {
            session.LastHeight = blockIndex;
            if (blockIndex > _state.State.LastSeenBlockIndex)
            {
                _state.State.LastSeenBlockIndex = blockIndex;
            }
        }
        return Task.CompletedTask;
    }

    public async Task<int> AddTasksAsync(IEnumerable<byte[]> hashes, byte inventoryType)
    {
        var added = 0;
        foreach (var hash in hashes)
        {
            var hashHex = Convert.ToHexString(hash);

            // Skip if already known
            if (_state.State.KnownHashes.Contains(hashHex))
                continue;

            // Skip if already have task
            if (_state.State.GlobalTasks.ContainsKey(hashHex))
                continue;

            _state.State.GlobalTasks[hashHex] = new GlobalTask
            {
                HashHex = hashHex,
                Hash = hash,
                InventoryType = inventoryType,
                CreatedAt = DateTime.UtcNow,
                RetryCount = 0,
                AssignedPeer = null
            };
            added++;
        }

        if (added > 0)
            await _state.WriteStateAsync();

        return added;
    }

    public Task MarkKnownAsync(byte[] hash)
    {
        var hashHex = Convert.ToHexString(hash);
        _state.State.KnownHashes.Add(hashHex);

        // Remove from global tasks if present
        if (_state.State.GlobalTasks.TryGetValue(hashHex, out var task))
        {
            if (task.AssignedPeer != null &&
                _state.State.Sessions.TryGetValue(task.AssignedPeer, out var session))
            {
                session.PendingTasks.Remove(hashHex);
            }
            _state.State.GlobalTasks.Remove(hashHex);
        }

        // Trim known hashes if too large
        if (_state.State.KnownHashes.Count > _state.State.MaxKnownHashes)
        {
            // Remove oldest entries (simple FIFO approximation)
            var toRemove = _state.State.KnownHashes.Take(_state.State.KnownHashes.Count - _state.State.MaxKnownHashes).ToList();
            foreach (var h in toRemove)
                _state.State.KnownHashes.Remove(h);
        }

        return _state.WriteStateAsync();
    }

    public Task<bool> IsKnownAsync(byte[] hash)
    {
        var hashHex = Convert.ToHexString(hash);
        return Task.FromResult(_state.State.KnownHashes.Contains(hashHex));
    }

    public Task<IReadOnlyList<TaskItem>> GetPendingTasksAsync(string peerId, int maxCount)
    {
        if (!_state.State.Sessions.TryGetValue(peerId, out var session))
            return Task.FromResult<IReadOnlyList<TaskItem>>(Array.Empty<TaskItem>());

        var tasks = new List<TaskItem>();

        // Get unassigned tasks
        foreach (var (hashHex, task) in _state.State.GlobalTasks)
        {
            if (task.AssignedPeer != null)
                continue;

            if (tasks.Count >= maxCount)
                break;

            task.AssignedPeer = peerId;
            session.PendingTasks.Add(hashHex);

            tasks.Add(new TaskItem(task.Hash, task.InventoryType, task.CreatedAt, task.RetryCount));
        }

        return Task.FromResult<IReadOnlyList<TaskItem>>(tasks);
    }

    public Task CompleteTaskAsync(string peerId, byte[] hash)
    {
        var hashHex = Convert.ToHexString(hash);

        // Mark as known
        _state.State.KnownHashes.Add(hashHex);

        // Remove from global tasks
        _state.State.GlobalTasks.Remove(hashHex);

        // Remove from session pending
        if (_state.State.Sessions.TryGetValue(peerId, out var session))
        {
            session.PendingTasks.Remove(hashHex);
        }

        return _state.WriteStateAsync();
    }

    public async Task<int> RestartTasksAsync(IEnumerable<byte[]> hashes, byte inventoryType)
    {
        var restarted = 0;
        foreach (var hash in hashes)
        {
            var hashHex = Convert.ToHexString(hash);

            if (_state.State.KnownHashes.Contains(hashHex))
                continue;

            if (_state.State.GlobalTasks.TryGetValue(hashHex, out var task))
            {
                // Unassign and increment retry
                if (task.AssignedPeer != null &&
                    _state.State.Sessions.TryGetValue(task.AssignedPeer, out var session))
                {
                    session.PendingTasks.Remove(hashHex);
                }
                task.AssignedPeer = null;
                task.RetryCount++;
                restarted++;
            }
            else
            {
                // Add as new task
                _state.State.GlobalTasks[hashHex] = new GlobalTask
                {
                    HashHex = hashHex,
                    Hash = hash,
                    InventoryType = inventoryType,
                    CreatedAt = DateTime.UtcNow,
                    RetryCount = 0,
                    AssignedPeer = null
                };
                restarted++;
            }
        }

        if (restarted > 0)
            await _state.WriteStateAsync();

        return restarted;
    }

    public async Task<int> ProcessTimeoutsAsync()
    {
        var timeout = TimeSpan.FromMinutes(1);
        var now = DateTime.UtcNow;
        var timedOut = 0;

        foreach (var (hashHex, task) in _state.State.GlobalTasks.ToList())
        {
            if (task.AssignedPeer == null)
                continue;

            if (now - task.CreatedAt > timeout)
            {
                // Unassign task
                if (_state.State.Sessions.TryGetValue(task.AssignedPeer, out var session))
                {
                    session.PendingTasks.Remove(hashHex);
                }
                task.AssignedPeer = null;
                task.RetryCount++;
                timedOut++;

                // Remove if too many retries
                if (task.RetryCount > 3)
                {
                    _state.State.GlobalTasks.Remove(hashHex);
                }
            }
        }

        if (timedOut > 0)
            await _state.WriteStateAsync();

        return timedOut;
    }

    public Task<TaskManagerStateSummary> GetStateSummaryAsync()
    {
        return Task.FromResult(new TaskManagerStateSummary(
            _state.State.Sessions.Count,
            _state.State.GlobalTasks.Count,
            _state.State.KnownHashes.Count,
            _state.State.LastSeenBlockIndex));
    }

    public Task ClearAsync()
    {
        _state.State.Sessions.Clear();
        _state.State.GlobalTasks.Clear();
        _state.State.KnownHashes.Clear();
        _state.State.LastSeenBlockIndex = 0;
        return _state.WriteStateAsync();
    }
}

/// <summary>
/// Persistent state for TaskManagerGrain.
/// </summary>
[GenerateSerializer]
public class TaskManagerState
{
    [Id(0)] public Dictionary<string, TaskSession> Sessions { get; set; } = new();
    [Id(1)] public Dictionary<string, GlobalTask> GlobalTasks { get; set; } = new();
    [Id(2)] public HashSet<string> KnownHashes { get; set; } = new();
    [Id(3)] public uint LastSeenBlockIndex { get; set; }
    [Id(4)] public int MaxKnownHashes { get; set; } = 50000;
}

/// <summary>
/// Represents a peer session.
/// </summary>
[GenerateSerializer]
public class TaskSession
{
    [Id(0)] public string PeerId { get; set; } = "";
    [Id(1)] public uint StartHeight { get; set; }
    [Id(2)] public uint LastHeight { get; set; }
    [Id(3)] public string UserAgent { get; set; } = "";
    [Id(4)] public DateTime RegisteredAt { get; set; }
    [Id(5)] public HashSet<string> PendingTasks { get; set; } = new();
}

/// <summary>
/// Represents a global task.
/// </summary>
[GenerateSerializer]
public class GlobalTask
{
    [Id(0)] public string HashHex { get; set; } = "";
    [Id(1)] public byte[] Hash { get; set; } = Array.Empty<byte>();
    [Id(2)] public byte InventoryType { get; set; }
    [Id(3)] public DateTime CreatedAt { get; set; }
    [Id(4)] public int RetryCount { get; set; }
    [Id(5)] public string? AssignedPeer { get; set; }
}
