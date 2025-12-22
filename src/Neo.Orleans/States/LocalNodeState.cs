// Copyright (C) 2015-2025 The Neo Project.
//
// LocalNodeState.cs file belongs to the neo project and is free
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
    /// Persistent state for LocalNodeGrain.
    /// Mirrors Akka.NET LocalNode Actor with seed nodes and unconnected peer pool.
    /// </summary>
    [GenerateSerializer]
    public class LocalNodeState
    {
        /// <summary>
        /// Connected peer addresses mapped to their info.
        /// Key: "address:port"
        /// </summary>
        [Id(0)] public Dictionary<string, ConnectedPeerState> ConnectedPeers { get; set; } = new();

        /// <summary>
        /// Maximum allowed peer connections.
        /// </summary>
        [Id(1)] public int MaxConnections { get; set; } = 10;

        /// <summary>
        /// Recently relayed inventory hashes to prevent duplicate relay.
        /// </summary>
        [Id(2)] public HashSet<string> RecentRelays { get; set; } = new();

        /// <summary>
        /// Maximum size of recent relays cache.
        /// </summary>
        [Id(3)] public int MaxRecentRelays { get; set; } = 1000;

        /// <summary>
        /// Local node nonce for identifying this node.
        /// </summary>
        [Id(4)] public uint Nonce { get; set; }

        /// <summary>
        /// User agent string for this node.
        /// </summary>
        [Id(5)] public string UserAgent { get; set; } = string.Empty;

        /// <summary>
        /// Seed node addresses from configuration.
        /// </summary>
        [Id(6)] public List<string> SeedList { get; set; } = new();

        /// <summary>
        /// Unconnected peers waiting to be connected.
        /// </summary>
        [Id(7)] public HashSet<string> UnconnectedPeers { get; set; } = new();

        /// <summary>
        /// Maximum unconnected peers to track.
        /// </summary>
        [Id(8)] public int MaxUnconnectedPeers { get; set; } = 1000;

        /// <summary>
        /// TCP listener port (0 if not listening).
        /// </summary>
        [Id(9)] public int ListenerPort { get; set; }

        /// <summary>
        /// Network magic number for protocol identification.
        /// </summary>
        [Id(10)] public uint NetworkMagic { get; set; }

        /// <summary>
        /// Whether the node is started and accepting connections.
        /// </summary>
        [Id(11)] public bool IsStarted { get; set; }

        /// <summary>
        /// Protocol version.
        /// </summary>
        [Id(12)] public uint ProtocolVersion { get; set; }
    }

    /// <summary>
    /// State for a connected peer.
    /// </summary>
    [GenerateSerializer]
    public class ConnectedPeerState
    {
        [Id(0)] public string Address { get; set; } = string.Empty;
        [Id(1)] public int Port { get; set; }
        [Id(2)] public uint Height { get; set; }
        [Id(3)] public long ConnectedAt { get; set; }
        [Id(4)] public long LastSeen { get; set; }
        [Id(5)] public uint Nonce { get; set; }
        [Id(6)] public string UserAgent { get; set; } = string.Empty;
        [Id(7)] public bool IsFullNode { get; set; }
        [Id(8)] public int ListenerPort { get; set; }
    }
}
