// Copyright (C) 2015-2025 The Neo Project.
//
// P2PTracing.cs file belongs to the neo project and is free
// software distributed under the MIT software license.

using Neo.Network.P2P;
using Neo.Observability.Tracing;
using Neo.Observability.Tracing.Propagation;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Neo.Network.Observability;

/// <summary>
/// Provides distributed tracing support for P2P network operations.
/// Uses correlation mode (span links) instead of parent-child relationships
/// to avoid protocol changes while maintaining trace correlation.
/// </summary>
public static class P2PTracing
{
    /// <summary>
    /// The ActivitySource name for P2P tracing.
    /// </summary>
    public const string ActivitySourceName = "Neo.Network.P2P";

    private static readonly ActivitySource s_activitySource = new(ActivitySourceName, "1.0.0");

    /// <summary>
    /// Gets the ActivitySource for P2P tracing.
    /// </summary>
    public static ActivitySource ActivitySource => s_activitySource;

    #region Message Handling Spans

    /// <summary>
    /// Starts a span for handling an incoming P2P message.
    /// </summary>
    /// <param name="command">The message command type.</param>
    /// <param name="peerEndpoint">The remote peer endpoint.</param>
    /// <param name="payloadSize">The payload size in bytes.</param>
    /// <returns>An Activity representing the span, or null if not sampled.</returns>
    public static Activity? StartMessageReceiveSpan(
        MessageCommand command,
        string? peerEndpoint,
        int payloadSize)
    {
        var activity = s_activitySource.StartActivity(
            $"p2p.receive.{command}",
            ActivityKind.Consumer);

        if (activity != null)
        {
            SetMessageTags(activity, command, peerEndpoint, payloadSize);
            activity.SetTag("messaging.operation", "receive");
        }

        return activity;
    }

    /// <summary>
    /// Starts a span for sending a P2P message.
    /// </summary>
    /// <param name="command">The message command type.</param>
    /// <param name="peerEndpoint">The remote peer endpoint.</param>
    /// <param name="payloadSize">The payload size in bytes.</param>
    /// <returns>An Activity representing the span, or null if not sampled.</returns>
    public static Activity? StartMessageSendSpan(
        MessageCommand command,
        string? peerEndpoint,
        int payloadSize)
    {
        var activity = s_activitySource.StartActivity(
            $"p2p.send.{command}",
            ActivityKind.Producer);

        if (activity != null)
        {
            SetMessageTags(activity, command, peerEndpoint, payloadSize);
            activity.SetTag("messaging.operation", "send");
        }

        return activity;
    }

    /// <summary>
    /// Starts a span for broadcasting a message to multiple peers.
    /// </summary>
    /// <param name="command">The message command type.</param>
    /// <param name="peerCount">The number of peers to broadcast to.</param>
    /// <param name="payloadSize">The payload size in bytes.</param>
    /// <returns>An Activity representing the span, or null if not sampled.</returns>
    public static Activity? StartBroadcastSpan(
        MessageCommand command,
        int peerCount,
        int payloadSize)
    {
        var activity = s_activitySource.StartActivity(
            $"p2p.broadcast.{command}",
            ActivityKind.Producer);

        if (activity != null)
        {
            activity.SetTag("messaging.system", "neo.p2p");
            activity.SetTag("messaging.operation", "broadcast");
            activity.SetTag("neo.p2p.command", command.ToString());
            activity.SetTag("neo.p2p.peer_count", peerCount);
            activity.SetTag("messaging.message.body.size", payloadSize);
        }

        return activity;
    }

    #endregion

    #region Inventory Correlation

    /// <summary>
    /// Adds a transaction correlation link to the current span.
    /// This creates a span link to correlate P2P message handling with transaction processing.
    /// </summary>
    /// <param name="activity">The activity to add the link to.</param>
    /// <param name="txHash">The transaction hash.</param>
    public static void AddTransactionLink(Activity? activity, string txHash)
    {
        if (activity == null || string.IsNullOrEmpty(txHash)) return;

        activity.SetTag("neo.tx.hash", txHash);
        activity.AddEvent(new ActivityEvent("tx.correlated", tags: new ActivityTagsCollection
        {
            { "neo.tx.hash", txHash }
        }));
    }

