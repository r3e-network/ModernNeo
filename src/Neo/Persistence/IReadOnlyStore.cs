// Copyright (C) 2015-2025 The Neo Project.
//
// IReadOnlyStore.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.SmartContract;

namespace Neo.Persistence
{
    /// <summary>
    /// This interface provides methods to read from the database using Neo storage types.
    /// </summary>
    /// <remarks>
    /// The generic <see cref="IReadOnlyStore{TKey, TValue}"/> interface is defined in Neo.Storage.
    /// </remarks>
    public interface IReadOnlyStore : IReadOnlyStore<StorageKey, StorageItem> { }
}
