// Copyright (C) 2015-2025 The Neo Project.
//
// IBlockEventHandler.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Core.Interfaces;

namespace Neo.Core.Events
{
    /// <summary>
    /// Dependency-free interface for handling block-related events.
    /// </summary>
    public interface IBlockEventHandler
    {
        /// <summary>
        /// Called when a block is about to be committed.
        /// </summary>
        /// <param name="block">The block data.</param>
        /// <param name="index">The block index.</param>
        void OnBlockCommitting(IBlockData block, uint index);

        /// <summary>
        /// Called after a block has been committed.
        /// </summary>
        /// <param name="block">The block data.</param>
        /// <param name="index">The block index.</param>
        void OnBlockCommitted(IBlockData block, uint index);
    }

    /// <summary>
    /// Dependency-free interface for handling transaction-related events.
    /// </summary>
    public interface ITransactionEventHandler
    {
        /// <summary>
        /// Called when a transaction is added to the memory pool.
        /// </summary>
        /// <param name="transaction">The transaction data.</param>
        void OnTransactionAdded(ITransactionData transaction);

        /// <summary>
        /// Called when a transaction is removed from the memory pool.
        /// </summary>
        /// <param name="transaction">The transaction data.</param>
        /// <param name="reason">The removal reason.</param>
        void OnTransactionRemoved(ITransactionData transaction, TransactionRemovalReason reason);
    }

    /// <summary>
    /// Reasons for transaction removal from memory pool.
    /// </summary>
    public enum TransactionRemovalReason
    {
        /// <summary>
        /// Transaction was included in a block.
        /// </summary>
        Included,

        /// <summary>
        /// Transaction expired (ValidUntilBlock passed).
        /// </summary>
        Expired,

        /// <summary>
        /// Transaction was replaced by a higher fee transaction.
        /// </summary>
        Replaced,

        /// <summary>
        /// Transaction conflicts with another transaction.
        /// </summary>
        Conflict,

        /// <summary>
        /// Memory pool capacity exceeded.
        /// </summary>
        CapacityExceeded,

        /// <summary>
        /// Transaction verification failed.
        /// </summary>
        VerificationFailed,

        /// <summary>
        /// Unknown reason.
        /// </summary>
        Unknown
    }

    /// <summary>
    /// Dependency-free interface for handling notification events.
    /// </summary>
    public interface INotificationEventHandler
    {
        /// <summary>
        /// Called when a smart contract emits a notification.
        /// </summary>
        /// <param name="scriptHash">The contract script hash (20 bytes).</param>
        /// <param name="eventName">The event name.</param>
        /// <param name="state">The notification state data.</param>
        void OnNotification(byte[] scriptHash, string eventName, object? state);
    }

    /// <summary>
    /// Dependency-free interface for handling log events.
    /// </summary>
    public interface ILogEventHandler
    {
        /// <summary>
        /// Called when a smart contract emits a log message.
        /// </summary>
        /// <param name="scriptHash">The contract script hash (20 bytes).</param>
        /// <param name="message">The log message.</param>
        void OnLog(byte[] scriptHash, string message);
    }
}
