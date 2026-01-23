// Copyright (C) 2015-2025 The Neo Project.
//
// RemoteNodeState.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

namespace Neo.Orleans.States
{
    /// <summary>
    /// Persistent state for RemoteNodeGrain.
    /// Stores connection state and peer information.
    /// </summary>
    [GenerateSerializer]
    public class RemoteNodeState
    {
        /// <summary>
        /// Default maximum number of queued outbound messages per peer.
        /// </summary>
        public const int DefaultMaxOutboundQueue = 1024;

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
        public int MaxKnownHashes { get; set; } = Neo.Network.P2P.ChannelsConfig.DefaultMaxKnownHashes;

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

        /// <summary>
        /// Whether a version message has been sent to this peer.
        /// </summary>
        [Id(15)]
        public bool VersionSent { get; set; }

        /// <summary>
        /// Maximum number of queued outbound messages.
        /// </summary>
        [Id(16)]
        public int MaxOutboundQueue { get; set; } = DefaultMaxOutboundQueue;

        /// <summary>
        /// Whether payload compression is enabled for this connection.
        /// </summary>
        [Id(17)]
        public bool EnableCompression { get; set; } = Neo.Network.P2P.ChannelsConfig.DefaultEnableCompression;

        /// <summary>
        /// Whether a mempool request has been sent for this session.
        /// </summary>
        [Id(18)]
        public bool MempoolSent { get; set; }

        /// <summary>
        /// Whether a version message has been received from this peer.
        /// </summary>
        [Id(19)]
        public bool VersionReceived { get; set; }

        /// <summary>
        /// Accumulated misbehavior score for this peer.
        /// </summary>
        [Id(20)]
        public int MisbehaviorScore { get; set; }

        /// <summary>
        /// Whether a GetAddr request has been sent to this peer.
        /// </summary>
        [Id(21)]
        public bool GetAddrSent { get; set; }
    }
}
