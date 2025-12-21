// Copyright (C) 2015-2025 The Neo Project.
//
// LevelDbStoreProvider.cs file belongs to the neo project and is free
// software distributed under the MIT software license.

using System;

namespace Neo.Persistence.Providers;

/// <summary>
/// A provider for creating LevelDB-based stores.
/// </summary>
public class LevelDbStoreProvider : IStoreProvider
{
    /// <inheritdoc/>
    public string Name => "LevelDBStore";

    /// <inheritdoc/>
    public IStore GetStore(string? path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return new LevelDbStore(path);
    }
}
