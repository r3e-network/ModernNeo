// Copyright (C) 2015-2025 The Neo Project.
//
// IStorageKey.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using System;

namespace Neo.Core.Abstractions.Storage
{
    /// <summary>
    /// Represents a storage key used to identify data in the blockchain state.
    /// </summary>
    public interface IStorageKey : IEquatable<IStorageKey>
    {
        /// <summary>
        /// Gets the contract ID this key belongs to.
        /// </summary>
        int Id { get; }

        /// <summary>
        /// Gets the key data as a byte array.
        /// </summary>
        byte[] Key { get; }

        /// <summary>
        /// Converts the storage key to a byte array for serialization.
        /// </summary>
        /// <returns>The serialized key.</returns>
        byte[] ToArray();
    }
}
