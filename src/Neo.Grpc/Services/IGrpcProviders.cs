// Copyright (C) 2015-2025 The Neo Project.
//
// IGrpcProviders.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Network.P2P.Payloads;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Neo.Grpc.Services
{
    /// <summary>
    /// Interface for providing blockchain data to gRPC services.
    /// This abstraction allows different implementations (NeoSystem, Orleans, etc.)
    /// to provide data to the gRPC layer.
    /// </summary>
    public interface IGrpcBlockchainProvider
    {
        /// <summary>
        /// Gets a block by its hash.
        /// </summary>
        Task<Block?> GetBlockAsync(UInt256 hash);

        /// <summary>
        /// Gets a block by its index.
        /// </summary>
        Task<Block?> GetBlockByIndexAsync(uint index);

        /// <summary>
        /// Gets a block header by its hash.
        /// </summary>
        Task<Header?> GetHeaderAsync(UInt256 hash);

        /// <summary>
        /// Gets a block header by its index.
        /// </summary>
        Task<Header?> GetHeaderByIndexAsync(uint index);

        /// <summary>
        /// Gets the hash of a block by its index.
        /// </summary>
        Task<UInt256?> GetBlockHashAsync(uint index);

        /// <summary>
        /// Gets the current block count (height + 1).
        /// </summary>
        Task<uint> GetBlockCountAsync();

        /// <summary>
        /// Gets a transaction by its hash.
        /// </summary>
        Task<(Transaction? tx, uint blockIndex, Neo.VM.VMState state)?> GetTransactionAsync(UInt256 hash);

        /// <summary>
        /// Gets verified transactions from the memory pool.
        /// </summary>
        Task<IEnumerable<Transaction>> GetMemPoolTransactionsAsync();

        /// <summary>
        /// Gets memory pool statistics.
        /// </summary>
        Task<(int verified, int unverified)> GetMemPoolInfoAsync();

        /// <summary>
        /// Sends a raw transaction to the network.
        /// </summary>
        Task<(bool success, string? error)> SendTransactionAsync(Transaction tx);
    }

    /// <summary>
    /// Interface for providing node information to gRPC services.
    /// </summary>
    public interface IGrpcNodeProvider
    {
        /// <summary>
        /// Gets the protocol settings.
        /// </summary>
        ProtocolSettings Settings { get; }

        /// <summary>
        /// Gets the number of connected peers.
        /// </summary>
        Task<int> GetConnectionCountAsync();

        /// <summary>
        /// Gets the list of validators.
        /// </summary>
        Task<IEnumerable<(Cryptography.ECC.ECPoint publicKey, long votes, bool active)>> GetValidatorsAsync();

        /// <summary>
        /// Gets the committee members.
        /// </summary>
        Task<IEnumerable<Cryptography.ECC.ECPoint>> GetCommitteeAsync();
    }

    /// <summary>
    /// Interface for providing contract data to gRPC services.
    /// </summary>
    public interface IGrpcContractProvider
    {
        /// <summary>
        /// Gets a contract state by its hash.
        /// </summary>
        Task<SmartContract.ContractState?> GetContractAsync(UInt160 hash);

        /// <summary>
        /// Gets a storage value.
        /// </summary>
        Task<byte[]?> GetStorageAsync(UInt160 scriptHash, byte[] key);

        /// <summary>
        /// Gets NEP-17 balance for an address.
        /// </summary>
        Task<System.Numerics.BigInteger> GetNep17BalanceAsync(UInt160 tokenHash, UInt160 address);
    }
}
