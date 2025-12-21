// Copyright (C) 2015-2025 The Neo Project.
//
// ConsensusMetrics.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using System;

namespace Neo.Observability.Metrics
{
    /// <summary>
    /// Provides pre-defined metrics for dBFT consensus operations.
    /// </summary>
    public sealed class ConsensusMetrics
    {
        private readonly IMetricsProvider _provider;

        // Consensus round metrics
        private readonly ICounter _consensusRoundsTotal;
        private readonly ICounter _consensusRoundsSuccessful;
        private readonly ICounter _consensusRoundsFailed;
        private readonly IHistogram _consensusRoundDuration;
        private readonly IGauge _currentView;
        private readonly IGauge _currentPrimaryIndex;

        // Message metrics
        private readonly ICounter _prepareRequestsSent;
        private readonly ICounter _prepareRequestsReceived;
        private readonly ICounter _prepareResponsesSent;
        private readonly ICounter _prepareResponsesReceived;
        private readonly ICounter _commitsSent;
        private readonly ICounter _commitsReceived;
        private readonly ICounter _changeViewsSent;
        private readonly ICounter _changeViewsReceived;
        private readonly ICounter _recoveryMessagesSent;
        private readonly ICounter _recoveryMessagesReceived;

        // Validator metrics
        private readonly IGauge _validatorCount;
        private readonly IGauge _activeValidators;
        private readonly IGauge _myValidatorIndex;
        private readonly ICounter _blocksProposed;
        private readonly ICounter _blocksCommitted;

        // Timing metrics
        private readonly IHistogram _prepareRequestLatency;
        private readonly IHistogram _commitLatency;
        private readonly IHistogram _blockFinalizationTime;

        // Error metrics
        private readonly ICounter _invalidMessages;
        private readonly ICounter _timeouts;
        private readonly ICounter _viewChanges;

        #region Properties

        /// <summary>
        /// Gets the total consensus rounds counter.
        /// </summary>
        public ICounter ConsensusRoundsTotal => _consensusRoundsTotal;

        /// <summary>
        /// Gets the successful consensus rounds counter.
        /// </summary>
        public ICounter ConsensusRoundsSuccessful => _consensusRoundsSuccessful;

        /// <summary>
        /// Gets the failed consensus rounds counter.
        /// </summary>
        public ICounter ConsensusRoundsFailed => _consensusRoundsFailed;

        /// <summary>
        /// Gets the consensus round duration histogram.
        /// </summary>
        public IHistogram ConsensusRoundDuration => _consensusRoundDuration;

        /// <summary>
        /// Gets the current view gauge.
        /// </summary>
        public IGauge CurrentView => _currentView;

        /// <summary>
        /// Gets the current primary index gauge.
        /// </summary>
        public IGauge CurrentPrimaryIndex => _currentPrimaryIndex;

        /// <summary>
        /// Gets the validator count gauge.
        /// </summary>
        public IGauge ValidatorCount => _validatorCount;

        /// <summary>
        /// Gets the active validators gauge.
        /// </summary>
        public IGauge ActiveValidators => _activeValidators;

        /// <summary>
        /// Gets the blocks proposed counter.
        /// </summary>
        public ICounter BlocksProposed => _blocksProposed;

        /// <summary>
        /// Gets the blocks committed counter.
        /// </summary>
        public ICounter BlocksCommitted => _blocksCommitted;

        /// <summary>
        /// Gets the invalid messages counter.
        /// </summary>
        public ICounter InvalidMessages => _invalidMessages;

        /// <summary>
        /// Gets the timeouts counter.
        /// </summary>
        public ICounter Timeouts => _timeouts;

        /// <summary>
        /// Gets the view changes counter.
        /// </summary>
        public ICounter ViewChanges => _viewChanges;

        #endregion

        /// <summary>
        /// Initializes a new instance of the ConsensusMetrics class.
        /// </summary>
        /// <param name="provider">The metrics provider to use.</param>
        public ConsensusMetrics(IMetricsProvider provider)
        {
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));

