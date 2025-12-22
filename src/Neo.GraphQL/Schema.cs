// Copyright (C) 2015-2025 The Neo Project.
//
// Schema.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using GraphQL;
using GraphQL.Types;
using Neo.GraphQL.Types;
using Neo.Services.Blocks;
using Neo.Services.NodeInfo;
using Neo.Services.Transactions;
using System;
using System.Linq;

namespace Neo.GraphQL
{
    public sealed class NeoSchema : Schema
    {
        public NeoSchema(IServiceProvider provider) : base(provider)
        {
            Query = new RootQuery(
                provider.GetService(typeof(INodeInfoService)) as INodeInfoService,
                provider.GetService(typeof(IBlockQueryService)) as IBlockQueryService,
                provider.GetService(typeof(ITransactionQueryService)) as ITransactionQueryService);
        }
    }

    public sealed class RootQuery : ObjectGraphType
    {
        public RootQuery(
            INodeInfoService? node,
            IBlockQueryService? blocks,
            ITransactionQueryService? transactions)
        {
            Name = "Query";
            Description = "Neo blockchain GraphQL API root query";

            // ============================================
            // Node Information Queries
            // ============================================

            Field<NonNullGraphType<StringGraphType>>("version")
                .Description("Returns GraphQL API version")
                .Resolve(_ => "v1.0");

            Field<NonNullGraphType<StringGraphType>>("network")
                .Description("Network magic identifier")
                .Resolve(_ => node?.Network ?? "unknown");

            Field<NonNullGraphType<LongGraphType>>("height")
                .Description("Current blockchain height")
                .Resolve(_ => node?.Height ?? 0);

            Field<NonNullGraphType<IntGraphType>>("mempoolCount")
                .Description("Number of transactions in mempool")
                .Resolve(_ => node?.MempoolCount ?? 0);

            // ============================================
            // Block Queries
            // ============================================

            Field<BlockType>("block")
                .Description("Get block by index")
                .Argument<NonNullGraphType<UIntGraphType>>("index", "Block index/height")
                .Resolve(ctx =>
                {
                    if (blocks is null) return null;
                    var index = ctx.GetArgument<uint>("index");
                    return blocks.GetBlockByIndex(index);
                });

            Field<BlockType>("blockByHash")
                .Description("Get block by hash")
                .Argument<NonNullGraphType<StringGraphType>>("hash", "Block hash (hex, with or without 0x prefix)")
                .Resolve(ctx =>
                {
                    if (blocks is null) return null;
                    var hash = ctx.GetArgument<string>("hash");
                    return blocks.GetBlockByHash(hash);
                });

            Field<NonNullGraphType<ListGraphType<NonNullGraphType<BlockType>>>>("blocks")
                .Description("Get blocks by index range")
                .Argument<NonNullGraphType<UIntGraphType>>("start", "Starting block index")
                .Argument<NonNullGraphType<IntGraphType>>("count", "Number of blocks to retrieve (max 100)")
                .Resolve(ctx =>
                {
                    if (blocks is null) return Array.Empty<Network.P2P.Payloads.Block>();
                    var start = ctx.GetArgument<uint>("start");
                    var count = Math.Min(ctx.GetArgument<int>("count"), 100); // Limit to 100
                    return blocks.GetBlocks(start, count).ToArray();
                });

            Field<NonNullGraphType<ListGraphType<NonNullGraphType<StringGraphType>>>>("blockHashes")
                .Description("Get block hashes by index range")
                .Argument<NonNullGraphType<UIntGraphType>>("start", "Starting block index")
                .Argument<NonNullGraphType<IntGraphType>>("count", "Number of hashes to retrieve (max 500)")
                .Resolve(ctx =>
                {
                    if (blocks is null) return Array.Empty<string>();
                    var start = ctx.GetArgument<uint>("start");
                    var count = Math.Min(ctx.GetArgument<int>("count"), 500); // Limit to 500
                    return blocks.GetBlocks(start, count).Select(b => b.Hash.ToString()).ToArray();
                });

            // ============================================
            // Transaction Queries
            // ============================================

            Field<TransactionType>("transaction")
                .Description("Get transaction by hash")
                .Argument<NonNullGraphType<StringGraphType>>("hash", "Transaction hash (hex, with or without 0x prefix)")
                .Resolve(ctx =>
                {
                    if (transactions is null) return null;
                    var hash = ctx.GetArgument<string>("hash");
                    return transactions.GetTransactionByHash(hash);
                });

            Field<NonNullGraphType<BooleanGraphType>>("transactionExists")
                .Description("Check if transaction exists in blockchain")
                .Argument<NonNullGraphType<StringGraphType>>("hash", "Transaction hash (hex)")
                .Resolve(ctx =>
                {
                    if (transactions is null) return false;
                    var hash = ctx.GetArgument<string>("hash");
                    return transactions.TransactionExists(hash);
                });

            Field<UIntGraphType>("transactionBlockIndex")
                .Description("Get block index containing the transaction")
                .Argument<NonNullGraphType<StringGraphType>>("hash", "Transaction hash (hex)")
                .Resolve(ctx =>
                {
                    if (transactions is null) return null;
                    var hash = ctx.GetArgument<string>("hash");
                    return transactions.GetTransactionBlockIndex(hash);
                });

            Field<NonNullGraphType<ListGraphType<NonNullGraphType<TransactionType>>>>("mempoolTransactions")
                .Description("Get unconfirmed transactions from mempool")
                .Argument<IntGraphType>("count", "Maximum number of transactions (default 50, max 200)")
                .Resolve(ctx =>
                {
                    if (transactions is null) return Array.Empty<Network.P2P.Payloads.Transaction>();
                    var count = Math.Min(ctx.GetArgument<int?>("count") ?? 50, 200);
                    return transactions.GetMempoolTransactions(count).ToArray();
                });

            // ============================================
            // Block Transaction Queries
            // ============================================

            Field<NonNullGraphType<ListGraphType<NonNullGraphType<TransactionType>>>>("blockTransactions")
                .Description("Get transactions from a specific block")
                .Argument<NonNullGraphType<UIntGraphType>>("index", "Block index")
                .Resolve(ctx =>
                {
                    if (blocks is null) return Array.Empty<Network.P2P.Payloads.Transaction>();
                    var index = ctx.GetArgument<uint>("index");
                    var block = blocks.GetBlockByIndex(index);
                    return block?.Transactions ?? Array.Empty<Network.P2P.Payloads.Transaction>();
                });
        }
    }
}
