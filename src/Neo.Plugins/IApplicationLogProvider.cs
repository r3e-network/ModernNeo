// Copyright (C) 2015-2025 The Neo Project.
//
// IApplicationLogProvider.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo;
using Neo.SmartContract;

namespace Neo.Plugins
{
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
}
