// Copyright (C) 2015-2025 The Neo Project.
//
// TransactionType.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using GraphQL.Types;
using Neo.Network.P2P.Payloads;
using System;
using System.Linq;

namespace Neo.GraphQL.Types
{
    /// <summary>
    /// GraphQL type representing a Neo transaction.
    /// </summary>
    public sealed class TransactionType : ObjectGraphType<Transaction>
    {
        public TransactionType()
        {
            Name = "Transaction";
            Description = "A Neo blockchain transaction";

            Field<NonNullGraphType<StringGraphType>>("hash")
                .Description("Transaction hash (UInt256)")
                .Resolve(ctx => ctx.Source.Hash.ToString());

            Field<NonNullGraphType<IntGraphType>>("version")
                .Description("Transaction version")
                .Resolve(ctx => (int)ctx.Source.Version);

            Field<NonNullGraphType<UIntGraphType>>("nonce")
                .Description("Transaction nonce")
                .Resolve(ctx => ctx.Source.Nonce);

            Field<NonNullGraphType<StringGraphType>>("sender")
                .Description("Transaction sender address (script hash)")
                .Resolve(ctx => ctx.Source.Sender.ToString());

            Field<NonNullGraphType<LongGraphType>>("systemFee")
                .Description("System fee in GAS (datoshi)")
                .Resolve(ctx => ctx.Source.SystemFee);

            Field<NonNullGraphType<LongGraphType>>("networkFee")
                .Description("Network fee in GAS (datoshi)")
                .Resolve(ctx => ctx.Source.NetworkFee);

            Field<NonNullGraphType<UIntGraphType>>("validUntilBlock")
                .Description("Block height until which transaction is valid")
                .Resolve(ctx => ctx.Source.ValidUntilBlock);

            Field<NonNullGraphType<IntGraphType>>("size")
                .Description("Transaction size in bytes")
                .Resolve(ctx => ctx.Source.Size);

            Field<NonNullGraphType<LongGraphType>>("feePerByte")
                .Description("Fee per byte (NetworkFee / Size)")
                .Resolve(ctx => ctx.Source.FeePerByte);

            Field<NonNullGraphType<StringGraphType>>("script")
                .Description("Transaction script (base64 encoded)")
                .Resolve(ctx => Convert.ToBase64String(ctx.Source.Script.Span));

            Field<NonNullGraphType<IntGraphType>>("signersCount")
                .Description("Number of signers")
                .Resolve(ctx => ctx.Source.SignersCount);

            Field<NonNullGraphType<IntGraphType>>("attributesCount")
                .Description("Number of attributes")
                .Resolve(ctx => ctx.Source.AttributesCount);

            Field<NonNullGraphType<ListGraphType<NonNullGraphType<SignerType>>>>("signers")
                .Description("Transaction signers")
                .Resolve(ctx => ctx.Source.Signers);

            Field<NonNullGraphType<ListGraphType<NonNullGraphType<WitnessType>>>>("witnesses")
                .Description("Transaction witnesses")
                .Resolve(ctx => ctx.Source.Witnesses);
        }
    }
}
