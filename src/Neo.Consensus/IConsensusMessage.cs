// Copyright (C) 2015-2025 The Neo Project.
//
// IConsensusMessage.cs file belongs to the neo project and is free
// software distributed under the MIT software license.

namespace Neo.Consensus;

/// <summary>
/// Represents a consensus message in the dBFT protocol.
/// This is a dependency-free abstraction for consensus messages.
/// </summary>
public interface IConsensusMessage
{
    /// <summary>
    /// Gets the message type.
    /// </summary>
    ConsensusMessageType Type { get; }

    /// <summary>
    /// Gets the block index this message relates to.
    /// </summary>
    uint BlockIndex { get; }

    /// <summary>
    /// Gets the view number.
    /// </summary>
    byte ViewNumber { get; }

    /// <summary>
    /// Gets the validator index of the sender.
    /// </summary>
    int ValidatorIndex { get; }

    /// <summary>
    /// Gets the message timestamp.
    /// </summary>
    ulong Timestamp { get; }

    /// <summary>
    /// Gets the raw payload data.
    /// </summary>
    ReadOnlyMemory<byte> Payload { get; }
}

/// <summary>
/// Represents a prepare request message from the primary.
/// </summary>
public interface IPrepareRequest : IConsensusMessage
{
    /// <summary>
    /// Gets the nonce for the proposed block.
    /// </summary>
    ulong Nonce { get; }

    /// <summary>
    /// Gets the transaction hashes included in the proposed block.
    /// </summary>
    IReadOnlyList<byte[]> TransactionHashes { get; }
}

/// <summary>
/// Represents a prepare response message from a backup validator.
/// </summary>
public interface IPrepareResponse : IConsensusMessage
{
    /// <summary>
    /// Gets the hash of the prepare request being responded to.
    /// </summary>
    byte[] PreparationHash { get; }
}

/// <summary>
/// Represents a commit message with signature.
/// </summary>
public interface ICommitMessage : IConsensusMessage
{
    /// <summary>
    /// Gets the signature for the proposed block.
    /// </summary>
    byte[] Signature { get; }
}

/// <summary>
/// Represents a change view request.
/// </summary>
public interface IChangeViewMessage : IConsensusMessage
{
    /// <summary>
    /// Gets the new view number being requested.
    /// </summary>
    byte NewViewNumber { get; }

    /// <summary>
    /// Gets the reason for the view change request.
    /// </summary>
    ViewChangeReason Reason { get; }
}

/// <summary>
/// Represents a recovery request message.
/// </summary>
public interface IRecoveryRequest : IConsensusMessage
{
    /// <summary>
    /// Gets the timestamp of the request.
    /// </summary>
    new ulong Timestamp { get; }
}

/// <summary>
/// Represents a recovery message containing missed consensus data.
/// </summary>
public interface IRecoveryMessage : IConsensusMessage
{
    /// <summary>
    /// Gets the prepare request payload if available.
    /// </summary>
    ReadOnlyMemory<byte>? PrepareRequestPayload { get; }

    /// <summary>
    /// Gets the prepare response payloads.
    /// </summary>
    IReadOnlyDictionary<int, byte[]> PrepareResponsePayloads { get; }

    /// <summary>
    /// Gets the commit payloads.
    /// </summary>
    IReadOnlyDictionary<int, byte[]> CommitPayloads { get; }

    /// <summary>
    /// Gets the change view payloads.
    /// </summary>
    IReadOnlyDictionary<int, byte[]> ChangeViewPayloads { get; }
}
