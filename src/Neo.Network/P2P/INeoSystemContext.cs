// Copyright (C) 2015-2025 The Neo Project.
//
// INeoSystemContext.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Akka.Actor;
using Neo.Core.Interfaces;
using Neo.Ledger;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using System.Collections.Generic;

namespace Neo.Network.P2P
{
    /// <summary>
    /// Abstraction over the node runtime used by the P2P stack.
    /// </summary>
    public interface INeoSystemContext
    {
        ProtocolSettings Settings { get; }

        StoreCache StoreView { get; }

        HeaderCache HeaderCache { get; }

        IMemoryPool MemPool { get; }

        /// <summary>
        /// Gets the unified runtime abstraction for Orleans/Akka switching.
        /// </summary>
        INeoSystemRuntime Runtime { get; }

        IActorRef LocalNode { get; }

        IActorRef Blockchain { get; }

        IActorRef TaskManager { get; }

        IActorRef TxRouter { get; }

        uint GetCurrentIndex(StoreCache snapshot);

        bool TryGetBlockIndex(StoreCache snapshot, UInt256 hash, out uint index);

        UInt256? GetBlockHash(StoreCache snapshot, uint index);

        Block? GetBlock(StoreCache snapshot, uint index);

        Block? GetBlock(StoreCache snapshot, UInt256 hash);

        Header? GetHeader(StoreCache snapshot, uint index);

        bool ContainsBlock(StoreCache snapshot, UInt256 hash);

        bool ContainsTransaction(StoreCache snapshot, UInt256 hash);

        ContainsTransactionType ContainsTransaction(UInt256 hash);

        bool ContainsConflictHash(UInt256 hash, IEnumerable<UInt160> signers);

        bool TryGetRelay(UInt256 hash, out IInventory inventory);
    }
}
