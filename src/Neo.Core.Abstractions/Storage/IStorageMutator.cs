// Copyright (C) 2015-2025 The Neo Project.
//
// IStorageMutator.cs file belongs to the neo project and is free
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
    /// Provides write operations for storage state.
    /// This interface enables modifying storage data in a transactional manner.
    /// </summary>
    public interface IStorageMutator : IStorageSnapshot
    {
        /// <summary>
        /// Adds or updates a value in storage.
        /// </summary>
        /// <param name="key">The storage key.</param>
        /// <param name="value">The storage item to store.</param>
        void Put(IStorageKey key, IStorageItem value);

        /// <summary>
        /// Deletes a value from storage.
        /// </summary>
        /// <param name="key">The storage key to delete.</param>
        void Delete(IStorageKey key);

        /// <summary>
        /// Commits all pending changes to the underlying storage.
        /// </summary>
        void Commit();
    }
}
