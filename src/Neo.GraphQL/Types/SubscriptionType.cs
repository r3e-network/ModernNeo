// Copyright (C) 2015-2025 The Neo Project.
//
// SubscriptionType.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using GraphQL.Resolvers;
using GraphQL.Types;
using Neo.Network.P2P.Payloads;
using Neo.Services.Events;
using System;

namespace Neo.GraphQL.Types
{
    /// <summary>
    /// GraphQL subscription type for real-time blockchain events.
    /// </summary>
    public sealed class SubscriptionType : ObjectGraphType
    {
        public SubscriptionType(IBlockchainEventService? eventService)
        {
            Name = "Subscription";
            Description = "Real-time blockchain event subscriptions";

            AddField(new FieldType
            {
                Name = "blockCommitted",
                Description = "Subscribe to new blocks as they are committed to the blockchain",
                Type = typeof(BlockType),
                Resolver = new FuncFieldResolver<Block>(ctx => ctx.Source as Block),
                StreamResolver = new SourceStreamResolver<Block>(_ =>
                    eventService?.BlockCommitted ?? throw new InvalidOperationException("Event service not available"))
            });

            AddField(new FieldType
            {
                Name = "transactionAdded",
                Description = "Subscribe to new transactions added to the mempool",
                Type = typeof(TransactionType),
                Resolver = new FuncFieldResolver<Transaction>(ctx => ctx.Source as Transaction),
                StreamResolver = new SourceStreamResolver<Transaction>(_ =>
                    eventService?.TransactionAdded ?? throw new InvalidOperationException("Event service not available"))
            });

            AddField(new FieldType
            {
                Name = "transactionRemoved",
                Description = "Subscribe to transactions removed from the mempool",
                Type = typeof(TransactionRemovedType),
                Resolver = new FuncFieldResolver<TransactionRemovedEvent>(ctx => ctx.Source as TransactionRemovedEvent),
                StreamResolver = new SourceStreamResolver<TransactionRemovedEvent>(_ =>
                    eventService?.TransactionRemoved ?? throw new InvalidOperationException("Event service not available"))
            });
        }
    }

    /// <summary>
    /// GraphQL type for transaction removed events.
    /// </summary>
    public sealed class TransactionRemovedType : ObjectGraphType<TransactionRemovedEvent>
    {
        public TransactionRemovedType()
        {
            Name = "TransactionRemoved";
            Description = "Event data for a transaction removed from mempool";

            Field<NonNullGraphType<TransactionType>>("transaction")
                .Description("The removed transaction")
                .Resolve(ctx => ctx.Source.Transaction);

            Field<NonNullGraphType<StringGraphType>>("reason")
                .Description("Reason for removal (e.g., 'included_in_block', 'expired', 'replaced')")
                .Resolve(ctx => ctx.Source.Reason);
        }
    }
}
