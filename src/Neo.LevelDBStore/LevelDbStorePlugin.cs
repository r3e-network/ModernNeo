// Copyright (C) 2015-2025 The Neo Project.
//
// LevelDbStorePlugin.cs file belongs to the neo project and is free
// software distributed under the MIT software license.

namespace Neo.Plugins;

public sealed class LevelDbStorePlugin : Plugin, IStoragePlugin
{
    public override string Name => "LevelDBStore";
}
