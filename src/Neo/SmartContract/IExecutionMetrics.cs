// Copyright (C) 2015-2025 The Neo Project.
//
// IExecutionMetrics.cs file belongs to the neo project and is free
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
    /// Defines the interface for collecting execution metrics from the ApplicationEngine.
    /// Integrates with Neo.Observability for metrics collection.
    /// </summary>
    public interface IExecutionMetrics
    {
        /// <summary>
        /// Records the start of a contract execution.
        /// </summary>
        /// <param name="contractHash">The hash of the contract being executed.</param>
        /// <param name="method">The method being invoked.</param>
        void RecordExecutionStart(UInt160 contractHash, string method);

        /// <summary>
        /// Records the completion of a contract execution.
        /// </summary>
        /// <param name="contractHash">The hash of the contract.</param>
        /// <param name="method">The method that was invoked.</param>
        /// <param name="gasConsumed">The amount of GAS consumed.</param>
        /// <param name="success">Whether the execution was successful.</param>
        /// <param name="duration">The duration of the execution.</param>
        void RecordExecutionComplete(UInt160 contractHash, string method, long gasConsumed, bool success, TimeSpan duration);

        /// <summary>
        /// Records a syscall invocation.
        /// </summary>
        /// <param name="syscallName">The name of the syscall.</param>
        /// <param name="gasConsumed">The GAS consumed by the syscall.</param>
        void RecordSyscall(string syscallName, long gasConsumed);

        /// <summary>
        /// Records an instruction execution.
        /// </summary>
        /// <param name="opCode">The opcode of the instruction.</param>
        /// <param name="gasConsumed">The GAS consumed by the instruction.</param>
        void RecordInstruction(byte opCode, long gasConsumed);

        /// <summary>
        /// Records a storage operation.
        /// </summary>
        /// <param name="operation">The type of storage operation (read/write/delete).</param>
        /// <param name="keySize">The size of the key in bytes.</param>
        /// <param name="valueSize">The size of the value in bytes (0 for reads/deletes).</param>
        void RecordStorageOperation(StorageOperationType operation, int keySize, int valueSize);

        /// <summary>
        /// Gets the metrics provider used by this instance.
        /// </summary>
        IMetricsProvider MetricsProvider { get; }
    }

    /// <summary>
    /// Represents the type of storage operation.
    /// </summary>
    public enum StorageOperationType
    {
        /// <summary>
        /// A read operation.
        /// </summary>
        Read,

        /// <summary>
        /// A write operation.
        /// </summary>
        Write,

        /// <summary>
        /// A delete operation.
        /// </summary>
        Delete,

        /// <summary>
        /// A find/seek operation.
        /// </summary>
        Find
    }
}
