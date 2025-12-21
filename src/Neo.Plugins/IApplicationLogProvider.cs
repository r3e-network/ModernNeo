// Copyright (C) 2015-2025 The Neo Project.
//
// IApplicationLogProvider.cs file belongs to the neo project and is free
// software distributed under the MIT software license.

using Neo.SmartContract;
using Neo;

namespace Neo.Plugins;

/// <summary>
/// Provides access to persisted application execution logs.
/// </summary>
public interface IApplicationLogProvider
{
    /// <summary>
    /// Gets the application log JSON for the specified hash.
    /// </summary>
    /// <param name="hash">The transaction or block hash.</param>
    /// <param name="trigger">Optional trigger filter.</param>
    /// <returns>The JSON payload, or null if not found.</returns>
    string? GetApplicationLog(UInt256 hash, TriggerType? trigger);
}
