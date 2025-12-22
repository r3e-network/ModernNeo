// Copyright (C) 2015-2025 The Neo Project.
//
// IPoolItem.cs file belongs to the neo project and is free
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
    /// Represents an item in the transaction memory pool.
    /// Provides a dependency-free abstraction for pool items.
    /// </summary>
    public interface IPoolItem : IComparable<IPoolItem>
    {
        /// <summary>
        /// Gets the transaction data for this pool item.
        /// </summary>
        ITransactionData Transaction { get; }

        /// <summary>
        /// Gets the timestamp when the transaction was added to the pool.
        /// </summary>
        DateTime Timestamp { get; }

        /// <summary>
        /// Gets or sets the timestamp when this transaction was last broadcast.
        /// </summary>
        DateTime LastBroadcastTimestamp { get; set; }

        /// <summary>
        /// Gets the fee per byte for this transaction.
        /// </summary>
        long FeePerByte { get; }

        /// <summary>
        /// Gets the network fee for this transaction.
        /// </summary>
        long NetworkFee { get; }

        /// <summary>
        /// Gets whether this transaction has high priority.
        /// </summary>
        bool IsHighPriority { get; }
    }
}
