// Copyright (C) 2015-2025 The Neo Project.
//
// TaskManagerGrain.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo;
using Neo.Extensions;
using Neo.IO.Caching;
using Neo.Ledger;
using Neo.Network.P2P;
using Neo.Network.P2P.Payloads;
using Neo.Orleans.Hosting;
using Neo.Orleans.Interfaces;
using Neo.SmartContract.Native;
using Orleans.Runtime;
using System.Linq;

namespace Neo.Orleans.Grains
{
    /// <summary>
    /// Orleans Grain implementation for managing synchronization tasks.
    /// Tracks inventory task scheduling and completion.
    /// </summary>
    public class TaskManagerGrain : Grain, ITaskManagerGrain
    {
        private readonly IPersistentState<TaskManagerState> _state;
        private readonly NeoSystem _system;
        private readonly NeoOrleansOptions _options;
        private IGrainTimer? _timer;

        private static readonly TimeSpan TimerInterval = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan TaskTimeout = TimeSpan.FromMinutes(1);
        private static readonly UInt256 HeaderTaskHash = UInt256.Zero;
        private const int MaxConcurrentTasks = 3;

        private readonly Dictionary<string, PeerSession> _sessions = new(StringComparer.Ordinal);
        private readonly Dictionary<UInt256, int> _globalInvTasks = new();
        private readonly Dictionary<uint, int> _globalIndexTasks = new();
        private readonly Dictionary<UInt256, PendingTask> _pendingTasks = new();
        private HashSetCache<UInt256>? _knownHashes;
        private uint _lastSeenPersistedIndex;
        private bool _ledgerInitialized;

        private bool HasHeaderTask => _globalInvTasks.ContainsKey(HeaderTaskHash);

        public TaskManagerGrain(
            [PersistentState("taskmanager", "TaskManagerStore")]
            IPersistentState<TaskManagerState> state,
            NeoSystem system,
            NeoOrleansOptions? options = null)
        {
            _state = state;
            _system = system;
            _options = options ?? new NeoOrleansOptions();
        }

        public override async Task OnActivateAsync(CancellationToken cancellationToken)
        {
            var capacity = Math.Max(100, _system.MemPool.Capacity);
            if (_options.MaxKnownHashes > 0)
                capacity = Math.Max(capacity, _options.MaxKnownHashes);
            _knownHashes = new HashSetCache<UInt256>(capacity);

            _timer ??= this.RegisterGrainTimer(
                _ => OnTimerAsync(),
                new GrainTimerCreationOptions
                {
                    DueTime = TimerInterval,
                    Period = TimerInterval,
                    Interleave = true
                });

            await EnsureLedgerInitializedAsync();

            await base.OnActivateAsync(cancellationToken);
        }

        public override Task OnDeactivateAsync(DeactivationReason reason, CancellationToken cancellationToken)
        {
            _timer?.Dispose();
            _timer = null;
            return base.OnDeactivateAsync(reason, cancellationToken);
        }

        public async Task RegisterSessionAsync(string peerId, uint startHeight, string userAgent)
        {
            if (string.IsNullOrWhiteSpace(peerId))
                return;

            await EnsureLedgerInitializedAsync();

            if (_sessions.TryGetValue(peerId, out var existing))
                ReleaseSession(peerId, existing);

            var session = new PeerSession(peerId, startHeight, userAgent);
            _sessions[peerId] = session;
            await RequestTasksAsync(peerId, session);
        }

        public Task UnregisterSessionAsync(string peerId)
        {
            if (_sessions.TryGetValue(peerId, out var session))
                ReleaseSession(peerId, session);

            return Task.CompletedTask;
        }

        public Task UpdatePeerHeightAsync(string peerId, uint blockIndex)
        {
            if (_sessions.TryGetValue(peerId, out var session))
                session.LastBlockIndex = blockIndex;

            return Task.CompletedTask;
        }

        public async Task<int> AddTasksAsync(IEnumerable<byte[]> hashes, byte inventoryType)
        {
            if (!Enum.IsDefined(typeof(InventoryType), (InventoryType)inventoryType))
                return 0;

            var type = (InventoryType)inventoryType;
            var added = 0;

            foreach (var hash in hashes)
            {
                if (!TryGetHash(hash, out var key))
                    continue;

                if (_knownHashes != null && _knownHashes.Contains(key))
                    continue;

                if (_pendingTasks.ContainsKey(key))
                    continue;

                _pendingTasks[key] = new PendingTask(hash, type);
                added++;
            }

            if (added > 0)
            {
                foreach (var (peerId, session) in _sessions)
                    await RequestTasksAsync(peerId, session);
            }

            return added;
        }

