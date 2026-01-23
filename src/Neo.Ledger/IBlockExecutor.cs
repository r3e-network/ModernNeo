// Copyright (C) 2015-2025 The Neo Project.
//
// IBlockExecutor.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Core.Interfaces;
using Neo.Network.P2P.Payloads;

namespace Neo.Ledger
{
    /// <summary>
    /// Abstraction for block transaction execution strategies.
    /// Allows switching between sequential and parallel execution modes.
    /// </summary>
    public interface IBlockExecutor
    {
        /// <summary>
        /// Gets whether parallel execution is enabled.
        /// </summary>
        bool IsParallelEnabled { get; }

        /// <summary>
        /// Executes all transactions in a block.
        /// </summary>
        /// <param name="transactions">The transaction states to execute.</param>
        /// <param name="snapshot">The base snapshot for execution.</param>
        /// <param name="block">The block being persisted.</param>
        /// <param name="settings">Protocol settings.</param>
        /// <param name="onExecuted">Callback for each executed transaction.</param>
        /// <returns>List of execution results.</returns>
        IReadOnlyList<IBlockExecutionResult> Execute(
            IReadOnlyList<object> transactions,
            object snapshot,
            Block block,
            object settings,
            Action<object>? onExecuted = null);

        /// <summary>
        /// Gets the execution statistics from the last execution.
        /// </summary>
        IExecutionStatistics? LastStatistics { get; }
    }

    /// <summary>
    /// Result of executing a transaction within a block.
    /// </summary>
    public interface IBlockExecutionResult
    {
        /// <summary>
        /// The transaction hash.
        /// </summary>
        byte[] TransactionHash { get; }

        /// <summary>
        /// The final VM state (HALT or FAULT).
        /// </summary>
        byte State { get; }

        /// <summary>
        /// Whether to commit this transaction's changes.
        /// </summary>
        bool ShouldCommit { get; }

        /// <summary>
        /// The application executed event data.
        /// </summary>
        object? ApplicationExecuted { get; }
    }
}
