// Copyright (C) 2015-2025 The Neo Project.
//
// BlockchainMetrics.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using System;

namespace Neo.Observability.Metrics
{
    /// <summary>
    /// Provides pre-defined metrics for blockchain operations.
    /// </summary>
    public sealed class BlockchainMetrics
    {
        private readonly IMetricsProvider _provider;

        // Block metrics
        private readonly ICounter _blocksProcessed;
        private readonly IGauge _blockHeight;
        private readonly IHistogram _blockProcessingTime;
        private readonly IHistogram _blockSize;
        private readonly IGauge _blockTransactionCount;

        // Transaction metrics
        private readonly ICounter _transactionsProcessed;
        private readonly ICounter _transactionsFailed;
        private readonly IHistogram _transactionProcessingTime;
        private readonly IGauge _memPoolSize;
        private readonly IGauge _memPoolVerifiedCount;
        private readonly IGauge _memPoolUnverifiedCount;

        // Network metrics
        private readonly IGauge _connectedPeers;
        private readonly ICounter _messagesReceived;
        private readonly ICounter _messagesSent;
        private readonly IHistogram _messageLatency;

        // Storage metrics
        private readonly IHistogram _storageReadTime;
        private readonly IHistogram _storageWriteTime;
        private readonly ICounter _storageReads;
        private readonly ICounter _storageWrites;

        /// <summary>
        /// Gets the number of blocks processed.
        /// </summary>
        public ICounter BlocksProcessed => _blocksProcessed;

        /// <summary>
        /// Gets the current block height gauge.
        /// </summary>
        public IGauge BlockHeight => _blockHeight;

        /// <summary>
        /// Gets the block processing time histogram.
        /// </summary>
        public IHistogram BlockProcessingTime => _blockProcessingTime;

        /// <summary>
        /// Gets the block size histogram.
        /// </summary>
        public IHistogram BlockSize => _blockSize;

        /// <summary>
        /// Gets the transaction count per block gauge.
        /// </summary>
        public IGauge BlockTransactionCount => _blockTransactionCount;

        /// <summary>
        /// Gets the number of transactions processed.
        /// </summary>
        public ICounter TransactionsProcessed => _transactionsProcessed;

        /// <summary>
        /// Gets the number of failed transactions.
        /// </summary>
        public ICounter TransactionsFailed => _transactionsFailed;

        /// <summary>
        /// Gets the transaction processing time histogram.
        /// </summary>
        public IHistogram TransactionProcessingTime => _transactionProcessingTime;

        /// <summary>
        /// Gets the memory pool size gauge.
        /// </summary>
        public IGauge MemPoolSize => _memPoolSize;

        /// <summary>
        /// Gets the verified transaction count in memory pool.
        /// </summary>
        public IGauge MemPoolVerifiedCount => _memPoolVerifiedCount;

        /// <summary>
        /// Gets the unverified transaction count in memory pool.
        /// </summary>
        public IGauge MemPoolUnverifiedCount => _memPoolUnverifiedCount;

        /// <summary>
        /// Gets the connected peers gauge.
        /// </summary>
        public IGauge ConnectedPeers => _connectedPeers;

        /// <summary>
        /// Gets the messages received counter.
        /// </summary>
        public ICounter MessagesReceived => _messagesReceived;

        /// <summary>
        /// Gets the messages sent counter.
        /// </summary>
        public ICounter MessagesSent => _messagesSent;

        /// <summary>
        /// Gets the message latency histogram.
        /// </summary>
        public IHistogram MessageLatency => _messageLatency;

        /// <summary>
        /// Gets the storage read time histogram.
        /// </summary>
        public IHistogram StorageReadTime => _storageReadTime;

        /// <summary>
        /// Gets the storage write time histogram.
        /// </summary>
        public IHistogram StorageWriteTime => _storageWriteTime;

        /// <summary>
        /// Gets the storage reads counter.
        /// </summary>
        public ICounter StorageReads => _storageReads;

        /// <summary>
        /// Gets the storage writes counter.
        /// </summary>
        public ICounter StorageWrites => _storageWrites;

        /// <summary>
        /// Initializes a new instance of the BlockchainMetrics class.
        /// </summary>
        /// <param name="provider">The metrics provider to use.</param>
        public BlockchainMetrics(IMetricsProvider provider)
        {
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));

            // Block metrics
            _blocksProcessed = provider.CreateCounter("neo_blocks_processed_total", "Total number of blocks processed");
            _blockHeight = provider.CreateGauge("neo_block_height", "Current block height");
            _blockProcessingTime = provider.CreateHistogram("neo_block_processing_seconds", "Block processing time in seconds",
                [0.001, 0.005, 0.01, 0.025, 0.05, 0.1, 0.25, 0.5, 1.0, 2.5, 5.0, 10.0]);
            _blockSize = provider.CreateHistogram("neo_block_size_bytes", "Block size in bytes",
                [1024, 4096, 16384, 65536, 262144, 1048576]);
            _blockTransactionCount = provider.CreateGauge("neo_block_transaction_count", "Number of transactions in the last block");

