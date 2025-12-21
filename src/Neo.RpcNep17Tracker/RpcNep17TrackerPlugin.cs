// Copyright (C) 2015-2025 The Neo Project.
//
// RpcNep17TrackerPlugin.cs file belongs to the neo project and is free
// software distributed under the MIT software license.

using Neo;
using Neo.Extensions;
using Neo.IO;
using Neo.IEventHandlers;
using Neo.Json;
using Neo.Ledger;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.Persistence.Providers;
using Neo.SmartContract.Native;
using Neo.VM.Types;
using Neo.Wallets;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using Array = Neo.VM.Types.Array;

namespace Neo.Plugins;

public sealed class RpcNep17TrackerPlugin : Plugin, ICommittingHandler, INep17Tracker, IPersistencePlugin
{
    private const byte PrefixBalance = 0x01;
    private const byte PrefixSent = 0x02;
    private const byte PrefixReceived = 0x03;

    private readonly object _syncRoot = new();
    private readonly Dictionary<UInt160, bool> _nep17Cache = new();

    private string _storageEngine = nameof(MemoryStore);
    private string _storagePathTemplate = "RpcNep17Tracker_{0}";
    private IStore? _store;
    private ProtocolSettings? _settings;
    private bool _subscribed;

    public override string Name => "RpcNep17Tracker";

    protected override void Configure()
    {
        var config = GetConfiguration();
        _storageEngine = config.GetValue("Storage:Engine", _storageEngine);
        _storagePathTemplate = config.GetValue("Storage:Path", _storagePathTemplate);
    }

    protected internal override void OnSystemLoaded(NeoSystem system)
    {
        _settings = system.Settings;

        var path = string.Format(CultureInfo.InvariantCulture, _storagePathTemplate, system.Settings.Network);
        if (!Path.IsPathRooted(path))
            path = Path.Combine(RootPath, path);

        try
        {
            _store = StoreFactory.GetStore(_storageEngine, path);
        }
        catch (Exception ex)
        {
            Log($"Failed to open RpcNep17Tracker store '{_storageEngine}' at '{path}': {ex.Message}", LogLevel.Error);
            _store = StoreFactory.GetStore(nameof(MemoryStore), path);
        }

        if (!_subscribed)
        {
            Blockchain.Committing += Blockchain_Committing_Handler;
            _subscribed = true;
        }
    }

    public override void Dispose()
    {
        if (_subscribed)
        {
            Blockchain.Committing -= Blockchain_Committing_Handler;
            _subscribed = false;
        }

        _store?.Dispose();
        _store = null;
    }

    public void Blockchain_Committing_Handler(NeoSystem system, Block block, DataCache snapshot, IReadOnlyList<Blockchain.ApplicationExecuted> applicationExecutedList)
    {
        if (_store is null)
            return;

        var timestamp = block.Timestamp;
        var blockIndex = block.Index;

        lock (_syncRoot)
        {
            foreach (var execution in applicationExecutedList)
            {
                var tx = execution.Transaction;
                if (tx is null)
                    continue;

                var notifications = execution.Notifications;
                for (var i = 0; i < notifications.Length; i++)
                {
                    var notification = notifications[i];
                    if (!string.Equals(notification.EventName, "Transfer", StringComparison.Ordinal))
                        continue;

                    if (!IsNep17Contract(snapshot, notification.ScriptHash))
                        continue;

                    if (!TryParseTransfer(notification.State, out var from, out var to, out var amount))
                        continue;

                    if (amount.Sign < 0)
                        continue;

                    if (from is null && to is null)
                        continue;

                    var notifyIndex = (uint)i;
                    var assetHash = notification.ScriptHash;
                    var txHash = tx.Hash;

                    if (from is not null)
                    {
                        StoreTransfer(PrefixSent, from, timestamp, txHash, notifyIndex, assetHash, to, amount, blockIndex);
                        UpdateBalance(from, assetHash, -amount, blockIndex);
                    }

                    if (to is not null)
                    {
                        StoreTransfer(PrefixReceived, to, timestamp, txHash, notifyIndex, assetHash, from, amount, blockIndex);
                        UpdateBalance(to, assetHash, amount, blockIndex);
                    }
                }
            }
        }
    }

