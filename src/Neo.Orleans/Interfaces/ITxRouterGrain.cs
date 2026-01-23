// Copyright (C) 2015-2025 The Neo Project.
//
// ITxRouterGrain.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Core.Interfaces;

namespace Neo.Orleans.Interfaces
{
    /// <summary>
    /// Orleans Grain interface for transaction pre-verification routing.
    /// Supports parallel pre-verification workflows.
    /// </summary>
    public interface ITxRouterGrain : IGrainWithIntegerKey
    {
        /// <summary>
        /// Pre-verifies a transaction (state-independent verification).
        /// </summary>
        /// <param name="transaction">The transaction to verify.</param>
        /// <param name="relay">Whether to relay the transaction after verification.</param>
        /// <returns>The verification result.</returns>
        Task<TxPreverifyResult> PreverifyAsync(ITransactionData transaction, bool relay);

        /// <summary>
        /// Pre-verifies multiple transactions in parallel.
        /// </summary>
        /// <param name="transactions">The transactions to verify.</param>
        /// <param name="relay">Whether to relay transactions after verification.</param>
        /// <returns>The verification results.</returns>
        Task<IReadOnlyList<TxPreverifyResult>> PreverifyBatchAsync(
            IEnumerable<ITransactionData> transactions, bool relay);

        /// <summary>
        /// Gets the router statistics.
        /// </summary>
        Task<TxRouterStats> GetStatsAsync();

        /// <summary>
        /// Resets the router statistics.
        /// </summary>
        Task ResetStatsAsync();
    }

    /// <summary>
    /// Result of transaction pre-verification.
    /// </summary>
    [GenerateSerializer]
    public record TxPreverifyResult(
        [property: Id(0)] byte[] TransactionHash,
        [property: Id(1)] bool IsValid,
        [property: Id(2)] bool ShouldRelay,
        [property: Id(3)] string? ErrorMessage);

    /// <summary>
    /// Statistics for the transaction router.
    /// </summary>
    [GenerateSerializer]
    public record TxRouterStats(
        [property: Id(0)] long TotalVerified,
        [property: Id(1)] long TotalValid,
        [property: Id(2)] long TotalInvalid,
        [property: Id(3)] double AverageVerificationTimeMs);
}