            // Consensus round metrics
            _consensusRoundsTotal = provider.CreateCounter("neo_consensus_rounds_total", "Total number of consensus rounds");
            _consensusRoundsSuccessful = provider.CreateCounter("neo_consensus_rounds_successful_total", "Number of successful consensus rounds");
            _consensusRoundsFailed = provider.CreateCounter("neo_consensus_rounds_failed_total", "Number of failed consensus rounds");
            _consensusRoundDuration = provider.CreateHistogram("neo_consensus_round_duration_seconds", "Consensus round duration in seconds",
                [0.1, 0.5, 1.0, 2.0, 5.0, 10.0, 15.0, 30.0, 60.0]);
            _currentView = provider.CreateGauge("neo_consensus_current_view", "Current consensus view number");
            _currentPrimaryIndex = provider.CreateGauge("neo_consensus_primary_index", "Current primary validator index");

            // Message metrics
            _prepareRequestsSent = provider.CreateCounter("neo_consensus_prepare_requests_sent_total", "Number of PrepareRequest messages sent");
            _prepareRequestsReceived = provider.CreateCounter("neo_consensus_prepare_requests_received_total", "Number of PrepareRequest messages received");
            _prepareResponsesSent = provider.CreateCounter("neo_consensus_prepare_responses_sent_total", "Number of PrepareResponse messages sent");
            _prepareResponsesReceived = provider.CreateCounter("neo_consensus_prepare_responses_received_total", "Number of PrepareResponse messages received");
            _commitsSent = provider.CreateCounter("neo_consensus_commits_sent_total", "Number of Commit messages sent");
            _commitsReceived = provider.CreateCounter("neo_consensus_commits_received_total", "Number of Commit messages received");
            _changeViewsSent = provider.CreateCounter("neo_consensus_change_views_sent_total", "Number of ChangeView messages sent");
            _changeViewsReceived = provider.CreateCounter("neo_consensus_change_views_received_total", "Number of ChangeView messages received");
            _recoveryMessagesSent = provider.CreateCounter("neo_consensus_recovery_messages_sent_total", "Number of Recovery messages sent");
            _recoveryMessagesReceived = provider.CreateCounter("neo_consensus_recovery_messages_received_total", "Number of Recovery messages received");

            // Validator metrics
            _validatorCount = provider.CreateGauge("neo_consensus_validator_count", "Total number of validators");
            _activeValidators = provider.CreateGauge("neo_consensus_active_validators", "Number of active validators in current round");
            _myValidatorIndex = provider.CreateGauge("neo_consensus_my_validator_index", "This node's validator index (-1 if not a validator)");
            _blocksProposed = provider.CreateCounter("neo_consensus_blocks_proposed_total", "Number of blocks proposed by this node");
            _blocksCommitted = provider.CreateCounter("neo_consensus_blocks_committed_total", "Number of blocks committed");

            // Timing metrics
            _prepareRequestLatency = provider.CreateHistogram("neo_consensus_prepare_request_latency_seconds", "Time from round start to PrepareRequest",
                [0.01, 0.05, 0.1, 0.25, 0.5, 1.0, 2.0, 5.0]);
            _commitLatency = provider.CreateHistogram("neo_consensus_commit_latency_seconds", "Time from PrepareRequest to Commit",
                [0.1, 0.25, 0.5, 1.0, 2.0, 5.0, 10.0]);
            _blockFinalizationTime = provider.CreateHistogram("neo_consensus_block_finalization_seconds", "Time to finalize a block",
                [0.1, 0.5, 1.0, 2.0, 5.0, 10.0, 15.0]);

