// Copyright (C) 2015-2025 The Neo Project.
//
// ConsensusTracing.cs file belongs to the neo project and is free
// software distributed under the MIT software license.

using Neo.Observability.Tracing.Propagation;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Neo.Observability.Tracing;

/// <summary>
/// Provides distributed tracing support for dBFT consensus operations.
/// </summary>
public static class ConsensusTracing
{
    /// <summary>
    /// The ActivitySource name for consensus tracing.
    /// </summary>
    public const string ActivitySourceName = "Neo.Consensus";

    private static readonly ActivitySource s_activitySource = new(ActivitySourceName, "1.0.0");

    /// <summary>
    /// Gets the ActivitySource for consensus tracing.
    /// </summary>
    public static ActivitySource ActivitySource => s_activitySource;

    #region Consensus Round Spans

    /// <summary>
    /// Starts a span for a consensus round.
    /// </summary>
    /// <param name="blockIndex">The block index being proposed.</param>
    /// <param name="viewNumber">The current view number.</param>
    /// <param name="validatorIndex">The local validator index.</param>
    /// <returns>An Activity representing the span, or null if not sampled.</returns>
    public static Activity? StartConsensusRoundSpan(uint blockIndex, byte viewNumber, int validatorIndex)
    {
        var activity = s_activitySource.StartActivity(
            "consensus.round",
            ActivityKind.Internal);

        if (activity != null)
        {
            activity.SetTag("neo.consensus.block_index", blockIndex);
            activity.SetTag("neo.consensus.view_number", viewNumber);
            activity.SetTag("neo.consensus.validator_index", validatorIndex);
        }

        return activity;
    }

    /// <summary>
    /// Starts a span for the primary proposing a block.
    /// </summary>
    /// <param name="blockIndex">The block index being proposed.</param>
    /// <param name="viewNumber">The current view number.</param>
    /// <param name="transactionCount">The number of transactions in the proposed block.</param>
    /// <returns>An Activity representing the span, or null if not sampled.</returns>
    public static Activity? StartPrepareRequestSpan(uint blockIndex, byte viewNumber, int transactionCount)
    {
        var activity = s_activitySource.StartActivity(
            "consensus.prepare_request",
            ActivityKind.Producer);

        if (activity != null)
        {
            activity.SetTag("neo.consensus.block_index", blockIndex);
            activity.SetTag("neo.consensus.view_number", viewNumber);
            activity.SetTag("neo.consensus.tx_count", transactionCount);
            activity.SetTag("neo.consensus.message_type", "PrepareRequest");
        }

        return activity;
    }

    /// <summary>
    /// Starts a span for a backup validator responding to a prepare request.
    /// </summary>
    /// <param name="blockIndex">The block index.</param>
    /// <param name="viewNumber">The current view number.</param>
    /// <param name="validatorIndex">The responding validator index.</param>
    /// <returns>An Activity representing the span, or null if not sampled.</returns>
    public static Activity? StartPrepareResponseSpan(uint blockIndex, byte viewNumber, int validatorIndex)
    {
        var activity = s_activitySource.StartActivity(
            "consensus.prepare_response",
            ActivityKind.Consumer);

        if (activity != null)
        {
            activity.SetTag("neo.consensus.block_index", blockIndex);
            activity.SetTag("neo.consensus.view_number", viewNumber);
            activity.SetTag("neo.consensus.validator_index", validatorIndex);
            activity.SetTag("neo.consensus.message_type", "PrepareResponse");
        }

        return activity;
    }

    /// <summary>
    /// Starts a span for sending a commit message.
    /// </summary>
    /// <param name="blockIndex">The block index.</param>
    /// <param name="viewNumber">The current view number.</param>
    /// <param name="validatorIndex">The committing validator index.</param>
    /// <returns>An Activity representing the span, or null if not sampled.</returns>
    public static Activity? StartCommitSpan(uint blockIndex, byte viewNumber, int validatorIndex)
    {
        var activity = s_activitySource.StartActivity(
            "consensus.commit",
            ActivityKind.Internal);

        if (activity != null)
        {
            activity.SetTag("neo.consensus.block_index", blockIndex);
            activity.SetTag("neo.consensus.view_number", viewNumber);
            activity.SetTag("neo.consensus.validator_index", validatorIndex);
            activity.SetTag("neo.consensus.message_type", "Commit");
        }

        return activity;
    }