        public async Task NewTasksAsync(string peerId, byte inventoryType, IEnumerable<byte[]> hashes)
        {
            if (!_sessions.TryGetValue(peerId, out var session))
                return;

            if (!Enum.IsDefined(typeof(InventoryType), (InventoryType)inventoryType))
                return;

            var type = (InventoryType)inventoryType;
            var requestHashes = new HashSet<UInt256>();
            foreach (var hash in hashes)
            {
                if (!TryGetHash(hash, out var key))
                    continue;
                requestHashes.Add(key);
            }

            if (requestHashes.Count == 0)
                return;

            var snapshot = _system.StoreView;
            var currentHeight = Math.Max(await GetCurrentIndexAsync(), _lastSeenPersistedIndex);
            var headerHeight = _system.HeaderCache.Last?.Index ?? currentHeight;
            if (currentHeight < headerHeight &&
                (type == InventoryType.TX || (type == InventoryType.Block && currentHeight < session.LastBlockIndex - InvPayload.MaxHashesCount)))
            {
                await RequestTasksAsync(peerId, session);
                return;
            }

            if (_knownHashes != null)
                requestHashes.Remove(_knownHashes);

            if (type == InventoryType.Block)
                session.AvailableTasks.UnionWith(requestHashes.Where(p => _globalInvTasks.ContainsKey(p)));

            requestHashes.Remove(_globalInvTasks);
            if (requestHashes.Count == 0)
            {
                await RequestTasksAsync(peerId, session);
                return;
            }

            foreach (var hash in requestHashes)
            {
                IncrementGlobalTask(hash);
                session.InvTasks[hash] = TimeProvider.Current.UtcNow;
            }

            var remoteNode = GrainFactory.GetGrain<IRemoteNodeGrain>(peerId);
            foreach (var group in InvPayload.CreateGroup(type, requestHashes))
                _ = remoteNode.SendMessageAsync(MessageCommand.GetData, group);
        }

        public Task MarkKnownAsync(byte[] hash)
        {
            if (TryGetHash(hash, out var key))
                _knownHashes?.TryAdd(key);

            return Task.CompletedTask;
        }

        public Task<bool> IsKnownAsync(byte[] hash)
        {
            return Task.FromResult(TryGetHash(hash, out var key) && _knownHashes?.Contains(key) == true);
        }

        public Task<IReadOnlyList<TaskItem>> GetPendingTasksAsync(string peerId, int maxCount)
        {
            if (!_sessions.TryGetValue(peerId, out var session))
                return Task.FromResult<IReadOnlyList<TaskItem>>(Array.Empty<TaskItem>());

            var tasks = new List<TaskItem>();
            var now = DateTime.UtcNow;

            foreach (var (key, task) in _pendingTasks)
            {
                if (task.AssignedPeer != null)
                    continue;

                if (tasks.Count >= maxCount)
                    break;

                task.AssignedPeer = peerId;
                task.AssignedAt = now;
                session.PendingTasks.Add(key);
                session.InvTasks[key] = now;

                tasks.Add(new TaskItem(task.Hash, (byte)task.InventoryType, task.CreatedAt, task.RetryCount));
            }

            return Task.FromResult<IReadOnlyList<TaskItem>>(tasks);
        }

        public async Task CompleteTaskAsync(string peerId, byte[] hash)
        {
            if (!TryGetHash(hash, out var key))
                return;

            _knownHashes?.TryAdd(key);
            _globalInvTasks.Remove(key);
            foreach (var peerSession in _sessions.Values)
                peerSession.AvailableTasks.Remove(key);

            if (_pendingTasks.Remove(key, out var pending))
            {
                if (pending.AssignedPeer != null &&
                    _sessions.TryGetValue(pending.AssignedPeer, out var assignedSession))
                {
                    assignedSession.PendingTasks.Remove(key);
                }
            }

            if (_sessions.TryGetValue(peerId, out var currentSession))
            {
                currentSession.InvTasks.Remove(key);
                await RequestTasksAsync(peerId, currentSession);
            }
        }

