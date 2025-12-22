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
using Neo.Services.Blocks;
using Neo.Services.NodeInfo;
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
                provider.GetService(typeof(IBlockQueryService)) as IBlockQueryService);
        }
    }

    public sealed class RootQuery : ObjectGraphType
    {
        public RootQuery(INodeInfoService? node, IBlockQueryService? blocks)
        {
            Name = "Query";
            Field<StringGraphType>("version")
                .Description("Returns GraphQL API version")
                .Resolve(_ => "v0.1");

            Field<StringGraphType>("network")
                .Description("Network magic as string")
                .Resolve(_ => node?.Network ?? "");

            Field<IntGraphType>("height")
                .Description("Current chain height")
                .Resolve(_ => node?.Height ?? 0);

            Field<IntGraphType>("mempoolCount")
                .Description("Total transactions in mempool")
                .Resolve(_ => node?.MempoolCount ?? 0);

            Field<NonNullGraphType<ListGraphType<NonNullGraphType<StringGraphType>>>>("blocks")
                .Description("List block hashes from start index for count")
                .Argument<NonNullGraphType<IntGraphType>>("start")
                .Argument<NonNullGraphType<IntGraphType>>("count")
                .Resolve(ctx =>
                {
                    if (blocks is null) return Array.Empty<string>();
                    var start = (uint)ctx.GetArgument<int>("start");
                    var count = ctx.GetArgument<int>("count");
                    return blocks.GetBlocks(start, count).Select(b => b.Hash.ToString());
                });

            Field<StringGraphType>("blockByHash")
                .Description("Get block hash normalized (echo) if found")
                .Argument<NonNullGraphType<StringGraphType>>("hash")
                .Resolve(ctx =>
                {
                    if (blocks is null) return null;
                    var h = ctx.GetArgument<string>("hash");
                    var b = blocks.GetBlockByHash(h);
                    return b?.Hash.ToString();
                });
        }
    }
}
