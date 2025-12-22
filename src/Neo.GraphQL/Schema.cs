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
using Neo.Services.Accounts;
using Neo.Services.Blocks;
using Neo.Services.Contracts;
using Neo.Services.Events;
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
                provider.GetService(typeof(ITransactionQueryService)) as ITransactionQueryService,
                provider.GetService(typeof(IAccountQueryService)) as IAccountQueryService,
                provider.GetService(typeof(IContractQueryService)) as IContractQueryService);

            Subscription = new SubscriptionType(
                provider.GetService(typeof(IBlockchainEventService)) as IBlockchainEventService);
        }
    }

    public sealed class RootQuery : ObjectGraphType
    {
        public RootQuery(
            INodeInfoService? node,
            IBlockQueryService? blocks,
            ITransactionQueryService? transactions,
            IAccountQueryService? accounts,
            IContractQueryService? contracts)
        {
            Name = "Query";
            Description = "Neo blockchain GraphQL API root query";

            // ============================================
            // Node Information Queries
            // ============================================

            Field<NonNullGraphType<StringGraphType>>("version")
                .Description("Returns GraphQL API version")
                .Resolve(_ => "v1.2");

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
                    var count = Math.Min(ctx.GetArgument<int>("count"), 100);
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
                    var count = Math.Min(ctx.GetArgument<int>("count"), 500);
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

            // ============================================
            // Account Queries
            // ============================================

            Field<AccountBalanceType>("account")
                .Description("Get account balance information")
                .Argument<NonNullGraphType<StringGraphType>>("addressOrHash", "Account address or script hash (hex)")
                .Resolve(ctx =>
                {
                    if (accounts is null) return null;
                    var addressOrHash = ctx.GetArgument<string>("addressOrHash");
                    if (!accounts.IsValidAddress(addressOrHash)) return null;

                    var scriptHash = accounts.AddressToScriptHash(addressOrHash) ?? addressOrHash;
                    var address = accounts.ScriptHashToAddress(addressOrHash) ?? addressOrHash;

                    return new AccountBalanceData
                    {
                        Address = address,
                        ScriptHash = scriptHash,
                        NeoBalance = accounts.GetNeoBalance(addressOrHash).ToString(),
                        GasBalance = accounts.GetGasBalance(addressOrHash).ToString(),
                        UnclaimedGas = accounts.GetUnclaimedGas(addressOrHash).ToString()
                    };
                });

            Field<NonNullGraphType<StringGraphType>>("neoBalance")
                .Description("Get NEO balance for an account")
                .Argument<NonNullGraphType<StringGraphType>>("addressOrHash", "Account address or script hash")
                .Resolve(ctx =>
                {
                    if (accounts is null) return "0";
                    var addressOrHash = ctx.GetArgument<string>("addressOrHash");
                    return accounts.GetNeoBalance(addressOrHash).ToString();
                });

            Field<NonNullGraphType<StringGraphType>>("gasBalance")
                .Description("Get GAS balance for an account (in datoshi)")
                .Argument<NonNullGraphType<StringGraphType>>("addressOrHash", "Account address or script hash")
                .Resolve(ctx =>
                {
                    if (accounts is null) return "0";
                    var addressOrHash = ctx.GetArgument<string>("addressOrHash");
                    return accounts.GetGasBalance(addressOrHash).ToString();
                });

            Field<NonNullGraphType<StringGraphType>>("unclaimedGas")
                .Description("Get unclaimed GAS for an account (in datoshi)")
                .Argument<NonNullGraphType<StringGraphType>>("addressOrHash", "Account address or script hash")
                .Resolve(ctx =>
                {
                    if (accounts is null) return "0";
                    var addressOrHash = ctx.GetArgument<string>("addressOrHash");
                    return accounts.GetUnclaimedGas(addressOrHash).ToString();
                });

            Field<NonNullGraphType<BooleanGraphType>>("isValidAddress")
                .Description("Check if address or script hash is valid")
                .Argument<NonNullGraphType<StringGraphType>>("addressOrHash", "Address or script hash to validate")
                .Resolve(ctx =>
                {
                    if (accounts is null) return false;
                    var addressOrHash = ctx.GetArgument<string>("addressOrHash");
                    return accounts.IsValidAddress(addressOrHash);
                });

            // ============================================
            // Contract Queries
            // ============================================

            Field<ContractType>("contract")
                .Description("Get contract by script hash")
                .Argument<NonNullGraphType<StringGraphType>>("hash", "Contract script hash (hex)")
                .Resolve(ctx =>
                {
                    if (contracts is null) return null;
                    var hash = ctx.GetArgument<string>("hash");
                    return contracts.GetContract(hash);
                });

            Field<ContractType>("contractById")
                .Description("Get contract by ID")
                .Argument<NonNullGraphType<IntGraphType>>("id", "Contract ID")
                .Resolve(ctx =>
                {
                    if (contracts is null) return null;
                    var id = ctx.GetArgument<int>("id");
                    return contracts.GetContractById(id);
                });

            Field<NonNullGraphType<BooleanGraphType>>("contractExists")
                .Description("Check if contract exists")
                .Argument<NonNullGraphType<StringGraphType>>("hash", "Contract script hash (hex)")
                .Resolve(ctx =>
                {
                    if (contracts is null) return false;
                    var hash = ctx.GetArgument<string>("hash");
                    return contracts.ContractExists(hash);
                });

            Field<NonNullGraphType<BooleanGraphType>>("contractHasMethod")
                .Description("Check if contract has a specific method")
                .Argument<NonNullGraphType<StringGraphType>>("hash", "Contract script hash (hex)")
                .Argument<NonNullGraphType<StringGraphType>>("method", "Method name")
                .Argument<NonNullGraphType<IntGraphType>>("parameterCount", "Number of parameters")
                .Resolve(ctx =>
                {
                    if (contracts is null) return false;
                    var hash = ctx.GetArgument<string>("hash");
                    var method = ctx.GetArgument<string>("method");
                    var paramCount = ctx.GetArgument<int>("parameterCount");
                    return contracts.HasMethod(hash, method, paramCount);
                });

            Field<NonNullGraphType<ListGraphType<NonNullGraphType<ContractType>>>>("contracts")
                .Description("List deployed contracts (paginated)")
                .Argument<IntGraphType>("skip", "Number of contracts to skip (default 0)")
                .Argument<IntGraphType>("take", "Number of contracts to return (default 20, max 100)")
                .Resolve(ctx =>
                {
                    if (contracts is null) return Array.Empty<SmartContract.ContractState>();
                    var skip = ctx.GetArgument<int?>("skip") ?? 0;
                    var take = ctx.GetArgument<int?>("take") ?? 20;
                    return contracts.ListContracts(skip, take).ToArray();
                });

            Field<NonNullGraphType<IntGraphType>>("contractCount")
                .Description("Get total number of deployed contracts")
                .Resolve(_ => contracts?.GetContractCount() ?? 0);
        }
    }
}
