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
using System.Linq;
using System.Reflection;

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
        internal static void InvokeCommitting(NeoSystem system, Block block, DataCache snapshot, IReadOnlyList<ApplicationExecuted> applicationExecutedList)
        {
            var handler = Committing;
            if (handler == null) return;

            foreach (var d in handler.GetInvocationList().Cast<CommittingHandler>())
            {
                try
                {
                    d(system, block, snapshot, applicationExecutedList);
                }
                catch (Exception ex)
                {
                    HandleEventException(d.Target, ex);
                }
            }
        }

        /// <summary>
        /// Invokes the Committed event handlers with Plugin exception handling.
        /// </summary>
        internal static void InvokeCommitted(NeoSystem system, Block block)
        {
            var handler = Committed;
            if (handler == null) return;

            foreach (var d in handler.GetInvocationList().Cast<CommittedHandler>())
            {
                try
                {
                    d(system, block);
                }
                catch (Exception ex)
                {
                    HandleEventException(d.Target, ex);
                }
            }
        }

        /// <summary>
        /// Handles exceptions from event handlers, respecting Plugin exception policies.
        /// </summary>
        private static void HandleEventException(object? target, Exception ex)
        {
            Utility.Log(nameof(Blockchain), LogLevel.Error, ex);

            // Check if the target is a Plugin or belongs to a Plugin
            var plugin = target as Plugin ?? FindPluginForTarget(target);

            if (plugin != null)
            {
                switch (plugin.ExceptionPolicy)
                {
                    case UnhandledExceptionPolicy.StopNode:
                        throw ex;
                    case UnhandledExceptionPolicy.StopPlugin:
                        plugin.IsStopped = true;
                        break;
                    case UnhandledExceptionPolicy.Ignore:
                        break;
                    default:
                        throw new InvalidCastException($"The exception policy {plugin.ExceptionPolicy} is not valid.");
                }
            }
            else
            {
                // Non-plugin handler - rethrow the exception
                throw ex;
            }
        }

        /// <summary>
        /// Finds the Plugin instance that owns the target object.
        /// </summary>
        private static Plugin? FindPluginForTarget(object? target)
        {
            if (target == null) return null;

            // Check if target is a Plugin
            if (target is Plugin p) return p;

            // Check if target's declaring type is a Plugin
            var targetType = target.GetType();
            foreach (var plugin in Plugin.Plugins)
            {
                if (plugin.GetType() == targetType || targetType.IsAssignableTo(plugin.GetType()))
                {
                    return plugin;
                }
            }

            return null;
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
