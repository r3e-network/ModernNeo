// Copyright (C) 2015-2025 The Neo Project.
//
// NullExecutionMetrics.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Observability.Metrics;
using System;
using System.Runtime.CompilerServices;

namespace Neo.SmartContract
{
    /// <summary>
    /// A no-op implementation of <see cref="IExecutionMetrics"/> that does nothing.
    /// Used as default when no metrics collection is configured.
    /// All methods are aggressively inlined to eliminate call overhead in hot paths.
    /// </summary>
    public sealed class NullExecutionMetrics : IExecutionMetrics
    {
        /// <summary>
        /// Gets the singleton instance of the null execution metrics.
        /// </summary>
        public static NullExecutionMetrics Instance { get; } = new();

        private NullExecutionMetrics() { }

        /// <inheritdoc/>
        public IMetricsProvider MetricsProvider => NullMetricsProvider.Instance;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RecordExecutionStart(UInt160 contractHash, string method) { }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RecordExecutionComplete(UInt160 contractHash, string method, long gasConsumed, bool success, TimeSpan duration) { }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RecordSyscall(string syscallName, long gasConsumed) { }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RecordInstruction(byte opCode, long gasConsumed) { }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RecordStorageOperation(StorageOperationType operation, int keySize, int valueSize) { }
    }
}
