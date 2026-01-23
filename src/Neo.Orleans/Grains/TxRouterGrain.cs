// Copyright (C) 2015-2025 The Neo Project.
//
// TxRouterGrain.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo;
using Neo.Core.Interfaces;
using Neo.Extensions;
using Neo.IO;
using Neo.Ledger;
using Neo.Network.P2P.Payloads;
using Neo.Orleans.Hosting;
using Neo.Orleans.Interfaces;
using Orleans.Runtime;
using System.Diagnostics;

namespace Neo.Orleans.Grains
{
    /// <summary>
    /// Orleans Grain implementation for transaction pre-verification routing.
    /// Supports parallel pre-verification workflows.
    /// </summary>
    public class TxRouterGrain : Grain, ITxRouterGrain
    {
        private readonly IPersistentState<TxRouterState> _state;
        private readonly IGrainFactory _grainFactory;
        private readonly ProtocolSettings _settings;
        private readonly NeoOrleansOptions _options;

        public TxRouterGrain(
            [PersistentState("txrouter", "TxRouterStore")]
            IPersistentState<TxRouterState> state,
            IGrainFactory grainFactory,
            ProtocolSettings settings,
            NeoOrleansOptions? options = null)
        {
            _state = state;
            _grainFactory = grainFactory;
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _options = options ?? new NeoOrleansOptions();
        }

        public Task<TxPreverifyResult> PreverifyAsync(ITransactionData transaction, bool relay)
        {
            var sw = Stopwatch.StartNew();

            try
            {
                if (!TryDeserializeTransaction(transaction, out var tx))
                {
                    sw.Stop();
                    UpdateStats(false, sw.ElapsedMilliseconds);
                    return Task.FromResult(new TxPreverifyResult(
                        transaction.Hash.GetSpan().ToArray(),
                        false,
                        false,
                        "Transaction deserialization failed"));
                }

                var verifyResult = _options.ValidationMode == NeoValidationMode.None
                    ? VerifyResult.Succeed
                    : tx.VerifyStateIndependent(_settings);
                var isValid = verifyResult == VerifyResult.Succeed;

                sw.Stop();
                UpdateStats(isValid, sw.ElapsedMilliseconds);

                return Task.FromResult(new TxPreverifyResult(
                    tx.Hash.GetSpan().ToArray(),
                    isValid,
                    relay && isValid,
                    isValid ? null : $"Transaction validation failed: {verifyResult}"));
            }
            catch (Exception ex)
            {
                sw.Stop();
                UpdateStats(false, sw.ElapsedMilliseconds);

                return Task.FromResult(new TxPreverifyResult(
                    transaction.Hash.GetSpan().ToArray(),
                    false,
                    false,
                    ex.Message));
            }
        }

        public async Task<IReadOnlyList<TxPreverifyResult>> PreverifyBatchAsync(
            IEnumerable<ITransactionData> transactions, bool relay)
        {
            var tasks = transactions.Select(tx => PreverifyAsync(tx, relay));
            var results = await Task.WhenAll(tasks);
            return results;
        }

        public Task<TxRouterStats> GetStatsAsync()
        {
            var avgTime = _state.State.TotalVerified > 0
                ? _state.State.TotalVerificationTimeMs / _state.State.TotalVerified
                : 0;

            return Task.FromResult(new TxRouterStats(
                _state.State.TotalVerified,
                _state.State.TotalValid,
                _state.State.TotalInvalid,
                avgTime));
        }

        public Task ResetStatsAsync()
        {
            _state.State.TotalVerified = 0;
            _state.State.TotalValid = 0;
            _state.State.TotalInvalid = 0;
            _state.State.TotalVerificationTimeMs = 0;
            return _state.WriteStateAsync();
        }

        private static bool TryDeserializeTransaction(ITransactionData transaction, out Transaction tx)
        {
            tx = null!;
            if (transaction is Transaction fullTransaction)
            {
                tx = fullTransaction;
                return true;
            }

            var raw = transaction.ToArray();
            if (raw.Length == 0)
                return false;

            try
            {
                var reader = new MemoryReader(raw);
                tx = reader.ReadSerializable<Transaction>();
                return true;
            }
            catch (FormatException)
            {
                return false;
            }
            catch
            {
                return false;
            }
        }

        private void UpdateStats(bool isValid, long elapsedMs)
        {
            _state.State.TotalVerified++;
            _state.State.TotalVerificationTimeMs += elapsedMs;

            if (isValid)
                _state.State.TotalValid++;
            else
                _state.State.TotalInvalid++;

            // Persist stats periodically (every 100 verifications)
            if (_state.State.TotalVerified % 100 == 0)
            {
                _ = _state.WriteStateAsync();
            }
        }
    }

    /// <summary>
    /// Persistent state for TxRouterGrain.
    /// </summary>
    [GenerateSerializer]
    public class TxRouterState
    {
        [Id(0)] public long TotalVerified { get; set; }
        [Id(1)] public long TotalValid { get; set; }
        [Id(2)] public long TotalInvalid { get; set; }
        [Id(3)] public double TotalVerificationTimeMs { get; set; }
    }
}
