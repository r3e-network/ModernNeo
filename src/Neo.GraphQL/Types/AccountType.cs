// Copyright (C) 2015-2025 The Neo Project.
//
// AccountType.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using GraphQL.Types;

namespace Neo.GraphQL.Types
{
    /// <summary>
    /// GraphQL type representing account balance information.
    /// </summary>
    public sealed class AccountBalanceType : ObjectGraphType
    {
        public AccountBalanceType()
        {
            Name = "AccountBalance";
            Description = "Account balance information for NEO and GAS";

            Field<NonNullGraphType<StringGraphType>>("address")
                .Description("Account address");

            Field<NonNullGraphType<StringGraphType>>("scriptHash")
                .Description("Account script hash");

            Field<NonNullGraphType<StringGraphType>>("neoBalance")
                .Description("NEO balance (whole units)");

            Field<NonNullGraphType<StringGraphType>>("gasBalance")
                .Description("GAS balance (datoshi, 1 GAS = 10^8 datoshi)");

            Field<NonNullGraphType<StringGraphType>>("unclaimedGas")
                .Description("Unclaimed GAS (datoshi)");
        }
    }

    /// <summary>
    /// DTO for account balance data.
    /// </summary>
    public sealed class AccountBalanceData
    {
        public required string Address { get; init; }
        public required string ScriptHash { get; init; }
        public required string NeoBalance { get; init; }
        public required string GasBalance { get; init; }
        public required string UnclaimedGas { get; init; }
    }
}