    public string? GetNep17Balances(UInt160 account)
    {
        if (_store is null || _settings is null)
            return null;

        var balances = new List<(UInt160 AssetHash, BigInteger Amount, uint LastUpdated)>();
        var prefix = BuildBalancePrefix(account);

        lock (_syncRoot)
        {
            foreach (var (key, value) in _store.Find(prefix))
            {
                if (!TryReadBalance(value, out var amount, out var lastUpdated))
                    continue;

                if (amount.Sign <= 0)
                    continue;

                var assetHash = TryReadAssetFromBalanceKey(key);
                if (assetHash is null)
                    continue;

                balances.Add((assetHash, amount, lastUpdated));
            }
        }

        var balanceJson = new JArray();
        foreach (var entry in balances.OrderBy(b => b.AssetHash.ToString(), StringComparer.Ordinal))
        {
            balanceJson.Add(new JObject
            {
                ["assethash"] = entry.AssetHash.ToString(),
                ["amount"] = entry.Amount.ToString(),
                ["lastupdatedblock"] = new JNumber(entry.LastUpdated)
            });
        }

        var result = new JObject
        {
            ["address"] = account.ToAddress(_settings.AddressVersion),
            ["balance"] = balanceJson
        };

        return result.ToString();
    }

    public string? GetNep17Transfers(UInt160 account, ulong from, ulong to)
    {
        if (_store is null || _settings is null)
            return null;

        var sent = new JArray();
        var received = new JArray();

        lock (_syncRoot)
        {
            CollectTransfers(sent, PrefixSent, account, from, to);
            CollectTransfers(received, PrefixReceived, account, from, to);
        }

        var result = new JObject
        {
            ["address"] = account.ToAddress(_settings.AddressVersion),
            ["sent"] = sent,
            ["received"] = received
        };

        return result.ToString();
    }

    private bool IsNep17Contract(DataCache snapshot, UInt160 scriptHash)
    {
        if (_nep17Cache.TryGetValue(scriptHash, out var cached))
            return cached;

        var contract = NativeContract.ContractManagement.GetContract(snapshot, scriptHash);
        var supported = contract is not null &&
                        contract.Manifest.SupportedStandards.Any(s => string.Equals(s, "NEP-17", StringComparison.OrdinalIgnoreCase));
        _nep17Cache[scriptHash] = supported;
        return supported;
    }