        public async Task CompleteBlockAsync(string peerId, byte[] hash, uint blockIndex)
        {
            if (!TryGetHash(hash, out var key))
                return;

            _knownHashes?.TryAdd(key);
            _globalInvTasks.Remove(key);
            _globalIndexTasks.Remove(blockIndex);
            foreach (var peerSession in _sessions.Values)
                peerSession.AvailableTasks.Remove(key);

            if (_pendingTasks.Remove(key, out var pending))
            {
                if (pending.AssignedPeer != null &&
                    _sessions.TryGetValue(pending.AssignedPeer, out var assignedSession))
                {
                    assignedSession.PendingTasks.Remove(key);
                }
            }

            if (!_sessions.TryGetValue(peerId, out var session))
                return;

            session.InvTasks.Remove(key);
            session.IndexTasks.Remove(blockIndex);

            if (session.ReceivedBlock.TryGetValue(blockIndex, out var existingHash))
            {
                if (!existingHash.Equals(key))
                {
                    await DisconnectPeerAsync(peerId);
                    return;
                }
            }
            else
            {
                session.ReceivedBlock[blockIndex] = key;
            }
        }

        public Task<int> RestartTasksAsync(IEnumerable<byte[]> hashes, byte inventoryType)
        {
            if (!Enum.IsDefined(typeof(InventoryType), (InventoryType)inventoryType))
                return Task.FromResult(0);

            var type = (InventoryType)inventoryType;
            var restarted = 0;
            var validHashes = new List<UInt256>();
            var rawHashes = new List<byte[]>();

            foreach (var hash in hashes)
            {
                if (!TryGetHash(hash, out var key))
                    continue;

                _knownHashes?.Remove(key);
                _globalInvTasks.Remove(key);
                validHashes.Add(key);
                rawHashes.Add(hash);

                if (_pendingTasks.TryGetValue(key, out var task))
                {
                    task.AssignedPeer = null;
                    task.AssignedAt = null;
                    task.RetryCount++;
                }
                else
                {
                    _pendingTasks[key] = new PendingTask(hash, type);
                }

                restarted++;
            }

            if (rawHashes.Count > 0)
            {
                var localNode = GrainFactory.GetGrain<ILocalNodeGrain>(0);
                foreach (var group in InvPayload.CreateGroup(type, validHashes))
                {
                    var message = Message.Create(MessageCommand.GetData, group).ToArray();
                    _ = localNode.BroadcastAsync(message);
                }
            }

            return Task.FromResult(restarted);
        }

        public Task<int> ProcessTimeoutsAsync()
        {
            var now = DateTime.UtcNow;
            var timedOut = 0;

            foreach (var (key, task) in _pendingTasks.ToList())
            {
                if (task.AssignedPeer == null)
                    continue;

                var assignedAt = task.AssignedAt ?? task.CreatedAt;
                if (now - assignedAt > TaskTimeout)
                {
                    if (_sessions.TryGetValue(task.AssignedPeer, out var session))
                        session.PendingTasks.Remove(key);

                    task.AssignedPeer = null;
                    task.AssignedAt = null;
                    task.RetryCount++;
                    timedOut++;

                    if (task.RetryCount > 3)
                        _pendingTasks.Remove(key);
                }
            }

            return Task.FromResult(timedOut);
        }

        public Task<TaskManagerStateSummary> GetStateSummaryAsync()
        {
            var pendingCount = _pendingTasks.Count + _globalInvTasks.Count + _globalIndexTasks.Count;
            var knownCount = _knownHashes?.Count ?? 0;
            return Task.FromResult(new TaskManagerStateSummary(
                _sessions.Count,
                pendingCount,
                knownCount,
                _lastSeenPersistedIndex));
        }

        public Task ClearAsync()
        {
            _sessions.Clear();
            _globalInvTasks.Clear();
            _globalIndexTasks.Clear();
            _pendingTasks.Clear();
            _knownHashes?.Clear();
            _lastSeenPersistedIndex = 0;

            _state.State.Sessions.Clear();
            _state.State.GlobalTasks.Clear();
            _state.State.KnownHashes.Clear();
            _state.State.LastSeenBlockIndex = 0;
            return _state.WriteStateAsync();
        }

