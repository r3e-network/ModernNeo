// Copyright (C) 2015-2025 The Neo Project.
//
// IBlockValidator.cs file belongs to the neo project and is free
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
    /// Provides block validation operations.
    /// </summary>
    public interface IBlockValidator
    {
        /// <summary>
        /// Validates a block against the current blockchain state.
        /// </summary>
        /// <param name="block">The block to validate.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The verification result.</returns>
        Task<VerifyResult> ValidateBlockAsync(IBlockData block, CancellationToken cancellationToken = default);

        /// <summary>
        /// Validates a header against the current blockchain state.
        /// </summary>
        /// <param name="header">The header to validate.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The verification result.</returns>
        Task<VerifyResult> ValidateHeaderAsync(IHeaderData header, CancellationToken cancellationToken = default);
    }
}
