// Copyright (C) 2015-2025 The Neo Project.
//
// Program.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using GraphQL;
using GraphQL.MicrosoftDI;
using GraphQL.Server;
using GraphQL.SystemTextJson;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Neo.GraphQL.Types;

namespace Neo.GraphQL
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.Services
                .AddSingleton<NeoSchema>()
                .AddSingleton<Neo.Services.NodeInfo.INodeInfoService, Neo.Services.NodeInfo.NodeInfoService>()
                .AddSingleton<Neo.Services.Blocks.IBlockQueryService, Neo.Services.Blocks.BlockQueryService>()
                .AddSingleton<Neo.Services.Transactions.ITransactionQueryService, Neo.Services.Transactions.TransactionQueryService>()
                // Register GraphQL types
                .AddSingleton<BlockType>()
                .AddSingleton<TransactionType>()
                .AddSingleton<SignerType>()
                .AddSingleton<WitnessType>()
                .AddGraphQL(b => b
                    .AddSystemTextJson()
                    .AddSchema<NeoSchema>());

            var app = builder.Build();

            app.MapGraphQL("/graphql");

            app.Run();
        }
    }
}
