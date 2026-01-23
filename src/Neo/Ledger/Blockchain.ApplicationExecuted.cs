// Copyright (C) 2015-2025 The Neo Project.
//
// Blockchain.ApplicationExecuted.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.Plugins;
using Neo.SmartContract;
using Neo.VM;
using Neo.VM.Types;
using System;
using System.Collections.Generic;

namespace Neo.Ledger
{
    /// <summary>
    /// Handler for blockchain committing events.
    /// </summary>
    public delegate void CommittingHandler(NeoSystem system, Block block, DataCache snapshot, IReadOnlyList<Blockchain.ApplicationExecuted> applicationExecutedList);

    /// <summary>
    /// Handler for blockchain committed events.
    /// </summary>
    public delegate void CommittedHandler(NeoSystem system, Block block);

    /// <summary>
    /// Blockchain-related types and events.
    /// </summary>
    public static class Blockchain
    {
        /// <summary>
        /// Triggered when a new block is committing, and the state is still in the cache.
        /// </summary>
        public static event CommittingHandler? Committing;

        /// <summary>
        /// Triggered when a new block has been committed.
        /// </summary>
        public static event CommittedHandler? Committed;

        /// <summary>
        /// Invokes the Committing event handlers with Plugin exception handling.
        /// </summary>
        public static void InvokeCommitting(NeoSystem system, Block block, DataCache snapshot, IReadOnlyList<ApplicationExecuted> applicationExecutedList)
        {
            InvokeHandlers(Committing?.GetInvocationList(), h => ((CommittingHandler)h)(system, block, snapshot, applicationExecutedList));
        }

        /// <summary>
        /// Invokes the Committed event handlers with Plugin exception handling.
        /// </summary>
        public static void InvokeCommitted(NeoSystem system, Block block)
        {
            InvokeHandlers(Committed?.GetInvocationList(), h => ((CommittedHandler)h)(system, block));
        }

        private static void InvokeHandlers(Delegate[]? handlers, Action<Delegate> handlerAction)
        {
            if (handlers == null) return;

            foreach (var handler in handlers)
            {
                try
                {
                    if (handler.Target is Plugin { IsStopped: true })
                    {
                        continue;
                    }

                    handlerAction(handler);
                }
                catch (Exception ex) when (handler.Target is Plugin plugin)
                {
                    var cause = ex.InnerException ?? ex;
                    Utility.Log(nameof(plugin.Name), LogLevel.Error,
                        $"{plugin.Name} exception: {cause.Message}{Environment.NewLine}{cause.StackTrace}");
                    switch (plugin.ExceptionPolicy)
                    {
                        case UnhandledExceptionPolicy.StopNode:
                            throw;
                        case UnhandledExceptionPolicy.StopPlugin:
                            plugin.IsStopped = true;
                            break;
                        case UnhandledExceptionPolicy.Ignore:
                            break;
                        default:
                            throw new InvalidCastException($"The exception policy {plugin.ExceptionPolicy} is not valid.");
                    }
                }
            }
        }

        /// <summary>
        /// Sent by the blockchain when a smart contract is executed.
        /// </summary>
        public class ApplicationExecuted
        {
            /// <summary>
            /// The transaction that contains the executed script. This field could be <see langword="null"/> if the contract is invoked by system.
            /// </summary>
            public Transaction? Transaction { get; }

            /// <summary>
            /// The trigger of the execution.
            /// </summary>
            public TriggerType Trigger { get; }

            /// <summary>
            /// The state of the virtual machine after the contract is executed.
            /// </summary>
            public VMState VMState { get; }

            /// <summary>
            /// The exception that caused the execution to terminate abnormally. This field could be <see langword="null"/> if the execution ends normally.
            /// </summary>
            public Exception? Exception { get; }

            /// <summary>
            /// GAS spent to execute.
            /// </summary>
            public long GasConsumed { get; }

            /// <summary>
            /// Items on the stack of the virtual machine after execution.
            /// </summary>
            public StackItem[] Stack { get; }

            /// <summary>
            /// The notifications sent during the execution.
            /// </summary>
            public NotifyEventArgs[] Notifications { get; }

            public ApplicationExecuted(ApplicationEngine engine)
            {
                Transaction = engine.ScriptContainer as Transaction;
                Trigger = engine.Trigger;
                VMState = engine.State;
                GasConsumed = engine.FeeConsumed;
                Exception = engine.FaultException;
                Stack = [.. engine.ResultStack];
                Notifications = [.. engine.Notifications];
            }
        }

        /// <summary>
        /// Sent by the blockchain when a block is persisted.
        /// </summary>
        /// <param name="Block">The block that is persisted.</param>
        public record PersistCompleted(Block Block);

        /// <summary>
        /// Sent to the blockchain when importing blocks.
        /// </summary>
        /// <param name="Blocks">The blocks to be imported.</param>
        /// <param name="Verify">Indicates whether the blocks need to be verified when importing.</param>
        public record Import(System.Collections.Generic.IEnumerable<Block> Blocks, bool Verify = true);

        /// <summary>
        /// Sent by the blockchain when the import is complete.
        /// </summary>
        public record ImportCompleted;

        /// <summary>
        /// Sent to the blockchain when the consensus is filling the memory pool.
        /// </summary>
        /// <param name="Transactions">The transactions to be sent.</param>
        public record FillMemoryPool(System.Collections.Generic.IEnumerable<Transaction> Transactions);

        /// <summary>
        /// Sent by the blockchain when the memory pool is filled.
        /// </summary>
        public record FillCompleted;

        /// <summary>
        /// Sent to the blockchain when inventories need to be re-verified.
        /// </summary>
        /// <param name="Inventories">The inventories to be re-verified.</param>
        public record Reverify(System.Collections.Generic.IReadOnlyList<IInventory> Inventories);

        /// <summary>
        /// Sent by the blockchain when an inventory is relayed.
        /// </summary>
        /// <param name="Inventory">The inventory that is relayed.</param>
        /// <param name="Result">The result.</param>
        public record RelayResult(IInventory Inventory, VerifyResult Result);

        /// <summary>
        /// Internal initialization message.
        /// </summary>
        internal record Initialize;
    }
}
