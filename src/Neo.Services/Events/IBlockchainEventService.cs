// Copyright (C) 2015-2025 The Neo Project.
//
// IBlockchainEventService.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Network.P2P.Payloads;
using System;

namespace Neo.Services.Events
{
    /// <summary>
    /// Service for subscribing to blockchain events.
    /// </summary>
    public interface IBlockchainEventService
    {
        /// <summary>
        /// Observable stream of new blocks.
        /// </summary>
        IObservable<Block> BlockCommitted { get; }

        /// <summary>
        /// Observable stream of new transactions added to mempool.
        /// </summary>
        IObservable<Transaction> TransactionAdded { get; }

        /// <summary>
        /// Observable stream of transactions removed from mempool.
        /// </summary>
        IObservable<TransactionRemovedEvent> TransactionRemoved { get; }
    }

    /// <summary>
    /// Event data for transaction removal.
    /// </summary>
    public sealed class TransactionRemovedEvent
    {
        public required Transaction Transaction { get; init; }
        public required string Reason { get; init; }
    }
}
