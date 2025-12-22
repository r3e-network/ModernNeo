// Copyright (C) 2015-2025 The Neo Project.
//
// ITransactionValidator.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using System.Threading;
using System.Threading.Tasks;

namespace Neo.Core.Abstractions.Blockchain
{
    /// <summary>
    /// Provides transaction validation operations.
    /// </summary>
    public interface ITransactionValidator
    {
        /// <summary>
        /// Validates a transaction against the current blockchain state.
        /// </summary>
        /// <param name="transaction">The transaction to validate.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The verification result.</returns>
        Task<VerifyResult> ValidateTransactionAsync(ITransactionData transaction, CancellationToken cancellationToken = default);

        /// <summary>
        /// Performs state-independent verification of a transaction.
        /// This includes signature verification and basic format checks.
        /// </summary>
        /// <param name="transaction">The transaction to verify.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The verification result.</returns>
        Task<VerifyResult> PreverifyTransactionAsync(ITransactionData transaction, CancellationToken cancellationToken = default);
    }
}
