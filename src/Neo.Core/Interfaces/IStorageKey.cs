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

namespace Neo.Core.Interfaces
{
    /// <summary>
    /// Dependency-free interface for storage keys.
    /// Allows storage operations without depending on concrete SmartContract types.
    /// </summary>
    public interface IStorageKey
    {
        /// <summary>
        /// Gets the contract ID.
        /// </summary>
        int Id { get; }

        /// <summary>
        /// Gets the key data (excluding contract ID prefix).
        /// </summary>
        ReadOnlyMemory<byte> Key { get; }

        /// <summary>
        /// Gets the total length of the serialized key.
        /// </summary>
        int Length { get; }

        /// <summary>
        /// Serializes the storage key to a byte array.
        /// </summary>
        byte[] ToArray();
    }

    /// <summary>
    /// Dependency-free interface for storage items/values.
    /// Allows storage operations without depending on VM types.
    /// </summary>
    public interface IStorageItem
    {
        /// <summary>
        /// Gets the raw byte value of the storage item.
        /// </summary>
        ReadOnlyMemory<byte> Value { get; }

        /// <summary>
        /// Gets the serialized size of the storage item.
        /// </summary>
        int Size { get; }

        /// <summary>
        /// Serializes the storage item to a byte array.
        /// </summary>
        byte[] ToArray();
    }

    /// <summary>
    /// Factory interface for creating storage keys and items.
    /// </summary>
    public interface IStorageFactory
    {
        /// <summary>
        /// Creates a storage key from contract ID and key data.
        /// </summary>
        IStorageKey CreateKey(int contractId, ReadOnlySpan<byte> key);

        /// <summary>
        /// Creates a storage key from raw bytes.
        /// </summary>
        IStorageKey CreateKey(ReadOnlySpan<byte> data);

        /// <summary>
        /// Creates a storage item from raw bytes.
        /// </summary>
        IStorageItem CreateItem(ReadOnlySpan<byte> value);
    }
}
