// Copyright (C) 2015-2025 The Neo Project.
//
// SpanLink.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using System.Collections.Generic;
using System.Diagnostics;

namespace Neo.Observability.Tracing.Propagation
{
    /// <summary>
    /// Represents a link to another span, used for correlation in distributed tracing.
    /// Links are useful for connecting causally-related spans that don't have a parent-child relationship.
    /// </summary>
    public readonly struct SpanLink
    {
        /// <summary>
        /// Gets the trace context of the linked span.
        /// </summary>
        public TraceContext Context { get; }

        /// <summary>
        /// Gets the attributes associated with this link.
        /// </summary>
        public IReadOnlyDictionary<string, object?>? Attributes { get; }

        /// <summary>
        /// Initializes a new SpanLink with a trace context.
        /// </summary>
        /// <param name="context">The trace context to link to.</param>
        public SpanLink(TraceContext context)
        {
            Context = context;
            Attributes = null;
        }

        /// <summary>
        /// Initializes a new SpanLink with a trace context and attributes.
        /// </summary>
        /// <param name="context">The trace context to link to.</param>
        /// <param name="attributes">Attributes describing the link.</param>
        public SpanLink(TraceContext context, IReadOnlyDictionary<string, object?> attributes)
        {
            Context = context;
            Attributes = attributes;
        }

        /// <summary>
        /// Creates a SpanLink from trace and span ID strings.
        /// </summary>
        /// <param name="traceId">The trace ID.</param>
        /// <param name="spanId">The span ID.</param>
        /// <returns>A new SpanLink, or a link with empty context if parsing fails.</returns>
        public static SpanLink FromIds(string traceId, string spanId)
        {
            if (TraceContext.TryParse(traceId, spanId, 0, null, out var context))
                return new SpanLink(context);

            return new SpanLink(TraceContext.Empty);
        }

        /// <summary>
        /// Creates a SpanLink for correlating with a transaction hash.
        /// </summary>
        /// <param name="txHash">The transaction hash (UInt256).</param>
        /// <returns>A SpanLink with the transaction hash as an attribute.</returns>
        public static SpanLink ForTransaction(string txHash)
        {
            return new SpanLink(TraceContext.Empty, new Dictionary<string, object?>
            {
                ["neo.tx.hash"] = txHash
            });
        }

        /// <summary>
        /// Creates a SpanLink for correlating with a block hash.
        /// </summary>
        /// <param name="blockHash">The block hash (UInt256).</param>
        /// <param name="blockIndex">The block index.</param>
        /// <returns>A SpanLink with the block hash as an attribute.</returns>
        public static SpanLink ForBlock(string blockHash, uint blockIndex)
        {
            return new SpanLink(TraceContext.Empty, new Dictionary<string, object?>
            {
                ["neo.block.hash"] = blockHash,
                ["neo.block.index"] = blockIndex
            });
        }

        /// <summary>
        /// Creates a SpanLink for correlating with an inventory hash.
        /// </summary>
        /// <param name="inventoryType">The inventory type.</param>
        /// <param name="hash">The inventory hash.</param>
        /// <returns>A SpanLink with the inventory info as attributes.</returns>
        public static SpanLink ForInventory(string inventoryType, string hash)
        {
            return new SpanLink(TraceContext.Empty, new Dictionary<string, object?>
            {
                ["neo.inventory.type"] = inventoryType,
                ["neo.inventory.hash"] = hash
            });
        }

        /// <summary>
        /// Converts this SpanLink to an ActivityLink.
        /// </summary>
        /// <returns>An ActivityLink for use with System.Diagnostics.Activity.</returns>
        public ActivityLink ToActivityLink()
        {
            if (Attributes == null || Attributes.Count == 0)
                return new ActivityLink(Context.ActivityContext);

            var tags = new ActivityTagsCollection();
            foreach (var kvp in Attributes)
            {
                tags.Add(kvp.Key, kvp.Value);
            }

            return new ActivityLink(Context.ActivityContext, tags);
        }
    }
}
