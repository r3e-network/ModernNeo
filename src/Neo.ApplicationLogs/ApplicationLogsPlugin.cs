// Copyright (C) 2015-2025 The Neo Project.
//
// ApplicationLogsPlugin.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Microsoft.Extensions.Configuration;
using Neo;
using Neo.Extensions;
using Neo.IEventHandlers;
using Neo.Json;
using Neo.Ledger;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.Persistence.Providers;
using Neo.SmartContract;
using Neo.VM.Types;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace Neo.Plugins
{
    public sealed class ApplicationLogsPlugin : Plugin, ICommittingHandler, IApplicationLogProvider, IPersistencePlugin
    {
        private const byte PrefixBlockLog = 0x01;
        private const byte PrefixTxLog = 0x02;

        private readonly object _syncRoot = new();

        private string _storageEngine = nameof(MemoryStore);
        private string _storagePathTemplate = "ApplicationLogs_{0}";
        private IStore? _store;
        private bool _subscribed;

        public override string Name => "ApplicationLogs";

        protected override void Configure()
        {
            var config = GetConfiguration();
            _storageEngine = config.GetValue("Storage:Engine", _storageEngine);
            _storagePathTemplate = config.GetValue("Storage:Path", _storagePathTemplate);
        }

        private void EnsureInitialized(NeoSystem system)
        {
            if (_store is not null) return;

            var path = string.Format(CultureInfo.InvariantCulture, _storagePathTemplate, system.Settings.Network);
            if (!System.IO.Path.IsPathRooted(path))
                path = System.IO.Path.Combine(RootPath, path);

            try
            {
                _store = StoreFactory.GetStore(_storageEngine, path);
            }
            catch (Exception ex)
            {
                Log($"Failed to open ApplicationLogs store '{_storageEngine}' at '{path}': {ex.Message}", LogLevel.Error);
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
            EnsureInitialized(system);
            if (_store is null) return;

            var blockExecutions = new List<JObject>(applicationExecutedList.Count);
            var txExecutions = new Dictionary<UInt256, List<JObject>>();

            foreach (var execution in applicationExecutedList)
            {
                var executionJson = BuildExecutionJson(execution);

                if (execution.Transaction is null)
                {
                    blockExecutions.Add(executionJson);
                    continue;
                }

                var txHash = execution.Transaction.Hash;
                executionJson["txid"] = txHash.ToString();
                blockExecutions.Add(executionJson);

                if (!txExecutions.TryGetValue(txHash, out var list))
                {
                    list = [];
                    txExecutions[txHash] = list;
                }

                list.Add(executionJson);
            }

            foreach (var (txHash, executions) in txExecutions)
            {
                var txLog = new JObject
                {
                    ["txid"] = txHash.ToString(),
                    ["executions"] = new JArray(executions.ToArray())
                };

                StoreLog(BuildKey(PrefixTxLog, txHash), txLog);
            }

            var blockLog = new JObject
            {
                ["blockhash"] = block.Hash.ToString(),
                ["executions"] = new JArray(blockExecutions.ToArray())
            };

            StoreLog(BuildKey(PrefixBlockLog, block.Hash), blockLog);
        }

        public string? GetApplicationLog(UInt256 hash, TriggerType? trigger)
        {
            if (_store is null)
                return null;

            if (!TryGetLog(BuildKey(PrefixTxLog, hash), out var log) &&
                !TryGetLog(BuildKey(PrefixBlockLog, hash), out log))
            {
                return null;
            }

            if (!trigger.HasValue)
                return log;

            try
            {
                if (string.IsNullOrEmpty(log))
                    return null;
                var token = JToken.Parse(log) as JObject;
                if (token is null)
                    return log;

                var executions = token["executions"] as JArray ?? [];
                var filtered = new JArray(
                    executions.Where(e => e?["trigger"]?.AsString() == trigger.Value.ToString()).ToArray()
                );

                token["executions"] = filtered;
                return token.ToString();
            }
            catch
            {
                return log;
            }
        }

        private static JObject BuildExecutionJson(Blockchain.ApplicationExecuted execution)
        {
            var json = new JObject
            {
                ["trigger"] = execution.Trigger.ToString(),
                ["vmstate"] = execution.VMState.ToString(),
                ["gasconsumed"] = execution.GasConsumed.ToString(),
                ["exception"] = execution.Exception?.Message is null
                    ? JToken.Null
                    : new JString(execution.Exception.Message)
            };

            var stack = new JArray();
            foreach (StackItem item in execution.Stack)
                stack.Add(item.ToJson());
            json["stack"] = stack;

            var notifications = new JArray();
            foreach (var notification in execution.Notifications)
            {
                notifications.Add(new JObject
                {
                    ["contract"] = notification.ScriptHash.ToString(),
                    ["eventname"] = notification.EventName,
                    ["state"] = notification.State.ToJson()
                });
            }
            json["notifications"] = notifications;

            return json;
        }

        private bool TryGetLog(byte[] key, out string? log)
        {
            log = null;
            if (_store is null)
                return false;

            if (!_store.TryGet(key, out var value))
                return false;

            log = Encoding.UTF8.GetString(value);
            return true;
        }

        private void StoreLog(byte[] key, JObject log)
        {
            var payload = Encoding.UTF8.GetBytes(log.ToString());
            lock (_syncRoot)
            {
                _store?.Put(key, payload);
            }
        }

        private static byte[] BuildKey(byte prefix, UInt256 hash)
        {
            var data = new byte[1 + UInt256.Length];
            data[0] = prefix;
            hash.GetSpan().CopyTo(data.AsSpan(1));
            return data;
        }
    }
}
