// Copyright (C) 2015-2025 The Neo Project.
//
// SignerType.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using GraphQL.Types;
using Neo.Network.P2P.Payloads;
using System.Linq;

namespace Neo.GraphQL.Types
{
    /// <summary>
    /// GraphQL type representing a transaction signer.
    /// </summary>
    public sealed class SignerType : ObjectGraphType<Signer>
    {
        public SignerType()
        {
            Name = "Signer";
            Description = "A transaction signer with scope and permissions";

            Field<NonNullGraphType<StringGraphType>>("account")
                .Description("Signer account (script hash)")
                .Resolve(ctx => ctx.Source.Account.ToString());

            Field<NonNullGraphType<StringGraphType>>("scopes")
                .Description("Witness scope flags")
                .Resolve(ctx => ctx.Source.Scopes.ToString());

            Field<NonNullGraphType<ListGraphType<NonNullGraphType<StringGraphType>>>>("allowedContracts")
                .Description("Allowed contract hashes (if CustomContracts scope)")
                .Resolve(ctx => ctx.Source.AllowedContracts?.Select(c => c.ToString()).ToArray() ?? []);

            Field<NonNullGraphType<ListGraphType<NonNullGraphType<StringGraphType>>>>("allowedGroups")
                .Description("Allowed group public keys (if CustomGroups scope)")
                .Resolve(ctx => ctx.Source.AllowedGroups?.Select(g => g.ToString()).ToArray() ?? []);
        }
    }
}