    private static bool TryParseTransfer(Array state, out UInt160? from, out UInt160? to, out BigInteger amount)
    {
        from = null;
        to = null;
        amount = BigInteger.Zero;

        if (state.Count < 3)
            return false;

        if (!TryReadAddress(state[0], out from))
            return false;

        if (!TryReadAddress(state[1], out to))
            return false;

        try
        {
            amount = state[2].GetInteger();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryReadAddress(StackItem item, out UInt160? address)
    {
        address = null;
        if (item.IsNull)
            return true;

        var span = item.GetSpan();
        if (span.Length != UInt160.Length)
            return false;

        address = new UInt160(span);
        return true;
    }

    private void UpdateBalance(UInt160 account, UInt160 assetHash, BigInteger delta, uint blockIndex)
    {
        if (_store is null)
            return;

        var key = BuildBalanceKey(account, assetHash);
        var amount = BigInteger.Zero;

        if (_store.TryGet(key, out var existing) && existing is not null)
        {
            if (!TryReadBalance(existing, out amount, out _))
                amount = BigInteger.Zero;
        }

        var updated = amount + delta;
        if (updated.Sign <= 0)
        {
            _store.Delete(key);
            return;
        }

        _store.Put(key, WriteBalance(updated, blockIndex));
    }

    private void CollectTransfers(JArray destination, byte prefix, UInt160 account, ulong from, ulong to)
    {
        if (_store is null || _settings is null)
            return;

        var keyPrefix = BuildTransferPrefix(prefix, account);
        foreach (var (key, value) in _store.Find(keyPrefix))
        {
            if (!TryReadTransferKey(key, out var timestamp, out var txHash, out var notifyIndex))
                continue;

            if (timestamp < from)
                continue;

            if (timestamp > to)
                break;

            if (!TryReadTransferValue(value, out var transfer))
                continue;

            destination.Add(new JObject
            {
                ["timestamp"] = new JNumber((double)timestamp),
                ["assethash"] = transfer.AssetHash.ToString(),
                ["transferaddress"] = transfer.TransferAddress is null
                    ? JToken.Null
                    : transfer.TransferAddress.ToAddress(_settings.AddressVersion),
                ["amount"] = transfer.Amount.ToString(),
                ["blockindex"] = new JNumber(transfer.BlockIndex),
                ["transfernotifyindex"] = new JNumber(notifyIndex),
                ["txhash"] = txHash.ToString()
            });
        }
    }

    private void StoreTransfer(byte prefix, UInt160 account, ulong timestamp, UInt256 txHash, uint notifyIndex, UInt160 assetHash, UInt160? transferAddress, BigInteger amount, uint blockIndex)
    {
        if (_store is null)
            return;

        var key = BuildTransferKey(prefix, account, timestamp, txHash, notifyIndex);
        var value = WriteTransferValue(assetHash, transferAddress, amount, blockIndex);
        _store.Put(key, value);
    }

    private static byte[] BuildBalancePrefix(UInt160 account)
    {
        var data = new byte[1 + UInt160.Length];
        data[0] = PrefixBalance;
        account.GetSpan().CopyTo(data.AsSpan(1));
        return data;
    }

    private static byte[] BuildBalanceKey(UInt160 account, UInt160 assetHash)
    {
        var data = new byte[1 + UInt160.Length + UInt160.Length];
        data[0] = PrefixBalance;
        account.GetSpan().CopyTo(data.AsSpan(1));
        assetHash.GetSpan().CopyTo(data.AsSpan(1 + UInt160.Length));
        return data;
    }

    private static UInt160? TryReadAssetFromBalanceKey(byte[] key)
    {
        if (key.Length < 1 + UInt160.Length + UInt160.Length)
            return null;

        return new UInt160(key.AsSpan(1 + UInt160.Length, UInt160.Length));
    }

    private static byte[] BuildTransferPrefix(byte prefix, UInt160 account)
    {
        var data = new byte[1 + UInt160.Length];
        data[0] = prefix;
        account.GetSpan().CopyTo(data.AsSpan(1));
        return data;
    }

    private static byte[] BuildTransferKey(byte prefix, UInt160 account, ulong timestamp, UInt256 txHash, uint notifyIndex)
    {
        var data = new byte[1 + UInt160.Length + sizeof(ulong) + UInt256.Length + sizeof(uint)];
        data[0] = prefix;
        account.GetSpan().CopyTo(data.AsSpan(1));

        var offset = 1 + UInt160.Length;
        BinaryPrimitives.WriteUInt64BigEndian(data.AsSpan(offset, sizeof(ulong)), timestamp);
        offset += sizeof(ulong);

        txHash.GetSpan().CopyTo(data.AsSpan(offset, UInt256.Length));
        offset += UInt256.Length;

        BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(offset, sizeof(uint)), notifyIndex);
        return data;
    }

    private static bool TryReadTransferKey(byte[] key, out ulong timestamp, out UInt256 txHash, out uint notifyIndex)
    {
        timestamp = 0;
        txHash = UInt256.Zero;
        notifyIndex = 0;

        if (key.Length < 1 + UInt160.Length + sizeof(ulong) + UInt256.Length + sizeof(uint))
            return false;

        var offset = 1 + UInt160.Length;
        timestamp = BinaryPrimitives.ReadUInt64BigEndian(key.AsSpan(offset, sizeof(ulong)));
        offset += sizeof(ulong);

        txHash = new UInt256(key.AsSpan(offset, UInt256.Length));
        offset += UInt256.Length;

        notifyIndex = BinaryPrimitives.ReadUInt32BigEndian(key.AsSpan(offset, sizeof(uint)));
        return true;
    }

    private static byte[] WriteBalance(BigInteger amount, uint lastUpdated)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.WriteVarBytes(amount.ToByteArray());
        writer.Write(lastUpdated);
        return stream.ToArray();
    }

    private static bool TryReadBalance(byte[] value, out BigInteger amount, out uint lastUpdated)
    {
        amount = BigInteger.Zero;
        lastUpdated = 0;

        try
        {
            using var stream = new MemoryStream(value, writable: false);
            using var reader = new BinaryReader(stream);
            amount = new BigInteger(reader.ReadVarBytes());
            lastUpdated = reader.ReadUInt32();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static byte[] WriteTransferValue(UInt160 assetHash, UInt160? transferAddress, BigInteger amount, uint blockIndex)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write(assetHash.GetSpan());
        writer.Write(blockIndex);
        writer.WriteVarBytes(amount.ToByteArray());

        if (transferAddress is null)
        {
            writer.Write((byte)0);
        }
        else
        {
            writer.Write((byte)1);
            writer.Write(transferAddress.GetSpan());
        }

        return stream.ToArray();
    }

    private static bool TryReadTransferValue(byte[] value, out TransferValue transfer)
    {
        transfer = default;

        try
        {
            using var stream = new MemoryStream(value, writable: false);
            using var reader = new BinaryReader(stream);
            var assetHash = new UInt160(reader.ReadFixedBytes(UInt160.Length));
            var blockIndex = reader.ReadUInt32();
            var amount = new BigInteger(reader.ReadVarBytes());

            var hasAddress = reader.ReadByte() != 0;
            UInt160? transferAddress = null;
            if (hasAddress)
                transferAddress = new UInt160(reader.ReadFixedBytes(UInt160.Length));

            transfer = new TransferValue(assetHash, amount, blockIndex, transferAddress);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private readonly record struct TransferValue(UInt160 AssetHash, BigInteger Amount, uint BlockIndex, UInt160? TransferAddress);
}