    /// <summary>
    /// Adds a block correlation link to the current span.
    /// This creates a span link to correlate P2P message handling with block processing.
    /// </summary>
    /// <param name="activity">The activity to add the link to.</param>
    /// <param name="blockHash">The block hash.</param>
    /// <param name="blockIndex">The block index.</param>
    public static void AddBlockLink(Activity? activity, string blockHash, uint blockIndex)
    {
        if (activity == null || string.IsNullOrEmpty(blockHash)) return;

        activity.SetTag("neo.block.hash", blockHash);
        activity.SetTag("neo.block.index", blockIndex);
        activity.AddEvent(new ActivityEvent("block.correlated", tags: new ActivityTagsCollection
        {
            { "neo.block.hash", blockHash },
            { "neo.block.index", blockIndex }
        }));
    }

    /// <summary>
    /// Adds an inventory correlation to the current span.
    /// </summary>
    /// <param name="activity">The activity to add the correlation to.</param>
    /// <param name="inventoryType">The inventory type (TX, Block, Consensus).</param>
    /// <param name="hash">The inventory hash.</param>
    public static void AddInventoryCorrelation(Activity? activity, string inventoryType, string hash)
    {
        if (activity == null) return;

        activity.SetTag("neo.inventory.type", inventoryType);
        activity.SetTag("neo.inventory.hash", hash);
    }

    /// <summary>
    /// Adds multiple inventory correlations to the current span.
    /// </summary>
    /// <param name="activity">The activity to add the correlations to.</param>
    /// <param name="inventoryType">The inventory type.</param>
    /// <param name="hashes">The inventory hashes.</param>
    public static void AddInventoryCorrelations(Activity? activity, string inventoryType, IEnumerable<string> hashes)
    {
        if (activity == null) return;

        var hashList = hashes.ToList();
        activity.SetTag("neo.inventory.type", inventoryType);
        activity.SetTag("neo.inventory.count", hashList.Count);

        // Add first few hashes as tags for quick reference
        for (int i = 0; i < Math.Min(hashList.Count, 5); i++)
        {
            activity.SetTag($"neo.inventory.hash.{i}", hashList[i]);
        }
    }

    #endregion

    #region Peer Connection Spans

    /// <summary>
    /// Starts a span for a peer connection attempt.
    /// </summary>
    /// <param name="endpoint">The peer endpoint.</param>
    /// <returns>An Activity representing the span, or null if not sampled.</returns>
    public static Activity? StartConnectSpan(string endpoint)
    {
        var activity = s_activitySource.StartActivity(
            "p2p.connect",
            ActivityKind.Client);

        if (activity != null)
        {
            activity.SetTag("net.peer.name", endpoint);
            activity.SetTag("neo.p2p.operation", "connect");
        }

        return activity;
    }

    /// <summary>
    /// Starts a span for accepting an incoming peer connection.
    /// </summary>
    /// <param name="endpoint">The peer endpoint.</param>
    /// <returns>An Activity representing the span, or null if not sampled.</returns>
    public static Activity? StartAcceptSpan(string endpoint)
    {
        var activity = s_activitySource.StartActivity(
            "p2p.accept",
            ActivityKind.Server);

        if (activity != null)
        {
            activity.SetTag("net.peer.name", endpoint);
            activity.SetTag("neo.p2p.operation", "accept");
        }

        return activity;
    }

    /// <summary>
    /// Starts a span for the handshake process.
    /// </summary>
    /// <param name="endpoint">The peer endpoint.</param>
    /// <returns>An Activity representing the span, or null if not sampled.</returns>
    public static Activity? StartHandshakeSpan(string endpoint)
    {
        var activity = s_activitySource.StartActivity(
            "p2p.handshake",
            ActivityKind.Internal);

        if (activity != null)
        {
            activity.SetTag("net.peer.name", endpoint);
            activity.SetTag("neo.p2p.operation", "handshake");
        }

        return activity;
    }

    /// <summary>
    /// Records a successful connection.
    /// </summary>
    /// <param name="activity">The activity to record on.</param>
    /// <param name="nodeVersion">The remote node version.</param>
    /// <param name="userAgent">The remote node user agent.</param>
    public static void RecordConnectionSuccess(Activity? activity, uint? nodeVersion = null, string? userAgent = null)
    {
        if (activity == null) return;

        activity.SetStatus(ActivityStatusCode.Ok);
        activity.SetTag("neo.p2p.connection.status", "connected");

        if (nodeVersion.HasValue)
        {
            activity.SetTag("neo.p2p.node.version", nodeVersion.Value);
        }

        if (!string.IsNullOrEmpty(userAgent))
        {
            activity.SetTag("neo.p2p.node.user_agent", userAgent);
        }
    }

