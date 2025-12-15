// Copyright (C) 2015-2025 The Neo Project.
//
// IVerificationService.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

namespace Neo.Core.Interfaces
{
    /// <summary>
    /// Service interface for verifying IVerifiableBase objects.
    /// This separates verification logic from the verifiable objects themselves,
    /// breaking the circular dependency between Network and Persistence layers.
    /// </summary>
    /// <remarks>
    /// Implementations of this interface will have access to DataCache/snapshot
    /// and can perform state-dependent verification without the IVerifiableBase
    /// interface needing to know about persistence.
    /// </remarks>
    public interface IVerificationService
    {
        /// <summary>
        /// Gets the script hashes that should be verified for the given object.
        /// </summary>
        /// <param name="verifiable">The object to get verification hashes for.</param>
        /// <returns>The script hashes that should be verified.</returns>
        UInt160[] GetScriptHashesForVerifying(IVerifiableBase verifiable);

        /// <summary>
        /// Verifies the signatures of the given object.
        /// </summary>
        /// <param name="verifiable">The object to verify.</param>
        /// <returns>True if verification succeeds; otherwise, false.</returns>
        bool Verify(IVerifiableBase verifiable);

        /// <summary>
        /// Verifies the signatures of the given object with additional context.
        /// </summary>
        /// <param name="verifiable">The object to verify.</param>
        /// <param name="context">Additional verification context.</param>
        /// <returns>True if verification succeeds; otherwise, false.</returns>
        bool Verify(IVerifiableBase verifiable, object? context);
    }
}
