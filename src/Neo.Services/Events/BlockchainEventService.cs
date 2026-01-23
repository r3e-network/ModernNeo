// Copyright (C) 2015-2025 The Neo Project.
//
// BlockchainEventService.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Ledger;
using Neo.Network.P2P.Payloads;
using System;
using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace Neo.Services.Events
{
    /// <summary>
    /// Service for subscribing to blockchain events using Rx observables.
    /// </summary>
    public sealed class BlockchainEventService : IBlockchainEventService, IDisposable
    {
        private readonly Subject<Block> _blockCommitted = new();
        private readonly Subject<Transaction> _transactionAdded = new();
        private readonly Subject<TransactionRemovedEvent> _transactionRemoved = new();
        private readonly IMemoryPool? _memoryPool;
        private bool _disposed;

        public BlockchainEventService(IMemoryPool? memoryPool = null)
        {
            // Subscribe to Blockchain.Committed event
            Blockchain.Committed += OnBlockCommitted;
            _memoryPool = memoryPool;
            if (_memoryPool != null)
            {
                _memoryPool.TransactionAdded += OnTransactionAdded;
                _memoryPool.TransactionRemoved += OnTransactionRemoved;
            }
        }

        public IObservable<Block> BlockCommitted => _blockCommitted.AsObservable();

        public IObservable<Transaction> TransactionAdded => _transactionAdded.AsObservable();

        public IObservable<TransactionRemovedEvent> TransactionRemoved => _transactionRemoved.AsObservable();

        private void OnBlockCommitted(NeoSystem system, Block block)
        {
            if (_disposed) return;
            _blockCommitted.OnNext(block);
        }

        private void OnTransactionAdded(object? sender, Transaction tx)
        {
            PublishTransactionAdded(tx);
        }

        private void OnTransactionRemoved(object? sender, TransactionRemovedEventArgs args)
        {
            if (_disposed) return;
            foreach (var tx in args.Transactions)
            {
                PublishTransactionRemoved(tx, args.Reason.ToString());
            }
        }

        /// <summary>
        /// Publishes a transaction added event. Called by mempool.
        /// </summary>
        public void PublishTransactionAdded(Transaction tx)
        {
            if (_disposed) return;
            _transactionAdded.OnNext(tx);
        }

        /// <summary>
        /// Publishes a transaction removed event. Called by mempool.
        /// </summary>
        public void PublishTransactionRemoved(Transaction tx, string reason)
        {
            if (_disposed) return;
            _transactionRemoved.OnNext(new TransactionRemovedEvent
            {
                Transaction = tx,
                Reason = reason
            });
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            Blockchain.Committed -= OnBlockCommitted;
            if (_memoryPool != null)
            {
                _memoryPool.TransactionAdded -= OnTransactionAdded;
                _memoryPool.TransactionRemoved -= OnTransactionRemoved;
            }

            _blockCommitted.OnCompleted();
            _blockCommitted.Dispose();

            _transactionAdded.OnCompleted();
            _transactionAdded.Dispose();

            _transactionRemoved.OnCompleted();
            _transactionRemoved.Dispose();
        }
    }
}