    /// <summary>
    /// Starts a span for a view change operation.
    /// </summary>
    /// <param name="blockIndex">The block index.</param>
    /// <param name="oldView">The old view number.</param>
    /// <param name="newView">The new view number.</param>
    /// <param name="reason">The reason for view change.</param>
    /// <returns>An Activity representing the span, or null if not sampled.</returns>
    public static Activity? StartViewChangeSpan(uint blockIndex, byte oldView, byte newView, string? reason = null)
    {
        var activity = s_activitySource.StartActivity(
            "consensus.view_change",
            ActivityKind.Internal);

        if (activity != null)
        {
            activity.SetTag("neo.consensus.block_index", blockIndex);
            activity.SetTag("neo.consensus.old_view", oldView);
            activity.SetTag("neo.consensus.new_view", newView);
            activity.SetTag("neo.consensus.message_type", "ChangeView");

            if (!string.IsNullOrEmpty(reason))
            {
                activity.SetTag("neo.consensus.view_change_reason", reason);
            }
        }

        return activity;
    }

    #endregion

    #region Phase Tracking

    /// <summary>
    /// Records a consensus phase transition.
    /// </summary>
    /// <param name="activity">The activity to record on.</param>
    /// <param name="fromPhase">The previous phase.</param>
    /// <param name="toPhase">The new phase.</param>
    public static void RecordPhaseTransition(Activity? activity, string fromPhase, string toPhase)
    {
        if (activity == null) return;

        activity.SetTag("neo.consensus.phase", toPhase);
        activity.AddEvent(new ActivityEvent("phase.transition", tags: new ActivityTagsCollection
        {
            { "neo.consensus.from_phase", fromPhase },
            { "neo.consensus.to_phase", toPhase }
        }));
    }

    /// <summary>
    /// Records the number of signatures collected.
    /// </summary>
    /// <param name="activity">The activity to record on.</param>
    /// <param name="prepareCount">Number of prepare responses received.</param>
    /// <param name="commitCount">Number of commit messages received.</param>
    /// <param name="requiredCount">Number of signatures required for consensus.</param>
    public static void RecordSignatureProgress(Activity? activity, int prepareCount, int commitCount, int requiredCount)
    {
        if (activity == null) return;

        activity.SetTag("neo.consensus.prepare_count", prepareCount);
        activity.SetTag("neo.consensus.commit_count", commitCount);
        activity.SetTag("neo.consensus.required_count", requiredCount);
    }

    #endregion

    #region Block Correlation

    /// <summary>
    /// Adds block correlation to the consensus span.
    /// </summary>
    /// <param name="activity">The activity to add correlation to.</param>
    /// <param name="blockHash">The block hash.</param>
    /// <param name="blockIndex">The block index.</param>
    public static void AddBlockCorrelation(Activity? activity, string blockHash, uint blockIndex)
    {
        if (activity == null || string.IsNullOrEmpty(blockHash)) return;

        activity.SetTag("neo.block.hash", blockHash);
        activity.SetTag("neo.block.index", blockIndex);
        activity.AddEvent(new ActivityEvent("block.proposed", tags: new ActivityTagsCollection
        {
            { "neo.block.hash", blockHash },
            { "neo.block.index", blockIndex }
        }));
    }

    /// <summary>
    /// Records consensus completion with block details.
    /// </summary>
    /// <param name="activity">The activity to record on.</param>
    /// <param name="blockHash">The committed block hash.</param>
    /// <param name="transactionCount">Number of transactions in the block.</param>
    /// <param name="consensusDurationMs">Time taken to reach consensus in milliseconds.</param>
    public static void RecordConsensusComplete(Activity? activity, string blockHash, int transactionCount, double consensusDurationMs)
    {
        if (activity == null) return;

        activity.SetStatus(ActivityStatusCode.Ok);
        activity.SetTag("neo.block.hash", blockHash);
        activity.SetTag("neo.consensus.tx_count", transactionCount);
        activity.SetTag("neo.consensus.duration_ms", consensusDurationMs);
        activity.AddEvent(new ActivityEvent("consensus.complete", tags: new ActivityTagsCollection
        {
            { "neo.block.hash", blockHash },
            { "neo.consensus.tx_count", transactionCount }
        }));
    }

    #endregion

    #region Error Recording

    /// <summary>
    /// Records a consensus failure.
    /// </summary>
    /// <param name="activity">The activity to record on.</param>
    /// <param name="reason">The failure reason.</param>
    public static void RecordConsensusFailure(Activity? activity, string reason)
    {
        if (activity == null) return;

        activity.SetStatus(ActivityStatusCode.Error, reason);
        activity.SetTag("neo.consensus.failure_reason", reason);
        activity.AddEvent(new ActivityEvent("consensus.failed", tags: new ActivityTagsCollection
        {
            { "neo.consensus.failure_reason", reason }
        }));
    }

    /// <summary>
    /// Records an exception during consensus.
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

    #endregion
}
