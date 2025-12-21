// Copyright (C) 2015-2025 The Neo Project.
//
// INep17Tracker.cs file belongs to the neo project and is free
// software distributed under the MIT software license.

using Neo;

namespace Neo.Plugins;

public interface INep17Tracker
{
    string? GetNep17Balances(UInt160 account);

    string? GetNep17Transfers(UInt160 account, ulong from, ulong to);
}
