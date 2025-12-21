// Copyright (C) 2015-2025 The Neo Project.
//
// NativeContractRegistry.cs file belongs to the neo project and is free
// software distributed under the MIT software license.

using Neo.Core.Interfaces;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.SmartContract.Native;
using System.Collections.Generic;
using System.Linq;

namespace Neo.SmartContract;

/// <summary>
/// Default implementation of INativeContractRegistry that wraps the static NativeContract accessors.
/// </summary>
public class NativeContractRegistry : INativeContractRegistry
{
    /// <summary>
    /// Gets the singleton instance of the registry.
    /// </summary>
    public static NativeContractRegistry Instance { get; } = new();

    /// <inheritdoc/>
    public IReadOnlyCollection<INativeContractData> Contracts =>
        NativeContract.Contracts.Cast<INativeContractData>().ToList();

    /// <inheritdoc/>
    public INativeContractData? GetContract(byte[] hash)
    {
        var uint160Hash = new UInt160(hash);
        return NativeContract.GetContract(uint160Hash) as INativeContractData;
    }

    /// <inheritdoc/>
    public INativeContractData? GetContractById(int id)
    {
        return NativeContract.Contracts.FirstOrDefault(c => c.Id == id) as INativeContractData;
    }

    /// <inheritdoc/>
    public bool IsNative(byte[] hash)
    {
        var uint160Hash = new UInt160(hash);
        return NativeContract.IsNative(uint160Hash);
    }

    /// <inheritdoc/>
    public uint GetCurrentIndex(object snapshot)
    {
        if (snapshot is not IReadOnlyStore store)
            throw new System.ArgumentException("Snapshot must implement IReadOnlyStore", nameof(snapshot));
        return NativeContract.Ledger.CurrentIndex(store);
    }

    /// <inheritdoc/>
    public IBlockData? GetBlock(object snapshot, uint index)
    {
        if (snapshot is not StoreCache storeCache)
            throw new System.ArgumentException("Snapshot must be a StoreCache", nameof(snapshot));
        return NativeContract.Ledger.GetBlock(storeCache, index);
    }

    /// <inheritdoc/>
    public IBlockData? GetBlock(object snapshot, byte[] hash)
    {
        if (snapshot is not StoreCache storeCache)
            throw new System.ArgumentException("Snapshot must be a StoreCache", nameof(snapshot));
        var uint256Hash = new UInt256(hash);
        return NativeContract.Ledger.GetBlock(storeCache, uint256Hash);
    }

    /// <inheritdoc/>
    public bool ContainsTransaction(object snapshot, byte[] hash)
    {
        if (snapshot is not StoreCache storeCache)
            throw new System.ArgumentException("Snapshot must be a StoreCache", nameof(snapshot));
        var uint256Hash = new UInt256(hash);
        return NativeContract.Ledger.ContainsTransaction(storeCache, uint256Hash);
    }

    /// <inheritdoc/>
    public long GetFeePerByte(object snapshot)
    {
        if (snapshot is not IReadOnlyStore store)
            throw new System.ArgumentException("Snapshot must implement IReadOnlyStore", nameof(snapshot));
        return NativeContract.Policy.GetFeePerByte(store);
    }

    /// <inheritdoc/>
    public uint GetStoragePrice(object snapshot)
    {
        if (snapshot is not IReadOnlyStore store)
            throw new System.ArgumentException("Snapshot must implement IReadOnlyStore", nameof(snapshot));
        return NativeContract.Policy.GetStoragePrice(store);
    }
}
