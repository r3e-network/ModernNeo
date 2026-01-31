// Copyright (C) 2015-2025 The Neo Project.
//
// NeoSystemAdapter.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo;
using Neo.Ledger;
using Neo.Network.P2P.Payloads;
using Neo.Orleans.Interfaces;
using Neo.Persistence;
using Neo.SmartContract.Native;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Neo.Orleans.Adapters
{
    /// <summary>
    /// Adapter that wraps NeoSystem to implement INeoSystem abstraction.
    /// This enables dependency inversion for testing and alternative implementations.
    /// </summary>
    public sealed class NeoSystemAdapter : INeoSystem
    {
        private readonly NeoSystem _neoSystem;

        public NeoSystemAdapter(NeoSystem neoSystem)
        {
            _neoSystem = neoSystem ?? throw new ArgumentNullException(nameof(neoSystem));
        }

        /// <summary>
        /// Gets the underlying NeoSystem instance.
        /// Use with caution - this bypasses the INeoSystem abstraction.
        /// </summary>
        public NeoSystem NeoSystem => _neoSystem;

        public IProtocolSettings Settings => _neoSystem.Settings;

        public IStore Store => _neoSystem.Store;

        public MemoryPool MemPool => _neoSystem.MemPool;

        public Block GenesisBlock => _neoSystem.GenesisBlock;

        public StoreCache StoreView => _neoSystem.StoreView;

        public HeaderCache HeaderCache => _neoSystem.HeaderCache;

        public IRelayCache RelayCache => (IRelayCache)_neoSystem.RelayCache;

        public Task<Block> AddBlockAsync(Block block)
        {
            throw new NotSupportedException("Use NeoSystem directly for block addition in Orleans mode");
        }

        public Task<bool> RelayBlockAsync(Block block)
        {
            throw new NotSupportedException("Use NeoSystem directly for block relay in Orleans mode");
        }

        public Task<TransactionResult> SendRawTransactionAsync(Transaction tx)
        {
            throw new NotSupportedException("Use NeoSystem directly for transaction sending in Orleans mode");
        }

        public StoreCache GetSnapshotCache() => _neoSystem.GetSnapshotCache();

        public TimeSpan GetTimePerBlock() => _neoSystem.GetTimePerBlock();

        public uint GetMaxTraceableBlocks() => _neoSystem.GetMaxTraceableBlocks();

        public ContainsTransactionType ContainsTransaction(UInt256 hash) => _neoSystem.ContainsTransaction(hash);

        public bool ContainsConflictHash(UInt256 hash, IEnumerable<UInt160> signers)
        {
            var snapshot = _neoSystem.StoreView;
            var maxTraceableBlocks = _neoSystem.GetMaxTraceableBlocks();
            return NativeContract.Ledger.ContainsConflictHash(snapshot, hash, signers.ToArray().AsSpan(), maxTraceableBlocks);
        }

        public IStore LoadStore(string path) => _neoSystem.LoadStore(path);

        public void SetLocalNode(ISystemMessageTarget localNode) => _neoSystem.SetLocalNode(localNode);

        public void Dispose() => _neoSystem.Dispose();
    }
}
