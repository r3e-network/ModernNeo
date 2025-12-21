// Copyright (C) 2015-2025 The Neo Project.
//
// BuilderFactory.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Core.Builders;
using Neo.Network.P2P.Payloads;

namespace Neo.Builders
{
    /// <summary>
    /// Default builder factory for Neo protocol payload types.
    /// </summary>
    public sealed class BuilderFactory : IBuilderFactory<Transaction, Signer, Witness>
    {
        public ITransactionBuilder<Transaction, Signer, Witness> CreateTransactionBuilder() => TransactionBuilder.CreateEmpty();

        public IWitnessBuilder<Witness> CreateWitnessBuilder() => WitnessBuilder.CreateEmpty();

        public ISignerBuilder<Signer> CreateSignerBuilder(byte[] account) => SignerBuilder.Create(new UInt160(account));
    }
}

