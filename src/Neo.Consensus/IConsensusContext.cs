// Copyright (C) 2015-2025 The Neo Project.
//
// IConsensusContext.cs file belongs to the neo project and is free
// software distributed under the MIT software license.

using Neo.Core.Interfaces;

namespace Neo.Consensus;

/// <summary>
/// Represents the context for a consensus round.
/// This is a dependency-free abstraction for consensus state.
/// </summary>
public interface IConsensusContext
{
    /// <summary>
    /// Gets the current block index being proposed.
    /// </summary>
    uint BlockIndex { get; }

    /// <summary>
    /// Gets the current view number.
    /// </summary>
    byte ViewNumber { get; }

    /// <summary>
    /// Gets the current consensus phase.
    /// </summary>
    ConsensusPhase Phase { get; }

    /// <summary>
    /// Gets the index of this node in the validator list.
    /// </summary>
    int MyIndex { get; }

    /// <summary>
    /// Gets the total number of validators.
    /// </summary>
    int ValidatorCount { get; }

    /// <summary>
    /// Gets the minimum number of validators required for consensus (M = 2F + 1).
    /// </summary>
    int M { get; }

    /// <summary>
    /// Gets the maximum number of faulty nodes tolerated (F = (N - 1) / 3).
    /// </summary>
    int F { get; }

    /// <summary>
    /// Gets whether this node is the primary (speaker) for the current view.
    /// </summary>
    bool IsPrimary { get; }

    /// <summary>
    /// Gets the index of the primary validator for the current view.
    /// </summary>
    int PrimaryIndex { get; }

    /// <summary>
    /// Gets the hash of the previous block.
    /// </summary>
    byte[] PrevHash { get; }

    /// <summary>
    /// Gets the proposed block data, if any.
    /// </summary>
    IBlockData? ProposedBlock { get; }

    /// <summary>
    /// Gets the timestamp when the current view started.
    /// </summary>
    long ViewStartTime { get; }

    /// <summary>
    /// Gets the number of prepare responses received.
    /// </summary>
    int PrepareResponseCount { get; }

    /// <summary>
    /// Gets the number of commit messages received.
    /// </summary>
    int CommitCount { get; }

    /// <summary>
    /// Checks if enough prepare responses have been received.
    /// </summary>
    bool HasEnoughPrepareResponses { get; }

    /// <summary>
    /// Checks if enough commits have been received.
    /// </summary>
    bool HasEnoughCommits { get; }

    /// <summary>
    /// Gets the list of validator public keys.
    /// </summary>
    IReadOnlyList<byte[]>? Validators { get; }

    /// <summary>
    /// Gets whether a prepare request has been received for this view.
    /// </summary>
    bool PrepareRequestReceived { get; }

    /// <summary>
    /// Resets the context for a new consensus round.
    /// </summary>
    /// <param name="blockIndex">The block index for the new round.</param>
    /// <param name="viewNumber">The view number for the new round.</param>
    void Reset(uint blockIndex, byte viewNumber);

    /// <summary>
    /// Creates a prepare request message (primary only).
    /// </summary>
    /// <returns>The prepare request message, or null if unable to create.</returns>
    IConsensusMessage? CreatePrepareRequest();

    /// <summary>
    /// Called when a prepare request is received.
    /// </summary>
    void OnPrepareRequestReceived();

    /// <summary>
    /// Gets the validator index for a given public key.
    /// </summary>
    /// <param name="publicKey">The validator's public key.</param>
    /// <returns>The validator index, or -1 if not found.</returns>
    int GetValidatorIndex(byte[] publicKey);

    /// <summary>
    /// Validates a prepare request message.
    /// </summary>
    /// <param name="message">The prepare request message to validate.</param>
    /// <returns>True if valid; otherwise, false.</returns>
    bool ValidatePrepareRequest(IConsensusMessage message);

    /// <summary>
    /// Creates a prepare response message (backup only).
    /// </summary>
    /// <returns>The prepare response message, or null if unable to create.</returns>
    IConsensusMessage? CreatePrepareResponse();

    /// <summary>
    /// Called when a prepare response is received.
    /// </summary>
    /// <param name="validatorIndex">The index of the validator that sent the response.</param>
    /// <param name="message">The prepare response message.</param>
    void OnPrepareResponseReceived(int validatorIndex, IConsensusMessage message);

    /// <summary>
    /// Creates a commit message.
    /// </summary>
    /// <returns>The commit message, or null if unable to create.</returns>
    IConsensusMessage? CreateCommit();

    /// <summary>
    /// Called when a commit message is received.
    /// </summary>
    /// <param name="validatorIndex">The index of the validator that sent the commit.</param>
    /// <param name="message">The commit message.</param>
    void OnCommitReceived(int validatorIndex, IConsensusMessage message);

    /// <summary>
    /// Gets the hash of the proposed block.
    /// </summary>
    /// <returns>The block hash, or null if no block is proposed.</returns>
    byte[]? GetBlockHash();

    /// <summary>
    /// Gets the commit signatures collected so far.
    /// </summary>
    /// <returns>The list of signatures, or null if not enough commits.</returns>
    IReadOnlyList<byte[]>? GetCommitSignatures();

    /// <summary>
    /// Called when a change view message is received.
    /// </summary>
    /// <param name="validatorIndex">The index of the validator requesting view change.</param>
    /// <param name="message">The change view message.</param>
    void OnChangeViewReceived(int validatorIndex, IConsensusMessage message);

    /// <summary>
    /// Gets the number of change view requests for a specific view number.
    /// </summary>
    /// <param name="newViewNumber">The target view number.</param>
    /// <returns>The count of change view requests.</returns>
    int GetChangeViewCount(byte newViewNumber);

    /// <summary>
    /// Creates a change view message.
    /// </summary>
    /// <param name="newViewNumber">The target view number.</param>
    /// <returns>The change view message, or null if unable to create.</returns>
    IConsensusMessage? CreateChangeView(byte newViewNumber);

    /// <summary>
    /// Creates a recovery message containing current consensus state.
    /// </summary>
    /// <returns>The recovery message, or null if unable to create.</returns>
    IConsensusMessage? CreateRecoveryMessage();

    /// <summary>
    /// Applies state from a recovery message.
    /// </summary>
    /// <param name="message">The recovery message to apply.</param>
    void ApplyRecoveryMessage(IConsensusMessage message);
}

/// <summary>
/// Represents a snapshot of consensus state for external queries.
/// </summary>
public record ConsensusStateSnapshot(
    uint BlockIndex,
    byte ViewNumber,
    ConsensusPhase Phase,
    bool IsPrimary,
    int ValidatorCount,
    int PrepareResponseCount,
    int CommitCount,
    bool IsRunning);
