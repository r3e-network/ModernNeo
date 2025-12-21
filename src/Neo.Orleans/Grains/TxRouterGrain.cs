// Copyright (C) 2015-2025 The Neo Project.
//
// TxRouterGrain.cs file belongs to the neo project and is free
// software distributed under the MIT software license.

using Neo.Core.Interfaces;
using Neo.Orleans.Interfaces;
using Orleans.Runtime;
using System.Diagnostics;

namespace Neo.Orleans.Grains;

/// <summary>
/// Orleans Grain implementation for transaction pre-verification routing.
/// Replaces Akka.NET TransactionRouter Actor with parallel verification.
/// </summary>
public class TxRouterGrain : Grain, ITxRouterGrain
{
    private readonly IPersistentState<TxRouterState> _state;
    private readonly IGrainFactory _grainFactory;

    public TxRouterGrain(
        [PersistentState("txrouter", "TxRouterStore")]
        IPersistentState<TxRouterState> state,
        IGrainFactory grainFactory)
    {
        _state = state;
        _grainFactory = grainFactory;
    }

    public async Task<TxPreverifyResult> PreverifyAsync(ITransactionData transaction, bool relay)
    {
        var sw = Stopwatch.StartNew();

        try
        {
            // Perform state-independent verification
            // In a full implementation, this would call transaction.VerifyStateIndependent()
            // For now, we do basic validation
            var isValid = ValidateTransaction(transaction);

            sw.Stop();
            UpdateStats(isValid, sw.ElapsedMilliseconds);

            return new TxPreverifyResult(
                transaction.Hash.GetSpan().ToArray(),
                isValid,
                relay && isValid,
                isValid ? null : "Transaction validation failed");
        }
        catch (Exception ex)
        {
            sw.Stop();
            UpdateStats(false, sw.ElapsedMilliseconds);

            return new TxPreverifyResult(
                transaction.Hash.GetSpan().ToArray(),
                false,
                false,
                ex.Message);
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

    private bool ValidateTransaction(ITransactionData transaction)
    {
        // Basic validation checks
        // In a full implementation, this would perform state-independent verification

        // Check hash is not empty
        var hash = transaction.Hash.GetSpan();
        if (hash.IsEmpty)
            return false;

        // Check version
        if (transaction.Version > 0)
            return false;

        // Check script is not empty
        if (transaction.Script.IsEmpty)
            return false;

        // Check system fee is non-negative
        if (transaction.SystemFee < 0)
            return false;

        // Check network fee is non-negative
        if (transaction.NetworkFee < 0)
            return false;

        return true;
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