    /// <summary>
    /// Records a connection failure.
    /// </summary>
    /// <param name="activity">The activity to record on.</param>
    /// <param name="reason">The failure reason.</param>
    public static void RecordConnectionFailure(Activity? activity, string reason)
    {
        if (activity == null) return;

        activity.SetStatus(ActivityStatusCode.Error, reason);
        activity.SetTag("neo.p2p.connection.status", "failed");
        activity.SetTag("neo.p2p.connection.failure_reason", reason);
    }

    /// <summary>
    /// Records a peer disconnection.
    /// </summary>
    /// <param name="activity">The activity to record on.</param>
    /// <param name="reason">The disconnection reason.</param>
    public static void RecordDisconnection(Activity? activity, string? reason)
    {
        if (activity == null) return;

        activity.SetTag("neo.p2p.connection.status", "disconnected");
        if (!string.IsNullOrEmpty(reason))
        {
            activity.SetTag("neo.p2p.disconnection.reason", reason);
        }
    }

    #endregion

    #region Synchronization Spans

    /// <summary>
    /// Starts a span for block synchronization.
    /// </summary>
    /// <param name="startIndex">The starting block index.</param>
    /// <param name="endIndex">The ending block index.</param>
    /// <returns>An Activity representing the span, or null if not sampled.</returns>
    public static Activity? StartBlockSyncSpan(uint startIndex, uint endIndex)
    {
        var activity = s_activitySource.StartActivity(
            "p2p.sync.blocks",
            ActivityKind.Internal);

        if (activity != null)
        {
            activity.SetTag("neo.sync.start_index", startIndex);
            activity.SetTag("neo.sync.end_index", endIndex);
            activity.SetTag("neo.sync.block_count", endIndex - startIndex + 1);
        }

        return activity;
    }

    /// <summary>
    /// Starts a span for header synchronization.
    /// </summary>
    /// <param name="startIndex">The starting header index.</param>
    /// <param name="count">The number of headers requested.</param>
    /// <returns>An Activity representing the span, or null if not sampled.</returns>
    public static Activity? StartHeaderSyncSpan(uint startIndex, int count)
    {
        var activity = s_activitySource.StartActivity(
            "p2p.sync.headers",
            ActivityKind.Internal);

        if (activity != null)
        {
            activity.SetTag("neo.sync.start_index", startIndex);
            activity.SetTag("neo.sync.header_count", count);
        }

        return activity;
    }

    /// <summary>
    /// Records synchronization progress.
    /// </summary>
    /// <param name="activity">The activity to record on.</param>
    /// <param name="currentIndex">The current sync index.</param>
    /// <param name="totalBlocks">The total blocks to sync.</param>
    public static void RecordSyncProgress(Activity? activity, uint currentIndex, uint totalBlocks)
    {
        if (activity == null) return;

        activity.SetTag("neo.sync.current_index", currentIndex);
        activity.SetTag("neo.sync.total_blocks", totalBlocks);

        var progress = totalBlocks > 0 ? (double)currentIndex / totalBlocks * 100 : 0;
        activity.SetTag("neo.sync.progress_percent", Math.Round(progress, 2));
    }

    #endregion

    #region Helper Methods

    private static void SetMessageTags(Activity activity, MessageCommand command, string? peerEndpoint, int payloadSize)
    {
        activity.SetTag("messaging.system", "neo.p2p");
        activity.SetTag("neo.p2p.command", command.ToString());
        activity.SetTag("messaging.message.body.size", payloadSize);

        if (!string.IsNullOrEmpty(peerEndpoint))
        {
            activity.SetTag("net.peer.name", peerEndpoint);
        }
    }

    /// <summary>
    /// Records an exception on the span.
    /// </summary>
    /// <param name="activity">The activity to record on.</param>
    /// <param name="exception">The exception that occurred.</param>
    public static void RecordException(Activity? activity, Exception exception)
    {
        if (activity == null) return;

        activity.SetStatus(ActivityStatusCode.Error, exception.Message);
        activity.AddEvent(new ActivityEvent("exception", tags: new ActivityTagsCollection
        {
            { "exception.type", exception.GetType().FullName },
            { "exception.message", exception.Message },
            { "exception.stacktrace", exception.StackTrace }
        }));
    }

    /// <summary>
    /// Records a successful operation.
    /// </summary>
    /// <param name="activity">The activity to record on.</param>
    public static void RecordSuccess(Activity? activity)
    {
        activity?.SetStatus(ActivityStatusCode.Ok);
    }

    #endregion
}
