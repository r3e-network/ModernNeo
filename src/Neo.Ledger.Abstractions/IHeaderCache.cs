// Copyright (C) 2015-2025 The Neo Project.
//
// IHeaderCache.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Core.Abstractions;
using System.Collections.Generic;

namespace Neo.Ledger.Abstractions
{
    /// <summary>
    /// Provides caching for block headers during synchronization.
    /// </summary>
    public interface IHeaderCache
    {
        /// <summary>
        /// Gets the number of headers in the cache.
        /// </summary>
        int Count { get; }

        /// <summary>
        /// Gets whether the cache is full.
        /// </summary>
        bool Full { get; }

        /// <summary>
        /// Gets the last header in the cache.
        /// </summary>
        IHeaderData? Last { get; }

        /// <summary>
        /// Adds a header to the cache.
        /// </summary>
        /// <param name="header">The header to add.</param>
        /// <returns>True if the header was added, false if rejected.</returns>
        bool Add(IHeaderData header);

        /// <summary>
        /// Tries to get a header by its hash.
        /// </summary>
        /// <param name="hash">The header hash.</param>
        /// <param name="header">The header if found.</param>
        /// <returns>True if the header was found.</returns>
        bool TryGet(byte[] hash, out IHeaderData? header);

        /// <summary>
        /// Tries to get a header by its index.
        /// </summary>
        /// <param name="index">The header index.</param>
        /// <param name="header">The header if found.</param>
        /// <returns>True if the header was found.</returns>
        bool TryGet(uint index, out IHeaderData? header);

        /// <summary>
        /// Gets all headers in the cache.
        /// </summary>
        /// <returns>An enumerable of all cached headers.</returns>
        IEnumerable<IHeaderData> GetAll();

        /// <summary>
        /// Clears all headers from the cache.
        /// </summary>
        void Clear();
    }
}
