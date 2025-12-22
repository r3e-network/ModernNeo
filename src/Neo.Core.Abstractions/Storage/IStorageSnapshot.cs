// Copyright (C) 2015-2025 The Neo Project.
//
// IStorageSnapshot.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using System;
using System.Collections.Generic;

namespace Neo.Core.Abstractions.Storage
{
    /// <summary>
    /// Represents an immutable snapshot of storage state.
    /// This interface provides read-only access to storage data.
    /// </summary>
    public interface IStorageSnapshot : IDisposable
    {
        /// <summary>
        /// Gets a value from storage by key.
        /// </summary>
        /// <param name="key">The storage key.</param>
        /// <returns>The storage item, or null if not found.</returns>
        IStorageItem? TryGet(IStorageKey key);

        /// <summary>
        /// Checks if a key exists in storage.
        /// </summary>
        /// <param name="key">The storage key.</param>
        /// <returns>True if the key exists, false otherwise.</returns>
        bool Contains(IStorageKey key);

        /// <summary>
        /// Finds all entries matching a key prefix.
        /// </summary>
        /// <param name="prefix">The key prefix to search for.</param>
        /// <returns>An enumerable of matching key-value pairs.</returns>
        IEnumerable<(IStorageKey Key, IStorageItem Value)> Find(byte[] prefix);

        /// <summary>
        /// Creates a clone of this snapshot for isolated modifications.
        /// </summary>
        /// <returns>A new snapshot clone.</returns>
        IStorageSnapshot Clone();
    }
}