        public async Task NotifyHeadersAsync(string peerId)
        {
            if (!_sessions.TryGetValue(peerId, out var session))
                return;

            if (session.InvTasks.Remove(HeaderTaskHash))
                DecrementGlobalTask(HeaderTaskHash);

            await RequestTasksAsync(peerId, session);
        }

        public async Task NotifyPersistCompletedAsync(byte[] hash, uint blockIndex)
        {
            if (!TryGetHash(hash, out var key))
                return;

            _lastSeenPersistedIndex = Math.Max(_lastSeenPersistedIndex, blockIndex);

            foreach (var (peerId, session) in _sessions)
            {
                if (session.ReceivedBlock.TryGetValue(blockIndex, out var receivedHash))
                {
                    if (receivedHash.Equals(key))
                    {
                        session.ReceivedBlock.Remove(blockIndex);
                        await RequestTasksAsync(peerId, session);
                    }
                    else
                    {
                        await DisconnectPeerAsync(peerId);
                    }
                }
            }
        }

        public async Task NotifyInvalidBlockAsync(byte[] hash, uint blockIndex)
        {
            if (!TryGetHash(hash, out var key))
                return;

            foreach (var (peerId, session) in _sessions)
            {
                if (session.ReceivedBlock.TryGetValue(blockIndex, out var receivedHash) && receivedHash.Equals(key))
                    await DisconnectPeerAsync(peerId);
            }
        }

        private void ReleaseSession(string peerId, PeerSession session)
        {
            foreach (var hash in session.InvTasks.Keys)
                DecrementGlobalTask(hash);

            foreach (var index in session.IndexTasks.Keys)
                DecrementGlobalTask(index);

            foreach (var hash in session.PendingTasks)
            {
                if (_pendingTasks.TryGetValue(hash, out var task) &&
                    string.Equals(task.AssignedPeer, peerId, StringComparison.Ordinal))
                {
                    task.AssignedPeer = null;
                    task.AssignedAt = null;
                }
            }

            _sessions.Remove(peerId);
        }

        private async Task OnTimerAsync()
        {
            var now = TimeProvider.Current.UtcNow;

            foreach (var session in _sessions.Values)
            {
                RemoveExpiredTasks(session.InvTasks, now, p => DecrementGlobalTask(p.Key));
                RemoveExpiredTasks(session.IndexTasks, now, p => DecrementGlobalTask(p.Key));
            }

            _ = ProcessTimeoutsAsync();

            foreach (var (peerId, session) in _sessions)
                await RequestTasksAsync(peerId, session);
        }

        private static void RemoveExpiredTasks<TKey>(Dictionary<TKey, DateTime> tasks, DateTime now, Action<KeyValuePair<TKey, DateTime>> onRemoved) where TKey : notnull
        {
            var keysToRemove = new List<TKey>();
            foreach (var task in tasks)
            {
                if (now - task.Value > TaskTimeout)
                {
                    keysToRemove.Add(task.Key);
                }
            }
            foreach (var key in keysToRemove)
            {
                onRemoved(new KeyValuePair<TKey, DateTime>(key, tasks[key]));
                tasks.Remove(key);
            }
        }

        private static void RemoveWhere<T>(HashSet<T> set, Func<T, bool> predicate)
        {
            var itemsToRemove = new List<T>();
            foreach (var item in set)
            {
                if (predicate(item))
                {
                    itemsToRemove.Add(item);
                }
            }
            foreach (var item in itemsToRemove)
            {
                set.Remove(item);
            }
        }

