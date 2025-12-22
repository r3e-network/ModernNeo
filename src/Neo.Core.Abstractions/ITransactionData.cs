// Copyright (C) 2015-2025 The Neo Project.
//
// ITransactionData.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using System.Collections.Generic;

namespace Neo.Core.Abstractions
{
    /// <summary>
    /// Represents immutable transaction data in the Neo blockchain.
    /// This interface provides read-only access to transaction properties
    /// without exposing implementation details.
    /// </summary>
    public interface ITransactionData
    {
        /// <summary>
        /// Gets the transaction hash as a byte array.
        /// </summary>
        byte[] Hash { get; }

        /// <summary>
        /// Gets the transaction version.
        /// </summary>
        byte Version { get; }

        /// <summary>
        /// Gets the nonce used to prevent replay attacks.
        /// </summary>
        uint Nonce { get; }

        /// <summary>
        /// Gets the system fee for this transaction.
        /// </summary>
        long SystemFee { get; }

        /// <summary>
        /// Gets the network fee for this transaction.
        /// </summary>
        long NetworkFee { get; }

        /// <summary>
        /// Gets the block index after which this transaction is valid.
        /// </summary>
        uint ValidUntilBlock { get; }

        /// <summary>
        /// Gets the script to be executed by the VM.
        /// </summary>
        byte[] Script { get; }

        /// <summary>
        /// Gets the sender's script hash.
        /// </summary>
        byte[] Sender { get; }

        /// <summary>
        /// Gets the signers of this transaction.
        /// </summary>
        IReadOnlyList<ISignerData> Signers { get; }

        /// <summary>
        /// Gets the witnesses (signatures) for this transaction.
        /// </summary>
        IReadOnlyList<IWitnessData> Witnesses { get; }

        /// <summary>
        /// Gets the transaction attributes.
        /// </summary>
        IReadOnlyList<ITransactionAttribute> Attributes { get; }

        /// <summary>
        /// Gets the size of the transaction in bytes.
        /// </summary>
        int Size { get; }
    }

    /// <summary>
    /// Represents a transaction signer.
    /// </summary>
    public interface ISignerData
    {
        /// <summary>
        /// Gets the signer's account script hash.
        /// </summary>
        byte[] Account { get; }

        /// <summary>
        /// Gets the witness scope flags.
        /// </summary>
        byte Scopes { get; }
    }

    /// <summary>
    /// Represents a witness (signature) for verification.
    /// </summary>
    public interface IWitnessData
    {
        /// <summary>
        /// Gets the invocation script (signature data).
        /// </summary>
        byte[] InvocationScript { get; }

        /// <summary>
        /// Gets the verification script (public key/contract).
        /// </summary>
        byte[] VerificationScript { get; }
    }

    /// <summary>
    /// Represents a transaction attribute.
    /// </summary>
    public interface ITransactionAttribute
    {
        /// <summary>
        /// Gets the attribute type.
        /// </summary>
        byte Type { get; }
    }
}
