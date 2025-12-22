// Copyright (C) 2015-2025 The Neo Project.
//
// IInventory.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

namespace Neo.Core.Abstractions
{
    /// <summary>
    /// Represents an inventory item that can be relayed across the network.
    /// </summary>
    public interface IInventory
    {
        /// <summary>
        /// Gets the hash of this inventory item.
        /// </summary>
        byte[] Hash { get; }

        /// <summary>
        /// Gets the type of this inventory item.
        /// </summary>
        InventoryType Type { get; }
    }

    /// <summary>
    /// Defines the types of inventory items in the Neo network.
    /// </summary>
    public enum InventoryType : byte
    {
        /// <summary>
        /// A transaction.
        /// </summary>
        Transaction = 0x2b,

        /// <summary>
        /// A block.
        /// </summary>
        Block = 0x2c,

        /// <summary>
        /// An extensible payload (consensus, state root, etc.).
        /// </summary>
        Extensible = 0x2e
    }
}
