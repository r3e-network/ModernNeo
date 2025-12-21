// Copyright (C) 2015-2025 The Neo Project.
//
// RemoteNodeState.cs file belongs to the neo project and is free
// software distributed under the MIT software license.

namespace Neo.Orleans.States;

/// <summary>
/// Persistent state for RemoteNodeGrain.
/// Stores connection state and peer information.
/// </summary>
[GenerateSerializer]
public class RemoteNodeState
{
    /// <summary>
    /// Remote peer's IP address.
    /// </summary>
    [Id(0)]
    public string Address { get; set; } = string.Empty;

    /// <summary>
    /// Remote peer's TCP port.
    /// </summary>
    [Id(1)]
    public int Port { get; set; }

    /// <summary>
    /// Remote peer's listener port (0 if not a server).
    /// </summary>
    [Id(2)]
    public int ListenerPort { get; set; }

    /// <summary>
    /// Current connection state.
    /// </summary>
    [Id(3)]
    public int ConnectionState { get; set; } // Maps to ConnectionState enum

    /// <summary>
    /// Remote peer's reported block height.
    /// </summary>
    [Id(4)]
    public uint RemoteHeight { get; set; }

    /// <summary>
    /// Whether the remote node is a full node.
    /// </summary>
    [Id(5)]
    public bool IsFullNode { get; set; }

    /// <summary>
    /// Whether version handshake is complete.
    /// </summary>
    [Id(6)]
    public bool VersionAcknowledged { get; set; }

    /// <summary>
    /// Timestamp when connection was established.
    /// </summary>
    [Id(7)]
    public long ConnectedAt { get; set; }

    /// <summary>
    /// Timestamp of last message received.
    /// </summary>
    [Id(8)]
    public long LastMessageAt { get; set; }

    /// <summary>
    /// Remote peer's user agent string.
    /// </summary>
    [Id(9)]
    public string UserAgent { get; set; } = string.Empty;

    /// <summary>
    /// Remote peer's nonce for duplicate connection detection.
    /// </summary>
    [Id(10)]
    public uint Nonce { get; set; }

    /// <summary>
    /// Known inventory hashes (Base64 encoded for serialization).
    /// </summary>
    [Id(11)]
    public HashSet<string> KnownHashes { get; set; } = new();

    /// <summary>
    /// Maximum known hashes to track.
    /// </summary>
    [Id(12)]
    public int MaxKnownHashes { get; set; } = 10000;

    /// <summary>
    /// Pending outbound messages (serialized).
    /// </summary>
    [Id(13)]
    public Queue<byte[]> OutboundQueue { get; set; } = new();

    /// <summary>
    /// Whether we're waiting for acknowledgment.
    /// </summary>
    [Id(14)]
    public bool AwaitingAck { get; set; }
}