            // Error metrics
            _invalidMessages = provider.CreateCounter("neo_consensus_invalid_messages_total", "Number of invalid consensus messages received");
            _timeouts = provider.CreateCounter("neo_consensus_timeouts_total", "Number of consensus timeouts");
            _viewChanges = provider.CreateCounter("neo_consensus_view_changes_total", "Number of view changes");
        }

        /// <summary>
        /// Records the start of a new consensus round.
        /// </summary>
        /// <param name="view">The view number.</param>
        /// <param name="primaryIndex">The primary validator index.</param>
        /// <param name="validatorCount">Total number of validators.</param>
        public void RecordRoundStart(byte view, int primaryIndex, int validatorCount)
        {
            _consensusRoundsTotal.Increment();
            _currentView.Set(view);
            _currentPrimaryIndex.Set(primaryIndex);
            _validatorCount.Set(validatorCount);
        }

        /// <summary>
        /// Records a successful consensus round completion.
        /// </summary>
        /// <param name="durationMs">Round duration in milliseconds.</param>
        public void RecordRoundSuccess(double durationMs)
        {
            _consensusRoundsSuccessful.Increment();
            _consensusRoundDuration.Observe(durationMs / 1000.0);
        }

        /// <summary>
        /// Records a failed consensus round.
        /// </summary>
        public void RecordRoundFailure()
        {
            _consensusRoundsFailed.Increment();
        }

        /// <summary>
        /// Records a PrepareRequest message.
        /// </summary>
        /// <param name="sent">True if sent, false if received.</param>
        /// <param name="latencyMs">Latency from round start in milliseconds (for received).</param>
        public void RecordPrepareRequest(bool sent, double latencyMs = 0)
        {
            if (sent)
            {
                _prepareRequestsSent.Increment();
                _blocksProposed.Increment();
            }
            else
            {
                _prepareRequestsReceived.Increment();
                if (latencyMs > 0)
                {
                    _prepareRequestLatency.Observe(latencyMs / 1000.0);
                }
            }
        }

        /// <summary>
        /// Records a PrepareResponse message.
        /// </summary>
        /// <param name="sent">True if sent, false if received.</param>
        public void RecordPrepareResponse(bool sent)
        {
            if (sent)
                _prepareResponsesSent.Increment();
            else
                _prepareResponsesReceived.Increment();
        }

        /// <summary>
        /// Records a Commit message.
        /// </summary>
        /// <param name="sent">True if sent, false if received.</param>
        /// <param name="latencyMs">Latency from PrepareRequest in milliseconds.</param>
        public void RecordCommit(bool sent, double latencyMs = 0)
        {
            if (sent)
            {
                _commitsSent.Increment();
            }
            else
            {
                _commitsReceived.Increment();
            }

            if (latencyMs > 0)
            {
                _commitLatency.Observe(latencyMs / 1000.0);
            }
        }

        /// <summary>
        /// Records a ChangeView message.
        /// </summary>
        /// <param name="sent">True if sent, false if received.</param>
        public void RecordChangeView(bool sent)
        {
            if (sent)
                _changeViewsSent.Increment();
            else
                _changeViewsReceived.Increment();

            _viewChanges.Increment();
        }

        /// <summary>
        /// Records a Recovery message.
        /// </summary>
        /// <param name="sent">True if sent, false if received.</param>
        public void RecordRecoveryMessage(bool sent)
        {
            if (sent)
                _recoveryMessagesSent.Increment();
            else
                _recoveryMessagesReceived.Increment();
        }

        /// <summary>
        /// Records a block being committed.
        /// </summary>
        /// <param name="finalizationTimeMs">Time to finalize the block in milliseconds.</param>
        public void RecordBlockCommitted(double finalizationTimeMs)
        {
            _blocksCommitted.Increment();
            _blockFinalizationTime.Observe(finalizationTimeMs / 1000.0);
        }

        /// <summary>
        /// Records an invalid consensus message.
        /// </summary>
        public void RecordInvalidMessage()
        {
            _invalidMessages.Increment();
        }

        /// <summary>
        /// Records a consensus timeout.
        /// </summary>
        public void RecordTimeout()
        {
            _timeouts.Increment();
        }

        /// <summary>
        /// Updates the active validator count.
        /// </summary>
        /// <param name="count">Number of active validators.</param>
        public void UpdateActiveValidators(int count)
        {
            _activeValidators.Set(count);
        }

        /// <summary>
        /// Sets this node's validator index.
        /// </summary>
        /// <param name="index">Validator index, or -1 if not a validator.</param>
        public void SetMyValidatorIndex(int index)
        {
            _myValidatorIndex.Set(index);
        }
    }
}
