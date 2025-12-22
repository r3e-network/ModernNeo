// Copyright (C) 2015-2025 The Neo Project.
//
// MemoryPoolState.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

namespace Neo.Orleans.States
{
    /// <summary>
    /// Persistent state for MemoryPoolGrain.
    /// Mirrors Akka.NET MemoryPool with Verified/Unverified separation.
    /// </summary>
    [GenerateSerializer]
    public class MemoryPoolState
    {
        /// <summary>
        /// Maximum capacity of the memory pool.
        /// </summary>
        [Id(0)] public int Capacity { get; set; } = 50000;

        /// <summary>
        /// Verified transactions in the pool, keyed by hash hex.
        /// These transactions have been validated for the current block.
        /// </summary>
        [Id(1)] public Dictionary<string, PoolItemState> VerifiedTransactions { get; set; } = new();

        /// <summary>
        /// Unverified transactions in the pool, keyed by hash hex.
        /// These were valid in a prior block but need re-verification.
        /// </summary>
        [Id(2)] public Dictionary<string, PoolItemState> UnverifiedTransactions { get; set; } = new();

        /// <summary>
        /// Current block height for expiration checks.
        /// </summary>
        [Id(3)] public uint CurrentBlockHeight { get; set; }

        /// <summary>
        /// Conflict hashes - transactions that conflict with verified pool transactions.
        /// Key: conflicting tx hash, Value: set of pool tx hashes that conflict.
        /// </summary>
        [Id(4)] public Dictionary<string, HashSet<string>> Conflicts { get; set; } = new();

        /// <summary>
        /// Block index when transactions were last rebroadcast.
        /// Key: tx hash hex, Value: block index of last broadcast.
        /// </summary>
        [Id(5)] public Dictionary<string, uint> LastBroadcastBlock { get; set; } = new();
    }
}
