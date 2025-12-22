// Copyright (C) 2015-2025 The Neo Project.
//
// NodeInfoType.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using GraphQL.Types;
using Neo.Services.NodeInfo;

namespace Neo.GraphQL.Types
{
    /// <summary>
    /// GraphQL type representing node information.
    /// </summary>
    public sealed class NodeInfoType : ObjectGraphType
    {
        public NodeInfoType(INodeInfoService? nodeService)
        {
            Name = "NodeInfo";
            Description = "Neo node information and status";

            Field<NonNullGraphType<StringGraphType>>("network")
                .Description("Network magic identifier")
                .Resolve(_ => nodeService?.Network ?? "unknown");

            Field<NonNullGraphType<LongGraphType>>("height")
                .Description("Current blockchain height")
                .Resolve(_ => nodeService?.Height ?? 0);

            Field<NonNullGraphType<IntGraphType>>("mempoolCount")
                .Description("Number of transactions in mempool")
                .Resolve(_ => nodeService?.MempoolCount ?? 0);

            Field<NonNullGraphType<StringGraphType>>("version")
                .Description("GraphQL API version")
                .Resolve(_ => "v1.0");
        }
    }
}
