// Copyright (C) 2015-2025 The Neo Project.
//
// IStorageItem.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

namespace Neo.Core.Abstractions.Storage
{
    /// <summary>
    /// Represents a storage item (value) in the blockchain state.
    /// </summary>
    public interface IStorageItem
    {
        /// <summary>
        /// Gets the value data as a byte array.
        /// </summary>
        byte[] Value { get; }

        /// <summary>
        /// Gets whether this item is a constant (cannot be modified).
        /// </summary>
        bool IsConstant { get; }

        /// <summary>
        /// Creates a clone of this storage item.
        /// </summary>
        /// <returns>A new storage item clone.</returns>
        IStorageItem Clone();
    }
}
