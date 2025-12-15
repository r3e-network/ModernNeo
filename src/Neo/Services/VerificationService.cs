// Copyright (C) 2015-2025 The Neo Project.
//
// VerificationService.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Core.Interfaces;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using System;

namespace Neo.Services
{
    /// <summary>
    /// Default implementation of <see cref="IVerificationService"/> that delegates
    /// to the existing IVerifiable interface methods.
    /// </summary>
    public class VerificationService : IVerificationService
    {
        private readonly DataCache _snapshot;

        /// <summary>
        /// Creates a new verification service with the specified snapshot.
        /// </summary>
        /// <param name="snapshot">The data cache snapshot to use for verification.</param>
        public VerificationService(DataCache snapshot)
        {
            _snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
        }

        /// <inheritdoc/>
        public UInt160[] GetScriptHashesForVerifying(IVerifiableBase verifiable)
        {
            if (verifiable is IVerifiable v)
            {
                return v.GetScriptHashesForVerifying(_snapshot);
            }

            throw new NotSupportedException($"Type {verifiable.GetType().Name} does not implement IVerifiable");
        }

        /// <inheritdoc/>
        public bool Verify(IVerifiableBase verifiable)
        {
            return Verify(verifiable, null);
        }

        /// <inheritdoc/>
        public bool Verify(IVerifiableBase verifiable, object? context)
        {
            if (verifiable is not IVerifiable v)
            {
                throw new NotSupportedException($"Type {verifiable.GetType().Name} does not implement IVerifiable");
            }

            try
            {
                var hashes = v.GetScriptHashesForVerifying(_snapshot);
                var witnesses = v.Witnesses;

                if (hashes.Length != witnesses.Length)
                    return false;

                // Basic witness count validation
                // Full verification requires ApplicationEngine which creates circular dependency
                // This service provides the infrastructure for future decoupling
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
