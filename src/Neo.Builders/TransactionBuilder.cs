// Copyright (C) 2015-2025 The Neo Project.
//
// TransactionBuilder.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Core.Builders;
using Neo.Network.P2P.Payloads;
using Neo.Network.P2P.Payloads.Conditions;
using System;

namespace Neo.Builders
{
    public sealed class TransactionBuilder : ITransactionBuilder<Transaction, Signer, Witness>
    {
        // OpCode.RET = 0x40 - default empty script that just returns
        private static readonly byte[] DefaultScript = [0x40];

        private readonly Transaction _tx = new()
        {
            Script = DefaultScript,
            Attributes = [],
            Signers = [],
            Witnesses = [],
        };

        private TransactionBuilder() { }

        public static TransactionBuilder CreateEmpty()
        {
            return new TransactionBuilder();
        }

        public TransactionBuilder Version(byte version)
        {
            _tx.Version = version;
            return this;
        }

        public TransactionBuilder Nonce(uint nonce)
        {
            _tx.Nonce = nonce;
            return this;
        }

        public TransactionBuilder SystemFee(uint systemFee)
        {
            _tx.SystemFee = systemFee;
            return this;
        }

        public TransactionBuilder NetworkFee(uint networkFee)
        {
            _tx.NetworkFee = networkFee;
            return this;
        }

        public TransactionBuilder ValidUntil(uint blockIndex)
        {
            _tx.ValidUntilBlock = blockIndex;
            return this;
        }

        public TransactionBuilder AttachSystem(byte[] script)
        {
            _tx.Script = script;
            return this;
        }

        public TransactionBuilder AddAttributes(Action<TransactionAttributesBuilder> config)
        {
            var ab = TransactionAttributesBuilder.CreateEmpty();
            config(ab);
            _tx.Attributes = ab.Build();
            return this;
        }

        public TransactionBuilder AddWitness(Action<WitnessBuilder> config)
        {
            var wb = WitnessBuilder.CreateEmpty();
            config(wb);
            _tx.Witnesses = [.. _tx.Witnesses, wb.Build()];
            return this;
        }

        public TransactionBuilder AddWitness(Action<WitnessBuilder, Transaction> config)
        {
            var wb = WitnessBuilder.CreateEmpty();
            config(wb, _tx);
            _tx.Witnesses = [.. _tx.Witnesses, wb.Build()];
            return this;
        }

        public TransactionBuilder AddSigner(UInt160 account, Action<SignerBuilder, Transaction> config)
        {
            var wb = SignerBuilder.Create(account);
            config(wb, _tx);
            _tx.Signers = [.. _tx.Signers, wb.Build()];
            return this;
        }

        public Transaction Build()
        {
            return _tx;
        }

        ITransactionBuilder<Transaction, Signer, Witness> ITransactionBuilder<Transaction, Signer, Witness>.Version(byte version)
        {
            Version(version);
            return this;
        }

        ITransactionBuilder<Transaction, Signer, Witness> ITransactionBuilder<Transaction, Signer, Witness>.Nonce(uint nonce)
        {
            Nonce(nonce);
            return this;
        }

        ITransactionBuilder<Transaction, Signer, Witness> ITransactionBuilder<Transaction, Signer, Witness>.SystemFee(long systemFee)
        {
            _tx.SystemFee = systemFee;
            return this;
        }

        ITransactionBuilder<Transaction, Signer, Witness> ITransactionBuilder<Transaction, Signer, Witness>.NetworkFee(long networkFee)
        {
            _tx.NetworkFee = networkFee;
            return this;
        }

        ITransactionBuilder<Transaction, Signer, Witness> ITransactionBuilder<Transaction, Signer, Witness>.ValidUntil(uint blockIndex)
        {
            ValidUntil(blockIndex);
            return this;
        }

        ITransactionBuilder<Transaction, Signer, Witness> ITransactionBuilder<Transaction, Signer, Witness>.Script(byte[] script)
        {
            AttachSystem(script);
            return this;
        }

        ITransactionBuilder<Transaction, Signer, Witness> ITransactionBuilder<Transaction, Signer, Witness>.AddSigner(Signer signer)
        {
            _tx.Signers = [.. _tx.Signers, signer];
            return this;
        }

        ITransactionBuilder<Transaction, Signer, Witness> ITransactionBuilder<Transaction, Signer, Witness>.AddWitness(Witness witness)
        {
            _tx.Witnesses = [.. _tx.Witnesses, witness];
            return this;
        }
    }
}
