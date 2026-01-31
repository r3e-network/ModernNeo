// Copyright (C) 2015-2025 The Neo Project.
//
// StorageOptions.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Persistence;
using Neo.Persistence.Providers;

namespace Neo.Orleans.Options
{
    /// <summary>
    /// Storage-related configuration options for Neo Orleans.
    /// </summary>
    public sealed class StorageOptions
    {
        /// <summary>
        /// Storage provider name (MemoryStore, LevelDB, RocksDB).
        /// Default: MemoryStore.
        /// </summary>
        public string Engine { get; set; } = nameof(MemoryStore);

        /// <summary>
        /// Storage path template for Neo system state.
        /// Default: Data_{0}.
        /// </summary>
        public string? Path { get; set; } = "Data_{0}";

        /// <summary>
        /// Connection string for persistent storage (when UseMemory is false).
        /// </summary>
        public string? ConnectionString { get; set; }

        /// <summary>
        /// Whether to use in-memory storage (for development/testing).
        /// Default: true
        /// </summary>
        public bool UseMemory { get; set; } = true;
    }
}