        private async Task RequestTasksAsync(string peerId, PeerSession session)
        {
            if (session.HasTooManyTasks)
                return;

            var remoteNode = GrainFactory.GetGrain<IRemoteNodeGrain>(peerId);

            await EnsureLedgerInitializedAsync();

            if (await RequestPendingTasksAsync(remoteNode, session))
                return;

            if (session.AvailableTasks.Count > 0)
            {
                if (_knownHashes != null)
                    session.AvailableTasks.Remove(_knownHashes);

                if (_ledgerInitialized)
                {
                    var snapshot = _system.StoreView;
                    RemoveWhere(session.AvailableTasks, p => NativeContract.Ledger.ContainsBlock(snapshot, p));
                }
                var hashes = new HashSet<UInt256>(session.AvailableTasks);
                if (hashes.Count > 0)
                {
                    hashes.RemoveWhere(p => !IncrementGlobalTask(p));
                    session.AvailableTasks.Remove(hashes);
                    foreach (var hash in hashes)
                        session.InvTasks[hash] = DateTime.UtcNow;

                    foreach (var group in InvPayload.CreateGroup(InventoryType.Block, hashes))
                        _ = remoteNode.SendMessageAsync(MessageCommand.GetData, group);
                    return;
                }
            }

            var snapshotHeight = _system.StoreView;
            uint currentHeight = Math.Max(await GetCurrentIndexAsync(), _lastSeenPersistedIndex);
            uint headerHeight = _system.HeaderCache.Last?.Index ?? currentHeight;

            if ((!HasHeaderTask || _globalInvTasks[HeaderTaskHash] < MaxConcurrentTasks)
                && headerHeight < session.LastBlockIndex
                && !_system.HeaderCache.Full)
            {
                session.InvTasks[HeaderTaskHash] = DateTime.UtcNow;
                IncrementGlobalTask(HeaderTaskHash);
                _ = remoteNode.SendMessageAsync(
                    MessageCommand.GetHeaders,
                    GetBlockByIndexPayload.Create(headerHeight + 1));
            }
            else if (currentHeight < session.LastBlockIndex)
            {
                uint startHeight = currentHeight + 1;
                while (_globalIndexTasks.ContainsKey(startHeight) || session.ReceivedBlock.ContainsKey(startHeight))
                    startHeight++;

                if (startHeight > session.LastBlockIndex || startHeight >= currentHeight + InvPayload.MaxHashesCount)
                    return;

                uint endHeight = startHeight;
                while (!_globalIndexTasks.ContainsKey(++endHeight) &&
                       endHeight <= session.LastBlockIndex &&
                       endHeight <= currentHeight + InvPayload.MaxHashesCount)
                {
                }

                uint count = Math.Min(endHeight - startHeight, InvPayload.MaxHashesCount);
                for (uint i = 0; i < count; i++)
                {
                    session.IndexTasks[startHeight + i] = TimeProvider.Current.UtcNow;
                    IncrementGlobalTask(startHeight + i);
                }

                _ = remoteNode.SendMessageAsync(
                    MessageCommand.GetBlockByIndex,
                    GetBlockByIndexPayload.Create(startHeight, (short)count));
            }
            else if (!session.MempoolSent)
            {
                session.MempoolSent = true;
                _ = remoteNode.SendMessageAsync(MessageCommand.Mempool);
            }
        }

        private async Task<bool> RequestPendingTasksAsync(IRemoteNodeGrain remoteNode, PeerSession session)
        {
            if (_pendingTasks.Count == 0)
                return false;

            await EnsureLedgerInitializedAsync();

            var now = TimeProvider.Current.UtcNow;
            var snapshot = _system.StoreView;
            var currentHeight = Math.Max(await GetCurrentIndexAsync(), _lastSeenPersistedIndex);
            var headerHeight = _system.HeaderCache.Last?.Index ?? currentHeight;
            var headerBehind = currentHeight < headerHeight;
            var grouped = new Dictionary<InventoryType, HashSet<UInt256>>();
            var toRemove = new List<UInt256>();

            foreach (var (key, task) in _pendingTasks)
            {
                if (task.AssignedPeer != null)
                    continue;

                if (_knownHashes != null && _knownHashes.Contains(key))
                {
                    toRemove.Add(key);
                    continue;
                }

                if (_globalInvTasks.ContainsKey(key))
                    continue;

                if (headerBehind)
                {
                    if (task.InventoryType == InventoryType.TX)
                        continue;

                    if (task.InventoryType == InventoryType.Block &&
                        currentHeight < session.LastBlockIndex - InvPayload.MaxHashesCount)
                    {
                        continue;
                    }
                }

                if (_ledgerInitialized &&
                    task.InventoryType == InventoryType.Block &&
                    NativeContract.Ledger.ContainsBlock(snapshot, key))
                {
                    toRemove.Add(key);
                    continue;
                }

                if (_ledgerInitialized &&
                    task.InventoryType == InventoryType.TX &&
                    NativeContract.Ledger.ContainsTransaction(snapshot, key))
                {
                    toRemove.Add(key);
                    continue;
                }

                if (!IncrementGlobalTask(key))
                    continue;

                task.AssignedPeer = session.PeerId;
                task.AssignedAt = now;
                session.PendingTasks.Add(key);
                session.InvTasks[key] = now;

                if (!grouped.TryGetValue(task.InventoryType, out var set))
                {
                    set = new HashSet<UInt256>();
                    grouped[task.InventoryType] = set;
                }
                set.Add(key);
            }

            foreach (var key in toRemove)
                _pendingTasks.Remove(key);

            if (grouped.Count == 0)
                return false;

            foreach (var (type, hashes) in grouped)
                foreach (var group in InvPayload.CreateGroup(type, hashes))
                    _ = remoteNode.SendMessageAsync(MessageCommand.GetData, group);

            return true;
        }

