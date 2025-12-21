// Copyright (C) 2015-2025 The Neo Project.
//
// IStorageContextData.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

namespace Neo.Core.Interfaces
{
    /// <summary>
    /// Defines the data contract for a storage context without SmartContract dependencies.
    /// This interface allows lower layers to work with storage context data without depending
    /// on the full StorageContext implementation in Neo.SmartContract.
    /// </summary>
    public interface IStorageContextData
    {
        /// <summary>
        /// The id of the contract that owns the storage context.
        /// </summary>
        int Id { get; }

        /// <summary>
        /// Indicates whether the storage context is read-only.
        /// </summary>
        bool IsReadOnly { get; }
    }
}
