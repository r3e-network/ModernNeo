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

using System;

namespace Neo.Core.Interfaces
{
    /// <summary>
    /// Defines the data contract for a transaction without any external dependencies.
    /// This interface allows lower layers to work with transaction data without depending
    /// on the full Transaction implementation in Neo.Network.P2P.Payloads.
    /// </summary>
    public interface ITransactionData : IVerifiableBase
    {
        /// <summary>
        /// The version of the transaction.
        /// </summary>
        byte Version { get; }

        /// <summary>
        /// The nonce of the transaction.
        /// </summary>
        uint Nonce { get; }

        /// <summary>
        /// The system fee of the transaction.
        /// </summary>
        long SystemFee { get; }

        /// <summary>
        /// The network fee of the transaction.
        /// </summary>
        long NetworkFee { get; }

        /// <summary>
        /// Indicates that the transaction is only valid before this block height.
        /// </summary>
        uint ValidUntilBlock { get; }

        /// <summary>
        /// The script of the transaction.
        /// </summary>
        ReadOnlyMemory<byte> Script { get; }

        /// <summary>
        /// The sender is the first signer of the transaction.
        /// </summary>
        UInt160 Sender { get; }

        /// <summary>
        /// The <see cref="NetworkFee"/> for the transaction divided by its size.
        /// </summary>
        long FeePerByte { get; }

        /// <summary>
        /// The number of signers in the transaction.
        /// </summary>
        int SignersCount { get; }

        /// <summary>
        /// The number of attributes in the transaction.
        /// </summary>
        int AttributesCount { get; }
    }
}
