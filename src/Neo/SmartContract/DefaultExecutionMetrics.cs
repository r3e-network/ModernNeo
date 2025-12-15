// Copyright (C) 2015-2025 The Neo Project.
//
// DefaultExecutionMetrics.cs file belongs to the neo project and is free
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
    /// Default implementation of execution metrics using DiagnosticsMetricsProvider.
    /// Collects metrics for smart contract execution, syscalls, instructions, and storage operations.
    /// </summary>
    public sealed class DefaultExecutionMetrics : IExecutionMetrics
    {
        private readonly ICounter _executionCount;
        private readonly ICounter _executionSuccessCount;
        private readonly ICounter _executionFailureCount;
        private readonly IHistogram _executionDuration;
        private readonly IHistogram _gasConsumed;
        private readonly ICounter _syscallCount;
        private readonly ICounter _instructionCount;
        private readonly ICounter _storageReadCount;
        private readonly ICounter _storageWriteCount;
        private readonly ICounter _storageDeleteCount;
        private readonly ICounter _storageFindCount;
        private readonly IHistogram _storageKeySize;
        private readonly IHistogram _storageValueSize;

        /// <inheritdoc/>
        public IMetricsProvider MetricsProvider { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="DefaultExecutionMetrics"/> class.
        /// </summary>
        /// <param name="metricsProvider">The metrics provider to use. If null, creates a new DiagnosticsMetricsProvider.</param>
        public DefaultExecutionMetrics(IMetricsProvider? metricsProvider = null)
        {
            MetricsProvider = metricsProvider ?? new DiagnosticsMetricsProvider("Neo.SmartContract");

            // Execution metrics
            _executionCount = MetricsProvider.CreateCounter("neo_contract_executions_total", "Total number of contract executions");
            _executionSuccessCount = MetricsProvider.CreateCounter("neo_contract_executions_success_total", "Total number of successful contract executions");
            _executionFailureCount = MetricsProvider.CreateCounter("neo_contract_executions_failure_total", "Total number of failed contract executions");
            _executionDuration = MetricsProvider.CreateHistogram("neo_contract_execution_duration_ms", "Contract execution duration in milliseconds");
            _gasConsumed = MetricsProvider.CreateHistogram("neo_contract_gas_consumed", "GAS consumed per contract execution");

            // Syscall metrics
            _syscallCount = MetricsProvider.CreateCounter("neo_syscalls_total", "Total number of syscall invocations");

            // Instruction metrics
            _instructionCount = MetricsProvider.CreateCounter("neo_instructions_total", "Total number of instructions executed");

            // Storage metrics
            _storageReadCount = MetricsProvider.CreateCounter("neo_storage_reads_total", "Total number of storage read operations");
            _storageWriteCount = MetricsProvider.CreateCounter("neo_storage_writes_total", "Total number of storage write operations");
            _storageDeleteCount = MetricsProvider.CreateCounter("neo_storage_deletes_total", "Total number of storage delete operations");
            _storageFindCount = MetricsProvider.CreateCounter("neo_storage_finds_total", "Total number of storage find operations");
            _storageKeySize = MetricsProvider.CreateHistogram("neo_storage_key_size_bytes", "Storage key size in bytes");
            _storageValueSize = MetricsProvider.CreateHistogram("neo_storage_value_size_bytes", "Storage value size in bytes");
        }

        /// <inheritdoc/>
        public void RecordExecutionStart(UInt160 contractHash, string method)
        {
            _executionCount.Increment();
        }

        /// <inheritdoc/>
        public void RecordExecutionComplete(UInt160 contractHash, string method, long gasConsumed, bool success, TimeSpan duration)
        {
            if (success)
                _executionSuccessCount.Increment();
            else
                _executionFailureCount.Increment();

            _executionDuration.Observe(duration.TotalMilliseconds);
            _gasConsumed.Observe(gasConsumed);
        }

        /// <inheritdoc/>
        public void RecordSyscall(string syscallName, long gasConsumed)
        {
            _syscallCount.Increment();
        }

        /// <inheritdoc/>
        public void RecordInstruction(byte opCode, long gasConsumed)
        {
            _instructionCount.Increment();
        }

        /// <inheritdoc/>
        public void RecordStorageOperation(StorageOperationType operation, int keySize, int valueSize)
        {
            switch (operation)
            {
                case StorageOperationType.Read:
                    _storageReadCount.Increment();
                    break;
                case StorageOperationType.Write:
                    _storageWriteCount.Increment();
                    _storageValueSize.Observe(valueSize);
                    break;
                case StorageOperationType.Delete:
                    _storageDeleteCount.Increment();
                    break;
                case StorageOperationType.Find:
                    _storageFindCount.Increment();
                    break;
            }

            _storageKeySize.Observe(keySize);
        }
    }
}
