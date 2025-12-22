// Copyright (C) 2015-2025 The Neo Project.
//
// IBlockStorageService.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Core.Interfaces;

namespace Neo.Orleans.Services
{
    /// <summary>
    /// Service interface for block storage operations.
    /// Abstracts the underlying storage layer for BlockchainGrain.
    /// </summary>
    public interface IBlockStorageService
    {
        /// <summary>
        /// Stores a block in the storage.
        /// </summary>
        Task<bool> StoreBlockAsync(IBlockData block);

        /// <summary>
        /// Retrieves a block by its hash.
        /// </summary>
        Task<IBlockData?> GetBlockByHashAsync(byte[] hash);

        /// <summary>
        /// Retrieves a block by its index.
        /// </summary>
        Task<IBlockData?> GetBlockByIndexAsync(uint index);

        /// <summary>
        /// Checks if a block exists by hash.
        /// </summary>
        Task<bool> ContainsBlockAsync(byte[] hash);

        /// <summary>
        /// Checks if a transaction exists by hash.
        /// </summary>
        Task<bool> ContainsTransactionAsync(byte[] hash);

        /// <summary>
        /// Gets the current blockchain height.
        /// </summary>
        Task<uint> GetHeightAsync();
    }
}
