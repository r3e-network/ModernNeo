// Copyright (C) 2015-2025 The Neo Project.
//
// ContractType.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using GraphQL.Types;
using Neo.SmartContract;
using System;
using System.Linq;

namespace Neo.GraphQL.Types
{
    /// <summary>
    /// GraphQL type representing a deployed smart contract.
    /// </summary>
    public sealed class ContractType : ObjectGraphType<ContractState>
    {
        public ContractType()
        {
            Name = "Contract";
            Description = "A deployed Neo smart contract";

            Field<NonNullGraphType<IntGraphType>>("id")
                .Description("Contract ID")
                .Resolve(ctx => ctx.Source.Id);

            Field<NonNullGraphType<StringGraphType>>("hash")
                .Description("Contract script hash")
                .Resolve(ctx => ctx.Source.Hash.ToString());

            Field<NonNullGraphType<IntGraphType>>("updateCounter")
                .Description("Number of times contract has been updated")
                .Resolve(ctx => (int)ctx.Source.UpdateCounter);

            Field<NonNullGraphType<StringGraphType>>("name")
                .Description("Contract name from manifest")
                .Resolve(ctx => ctx.Source.Manifest.Name);

            Field<NonNullGraphType<ListGraphType<NonNullGraphType<StringGraphType>>>>("supportedStandards")
                .Description("Supported standards (e.g., NEP-17)")
                .Resolve(ctx => ctx.Source.Manifest.SupportedStandards);

            Field<NonNullGraphType<ListGraphType<NonNullGraphType<StringGraphType>>>>("methods")
                .Description("Contract method names")
                .Resolve(ctx => ctx.Source.Manifest.Abi.Methods.Select(m => m.Name).ToArray());

            Field<NonNullGraphType<ListGraphType<NonNullGraphType<StringGraphType>>>>("events")
                .Description("Contract event names")
                .Resolve(ctx => ctx.Source.Manifest.Abi.Events.Select(e => e.Name).ToArray());

            Field<NonNullGraphType<ListGraphType<NonNullGraphType<StringGraphType>>>>("permissions")
                .Description("Contract permissions")
                .Resolve(ctx => ctx.Source.Manifest.Permissions.Select(p => p.ToString()).ToArray());

            Field<NonNullGraphType<ListGraphType<NonNullGraphType<StringGraphType>>>>("trusts")
                .Description("Trusted contract hashes")
                .Resolve(ctx => ctx.Source.Manifest.Trusts.Select(t => t.ToString()).ToArray());

            Field<NonNullGraphType<StringGraphType>>("script")
                .Description("Contract script (base64 encoded)")
                .Resolve(ctx => Convert.ToBase64String(ctx.Source.Nef.Script.Span));

            Field<NonNullGraphType<StringGraphType>>("compiler")
                .Description("Compiler used to build the contract")
                .Resolve(ctx => ctx.Source.Nef.Compiler);
        }
    }
}
