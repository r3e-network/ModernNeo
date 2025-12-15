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

namespace Neo.SmartContract
{
    /// <summary>
    /// A no-op implementation of <see cref="IExecutionMetrics"/> that does nothing.
    /// Used as default when no metrics collection is configured.
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
        public void RecordExecutionStart(UInt160 contractHash, string method) { }

        /// <inheritdoc/>
        public void RecordExecutionComplete(UInt160 contractHash, string method, long gasConsumed, bool success, TimeSpan duration) { }

        /// <inheritdoc/>
        public void RecordSyscall(string syscallName, long gasConsumed) { }

        /// <inheritdoc/>
        public void RecordInstruction(byte opCode, long gasConsumed) { }

        /// <inheritdoc/>
        public void RecordStorageOperation(StorageOperationType operation, int keySize, int valueSize) { }
    }
}