        private Task EnsureLedgerInitializedAsync()
        {
            if (_ledgerInitialized)
                return Task.CompletedTask;

            _ledgerInitialized = NativeContract.Ledger.Initialized(_system.StoreView);
            return Task.CompletedTask;
        }

        private async Task<uint> GetCurrentIndexAsync()
        {
            await EnsureLedgerInitializedAsync();
            if (!_ledgerInitialized)
                return 0;
            try
            {
                return NativeContract.Ledger.CurrentIndex(_system.StoreView);
            }
            catch (KeyNotFoundException)
            {
                return 0;
            }
        }

        private async Task DisconnectPeerAsync(string peerId)
        {
            var remoteNode = GrainFactory.GetGrain<IRemoteNodeGrain>(peerId);
            await remoteNode.DisconnectAsync();
        }

        private static bool TryGetHash(byte[] hash, out UInt256 key)
        {
            if (hash == null || hash.Length != UInt256.Length)
            {
                key = UInt256.Zero;
                return false;
            }

            key = new UInt256(hash);
            return true;
        }

        private void DecrementGlobalTask(UInt256 hash)
        {
            if (_globalInvTasks.TryGetValue(hash, out var value))
            {
                if (value == 1)
                    _globalInvTasks.Remove(hash);
                else
                    _globalInvTasks[hash] = value - 1;
            }
        }

        private void DecrementGlobalTask(uint index)
        {
            if (_globalIndexTasks.TryGetValue(index, out var value))
            {
                if (value == 1)
                    _globalIndexTasks.Remove(index);
                else
                    _globalIndexTasks[index] = value - 1;
            }
        }

        private bool IncrementGlobalTask(UInt256 hash)
        {
            if (!_globalInvTasks.TryGetValue(hash, out var value))
            {
                _globalInvTasks[hash] = 1;
                return true;
            }

            if (value >= MaxConcurrentTasks)
                return false;

            _globalInvTasks[hash] = value + 1;
            return true;
        }

        private bool IncrementGlobalTask(uint index)
        {
            if (!_globalIndexTasks.TryGetValue(index, out var value))
            {
                _globalIndexTasks[index] = 1;
                return true;
            }

            if (value >= MaxConcurrentTasks)
                return false;

            _globalIndexTasks[index] = value + 1;
            return true;
        }
    }

    internal class PeerSession
    {
        public PeerSession(string peerId, uint startHeight, string userAgent)
        {
            PeerId = peerId;
            LastBlockIndex = startHeight;
            UserAgent = userAgent;
        }

        public string PeerId { get; }
        public string UserAgent { get; }
        public Dictionary<UInt256, DateTime> InvTasks { get; } = new();
        public Dictionary<uint, DateTime> IndexTasks { get; } = new();
        public HashSet<UInt256> AvailableTasks { get; } = new();
        public Dictionary<uint, UInt256> ReceivedBlock { get; } = new();
        public HashSet<UInt256> PendingTasks { get; } = new();
        public bool MempoolSent { get; set; }
        public uint LastBlockIndex { get; set; }
        public bool HasTooManyTasks => InvTasks.Count + IndexTasks.Count >= 100;
    }

    internal class PendingTask
    {
        public PendingTask(byte[] hash, InventoryType inventoryType)
        {
            Hash = hash;
            InventoryType = inventoryType;
            CreatedAt = DateTime.UtcNow;
        }

        public byte[] Hash { get; }
        public InventoryType InventoryType { get; }
        public DateTime CreatedAt { get; }
        public int RetryCount { get; set; }
        public string? AssignedPeer { get; set; }
        public DateTime? AssignedAt { get; set; }
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
        [Id(6)] public DateTime? AssignedAt { get; set; }
    }
}
