// Copyright (C) 2015-2025 The Neo Project.
//
// BlockType.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using GraphQL.Types;
using Neo.Network.P2P.Payloads;

namespace Neo.GraphQL.Types
{
    /// <summary>
    /// GraphQL type representing a Neo block.
    /// </summary>
    public sealed class BlockType : ObjectGraphType<Block>
    {
        public BlockType()
        {
            Name = "Block";
            Description = "A Neo blockchain block containing transactions";

            Field<NonNullGraphType<StringGraphType>>("hash")
                .Description("Block hash (UInt256)")
                .Resolve(ctx => ctx.Source.Hash.ToString());

            Field<NonNullGraphType<UIntGraphType>>("index")
                .Description("Block height/index")
                .Resolve(ctx => ctx.Source.Index);

            Field<NonNullGraphType<UIntGraphType>>("version")
                .Description("Block version")
                .Resolve(ctx => ctx.Source.Version);

            Field<NonNullGraphType<StringGraphType>>("prevHash")
                .Description("Previous block hash")
                .Resolve(ctx => ctx.Source.PrevHash.ToString());

            Field<NonNullGraphType<StringGraphType>>("merkleRoot")
                .Description("Merkle root of transactions")
                .Resolve(ctx => ctx.Source.MerkleRoot.ToString());

            Field<NonNullGraphType<ULongGraphType>>("timestamp")
                .Description("Block timestamp (milliseconds since epoch)")
                .Resolve(ctx => ctx.Source.Timestamp);

            Field<NonNullGraphType<ULongGraphType>>("nonce")
                .Description("Block nonce")
                .Resolve(ctx => ctx.Source.Nonce);

            Field<NonNullGraphType<IntGraphType>>("primaryIndex")
                .Description("Primary consensus node index")
                .Resolve(ctx => (int)ctx.Source.PrimaryIndex);

            Field<NonNullGraphType<StringGraphType>>("nextConsensus")
                .Description("Next consensus address")
                .Resolve(ctx => ctx.Source.NextConsensus.ToString());

            Field<NonNullGraphType<IntGraphType>>("size")
                .Description("Block size in bytes")
                .Resolve(ctx => ctx.Source.Size);

            Field<NonNullGraphType<IntGraphType>>("transactionCount")
                .Description("Number of transactions in block")
                .Resolve(ctx => ctx.Source.Transactions.Length);

            Field<NonNullGraphType<ListGraphType<NonNullGraphType<TransactionType>>>>("transactions")
                .Description("Transactions in this block")
                .Resolve(ctx => ctx.Source.Transactions);

            Field<NonNullGraphType<ListGraphType<NonNullGraphType<StringGraphType>>>>("transactionHashes")
                .Description("Transaction hashes in this block")
                .Resolve(ctx =>
                {
                    var hashes = new string[ctx.Source.Transactions.Length];
                    for (int i = 0; i < ctx.Source.Transactions.Length; i++)
                        hashes[i] = ctx.Source.Transactions[i].Hash.ToString();
                    return hashes;
                });
        }
    }
}
