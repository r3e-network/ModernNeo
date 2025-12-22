// Copyright (C) 2015-2025 The Neo Project.
//
// RocksDbStoreProvider.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using System;

namespace Neo.Persistence.Providers
{
    /// <summary>
    /// A provider for creating RocksDB-based stores.
    /// </summary>
    public class RocksDbStoreProvider : IStoreProvider
    {
        private readonly RocksDbStoreOptions _options;

        /// <inheritdoc/>
        public string Name => "RocksDBStore";

        /// <summary>
        /// Initializes a new instance of the <see cref="RocksDbStoreProvider"/> class with default options.
        /// </summary>
        public RocksDbStoreProvider()
            : this(RocksDbStoreOptions.Default)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="RocksDbStoreProvider"/> class with custom options.
        /// </summary>
        /// <param name="options">The store configuration options.</param>
        public RocksDbStoreProvider(RocksDbStoreOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);
            _options = options;
        }

        /// <inheritdoc/>
        public IStore GetStore(string? path)
        {
            ArgumentNullException.ThrowIfNull(path);
            return new RocksDbStore(path, _options);
        }
    }
}
