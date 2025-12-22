// Copyright (C) 2015-2025 The Neo Project.
//
// TransactionRemovedEventArgs.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Core.Interfaces;

namespace Neo.TxPool
{
    /// <summary>
    /// Represents the event arguments when a transaction is removed from the pool.
    /// </summary>
    public class TransactionRemovedEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the transaction that was removed.
        /// </summary>
        public ITransactionData Transaction { get; }

        /// <summary>
        /// Gets the reason why the transaction was removed.
        /// </summary>
        public TransactionRemovalReason Reason { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="TransactionRemovedEventArgs"/> class.
        /// </summary>
        /// <param name="transaction">The transaction that was removed.</param>
        /// <param name="reason">The reason for removal.</param>
        public TransactionRemovedEventArgs(ITransactionData transaction, TransactionRemovalReason reason)
        {
            Transaction = transaction;
            Reason = reason;
        }
    }

    /// <summary>
    /// Represents the reason why a transaction was removed from the memory pool.
    /// </summary>
    public enum TransactionRemovalReason : byte
    {
        /// <summary>
        /// The transaction was included in a block.
        /// </summary>
        Included = 0,

        /// <summary>
        /// The transaction expired (ValidUntilBlock exceeded).
        /// </summary>
        Expired = 1,

        /// <summary>
        /// The transaction was evicted due to capacity limits.
        /// </summary>
        CapacityExceeded = 2,

        /// <summary>
        /// The transaction failed re-verification.
        /// </summary>
        FailedReverification = 3,

        /// <summary>
        /// The transaction conflicts with another transaction.
        /// </summary>
        Conflict = 4,

        /// <summary>
        /// The transaction was explicitly removed.
        /// </summary>
        Explicit = 5
    }
}
