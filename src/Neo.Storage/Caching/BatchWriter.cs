// Copyright (C) 2015-2025 The Neo Project.
//
// BatchWriter.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Neo.Persistence.Caching
{
    /// <summary>
    /// Batches write operations for improved throughput.
    /// Accumulates writes and flushes them in batches to reduce I/O overhead.
    /// </summary>
    public sealed class BatchWriter : IAsyncDisposable
    {
        private readonly ConcurrentQueue<WriteOperation> _pendingWrites = new();
        private readonly IStore _store;
        private readonly BatchWriterOptions _options;
        private readonly Timer _flushTimer;
        private readonly SemaphoreSlim _flushLock = new(1, 1);
        private readonly CancellationTokenSource _cts = new();
        private int _pendingCount;
        private long _totalWrites;
        private long _totalBatches;
        private bool _disposed;

        /// <summary>
        /// Represents a pending write operation.
        /// </summary>
        private readonly struct WriteOperation
        {
            public byte[] Key { get; init; }
            public byte[]? Value { get; init; } // null = delete
            public TaskCompletionSource<bool>? Completion { get; init; }
        }

        /// <summary>
        /// Gets the number of pending write operations.
        /// </summary>
        public int PendingCount => _pendingCount;

        /// <summary>
        /// Gets the total number of writes processed.
        /// </summary>
        public long TotalWrites => Interlocked.Read(ref _totalWrites);

        /// <summary>
        /// Gets the total number of batches flushed.
        /// </summary>
        public long TotalBatches => Interlocked.Read(ref _totalBatches);

        /// <summary>
        /// Event raised when a batch is flushed.
        /// </summary>
        public event Action<int>? OnBatchFlushed;

        /// <summary>
        /// Creates a new batch writer for the specified store.
        /// </summary>
        public BatchWriter(IStore store, BatchWriterOptions? options = null)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _options = options ?? new BatchWriterOptions();

            _flushTimer = new Timer(
                _ => _ = FlushAsync(),
                null,
                _options.FlushInterval,
                _options.FlushInterval);
        }

        /// <summary>
        /// Queues a write operation.
        /// </summary>
        public void Put(byte[] key, byte[] value)
        {
            ThrowIfDisposed();

            _pendingWrites.Enqueue(new WriteOperation
            {
                Key = key,
                Value = value
            });

            var count = Interlocked.Increment(ref _pendingCount);

            // Trigger flush if batch size reached
            if (count >= _options.BatchSize)
            {
                _ = FlushAsync();
            }
        }

        /// <summary>
        /// Queues a write operation and waits for it to be flushed.
        /// </summary>
        public async Task PutAsync(byte[] key, byte[] value, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            using var registration = cancellationToken.Register(() => tcs.TrySetCanceled());

            _pendingWrites.Enqueue(new WriteOperation
            {
                Key = key,
                Value = value,
                Completion = tcs
            });

            var count = Interlocked.Increment(ref _pendingCount);

            if (count >= _options.BatchSize)
            {
                _ = FlushAsync();
            }

            await tcs.Task.ConfigureAwait(false);
        }

        /// <summary>
        /// Queues a delete operation.
        /// </summary>
        public void Delete(byte[] key)
        {
            ThrowIfDisposed();

            _pendingWrites.Enqueue(new WriteOperation
            {
                Key = key,
                Value = null // null indicates delete
            });

            var count = Interlocked.Increment(ref _pendingCount);

            if (count >= _options.BatchSize)
            {
                _ = FlushAsync();
            }
        }

        /// <summary>
        /// Queues a delete operation and waits for it to be flushed.
        /// </summary>
        public async Task DeleteAsync(byte[] key, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            using var registration = cancellationToken.Register(() => tcs.TrySetCanceled());

            _pendingWrites.Enqueue(new WriteOperation
            {
                Key = key,
                Value = null,
                Completion = tcs
            });

            var count = Interlocked.Increment(ref _pendingCount);

            if (count >= _options.BatchSize)
            {
                _ = FlushAsync();
            }

            await tcs.Task.ConfigureAwait(false);
        }

        /// <summary>
        /// Flushes all pending writes to the store.
        /// </summary>
        public async Task FlushAsync(CancellationToken cancellationToken = default)
        {
            if (_disposed || _pendingCount == 0) return;

            if (!await _flushLock.WaitAsync(0, cancellationToken).ConfigureAwait(false))
                return; // Another flush is in progress

            try
            {
                var operations = new List<WriteOperation>();
                var completions = new List<TaskCompletionSource<bool>>();

                // Dequeue all pending operations
                while (_pendingWrites.TryDequeue(out var op))
                {
                    operations.Add(op);
                    if (op.Completion != null)
                    {
                        completions.Add(op.Completion);
                    }
                    Interlocked.Decrement(ref _pendingCount);
                }

                if (operations.Count == 0) return;

                try
                {
                    // Get a snapshot and apply all operations
                    using var snapshot = _store.GetSnapshot();

                    foreach (var op in operations)
                    {
                        if (op.Value == null)
                        {
                            snapshot.Delete(op.Key);
                        }
                        else
                        {
                            snapshot.Put(op.Key, op.Value);
                        }
                    }

                    snapshot.Commit();

                    Interlocked.Add(ref _totalWrites, operations.Count);
                    Interlocked.Increment(ref _totalBatches);

                    // Signal completions
                    foreach (var tcs in completions)
                    {
                        tcs.TrySetResult(true);
                    }

                    OnBatchFlushed?.Invoke(operations.Count);
                }
                catch (Exception ex)
                {
                    // Signal failures
                    foreach (var tcs in completions)
                    {
                        tcs.TrySetException(ex);
                    }
                    throw;
                }
            }
            finally
            {
                _flushLock.Release();
            }
        }

        /// <summary>
        /// Gets batch writer statistics.
        /// </summary>
        public BatchWriterStatistics GetStatistics()
        {
            return new BatchWriterStatistics
            {
                PendingCount = PendingCount,
                TotalWrites = TotalWrites,
                TotalBatches = TotalBatches,
                AverageBatchSize = TotalBatches > 0 ? (double)TotalWrites / TotalBatches : 0
            };
        }

        private void ThrowIfDisposed()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
        }

        public async ValueTask DisposeAsync()
        {
            if (_disposed) return;

            await _cts.CancelAsync().ConfigureAwait(false);
            await _flushTimer.DisposeAsync().ConfigureAwait(false);

            // Final flush before marking as disposed
            try
            {
                await FlushAsync().ConfigureAwait(false);
            }
            catch
            {
                // Ignore errors during disposal
            }

            _disposed = true;
            _flushLock.Dispose();
            _cts.Dispose();
        }
    }

    /// <summary>
    /// Configuration options for batch writer.
    /// </summary>
    public sealed class BatchWriterOptions
    {
        /// <summary>
        /// Maximum number of operations before triggering a flush. Default: 1000.
        /// </summary>
        public int BatchSize { get; set; } = 1000;

        /// <summary>
        /// Maximum time between flushes. Default: 100ms.
        /// </summary>
        public TimeSpan FlushInterval { get; set; } = TimeSpan.FromMilliseconds(100);
    }

    /// <summary>
    /// Batch writer statistics for monitoring.
    /// </summary>
    public readonly struct BatchWriterStatistics
    {
        public int PendingCount { get; init; }
        public long TotalWrites { get; init; }
        public long TotalBatches { get; init; }
        public double AverageBatchSize { get; init; }
    }
}