            // Transaction metrics
            _transactionsProcessed = provider.CreateCounter("neo_transactions_processed_total", "Total number of transactions processed");
            _transactionsFailed = provider.CreateCounter("neo_transactions_failed_total", "Total number of failed transactions");
            _transactionProcessingTime = provider.CreateHistogram("neo_transaction_processing_seconds", "Transaction processing time in seconds",
                [0.0001, 0.0005, 0.001, 0.005, 0.01, 0.025, 0.05, 0.1, 0.25, 0.5, 1.0]);
            _memPoolSize = provider.CreateGauge("neo_mempool_size", "Current memory pool size");
            _memPoolVerifiedCount = provider.CreateGauge("neo_mempool_verified_count", "Number of verified transactions in memory pool");
            _memPoolUnverifiedCount = provider.CreateGauge("neo_mempool_unverified_count", "Number of unverified transactions in memory pool");

            // Network metrics
            _connectedPeers = provider.CreateGauge("neo_connected_peers", "Number of connected peers");
            _messagesReceived = provider.CreateCounter("neo_messages_received_total", "Total number of P2P messages received");
            _messagesSent = provider.CreateCounter("neo_messages_sent_total", "Total number of P2P messages sent");
            _messageLatency = provider.CreateHistogram("neo_message_latency_seconds", "P2P message latency in seconds",
                [0.001, 0.005, 0.01, 0.025, 0.05, 0.1, 0.25, 0.5, 1.0]);

            // Storage metrics
            _storageReadTime = provider.CreateHistogram("neo_storage_read_seconds", "Storage read time in seconds",
                [0.00001, 0.0001, 0.001, 0.01, 0.1]);
            _storageWriteTime = provider.CreateHistogram("neo_storage_write_seconds", "Storage write time in seconds",
                [0.0001, 0.001, 0.01, 0.1, 1.0]);
            _storageReads = provider.CreateCounter("neo_storage_reads_total", "Total number of storage reads");
            _storageWrites = provider.CreateCounter("neo_storage_writes_total", "Total number of storage writes");
        }

        /// <summary>
        /// Records a block being processed.
        /// </summary>
        /// <param name="height">The block height.</param>
        /// <param name="processingTimeMs">Processing time in milliseconds.</param>
        /// <param name="sizeBytes">Block size in bytes.</param>
        /// <param name="transactionCount">Number of transactions in the block.</param>
        public void RecordBlockProcessed(uint height, double processingTimeMs, int sizeBytes, int transactionCount)
        {
            _blocksProcessed.Increment();
            _blockHeight.Set(height);
            _blockProcessingTime.Observe(processingTimeMs / 1000.0); // Convert to seconds
            _blockSize.Observe(sizeBytes);
            _blockTransactionCount.Set(transactionCount);
            _transactionsProcessed.Increment(transactionCount);
        }

        /// <summary>
        /// Records a transaction being processed.
        /// </summary>
        /// <param name="processingTimeMs">Processing time in milliseconds.</param>
        /// <param name="success">Whether the transaction succeeded.</param>
        public void RecordTransactionProcessed(double processingTimeMs, bool success)
        {
            _transactionProcessingTime.Observe(processingTimeMs / 1000.0);
            if (!success)
            {
                _transactionsFailed.Increment();
            }
        }

        /// <summary>
        /// Updates memory pool metrics.
        /// </summary>
        /// <param name="totalSize">Total memory pool size.</param>
        /// <param name="verifiedCount">Number of verified transactions.</param>
        /// <param name="unverifiedCount">Number of unverified transactions.</param>
        public void UpdateMemPoolMetrics(int totalSize, int verifiedCount, int unverifiedCount)
        {
            _memPoolSize.Set(totalSize);
            _memPoolVerifiedCount.Set(verifiedCount);
            _memPoolUnverifiedCount.Set(unverifiedCount);
        }

        /// <summary>
        /// Updates network metrics.
        /// </summary>
        /// <param name="connectedPeers">Number of connected peers.</param>
        public void UpdateNetworkMetrics(int connectedPeers)
        {
            _connectedPeers.Set(connectedPeers);
        }

        /// <summary>
        /// Records a P2P message being received.
        /// </summary>
        /// <param name="latencyMs">Message latency in milliseconds.</param>
        public void RecordMessageReceived(double latencyMs = 0)
        {
            _messagesReceived.Increment();
            if (latencyMs > 0)
            {
                _messageLatency.Observe(latencyMs / 1000.0);
            }
        }

        /// <summary>
        /// Records a P2P message being sent.
        /// </summary>
        public void RecordMessageSent()
        {
            _messagesSent.Increment();
        }

        /// <summary>
        /// Records a storage read operation.
        /// </summary>
        /// <param name="durationMs">Read duration in milliseconds.</param>
        public void RecordStorageRead(double durationMs)
        {
            _storageReads.Increment();
            _storageReadTime.Observe(durationMs / 1000.0);
        }

        /// <summary>
        /// Records a storage write operation.
        /// </summary>
        /// <param name="durationMs">Write duration in milliseconds.</param>
        public void RecordStorageWrite(double durationMs)
        {
            _storageWrites.Increment();
            _storageWriteTime.Observe(durationMs / 1000.0);
        }
    }
}
