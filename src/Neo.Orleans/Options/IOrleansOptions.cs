// Copyright (C) 2015-2025 The Neo Project.
// 
// IOrleansOptions.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
// 
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo;

namespace Neo.Orleans.Options
{
    /// <summary>
    /// Orleans-specific configuration options for Neo Orleans.
    /// </summary>
    public interface IOrleansOptions
    {
        /// <summary>
        /// Cluster ID for Orleans cluster.
        /// Default: "neo-cluster"
        /// </summary>
        string ClusterId { get; }

        /// <summary>
        /// Service ID for Orleans service.
        /// Default: "neo-service"
        /// </summary>
        string ServiceId { get; }

        /// <summary>
        /// Time after which idle grains are deactivated.
        /// Default: 2 hours
        /// </summary>
        TimeSpan GrainCollectionAge { get; }

        /// <summary>
        /// Validation mode for Orleans ingress.
        /// Default: Full
        /// </summary>
        NeoValidationMode ValidationMode { get; }

        /// <summary>
        /// TCP listener port for inbound P2P connections.
        /// Default: 0 (disabled)
        /// </summary>
        int TcpPort { get; }

        /// <summary>
        /// User agent string advertised during handshake.
        /// Default: "/Neo:{version}/" derived from the Neo assembly.
        /// </summary>
        string UserAgent { get; }

        /// <summary>
        /// Seed list used for initial peer discovery.
        /// Defaults to N3 mainnet seeds.
        /// </summary>
        IReadOnlyList<string> SeedList { get; }

        /// <summary>
        /// Protocol version advertised during handshake.
        /// Default: 0
        /// </summary>
        uint ProtocolVersion { get; }

        /// <summary>
        /// Maximum transactions in memory pool.
        /// Default: 50000
        /// </summary>
        int MaxMemoryPoolSize { get; }

        /// <summary>
        /// TCP listener bind address (optional).
        /// Defaults to IPAddress.Any when not specified.
        /// </summary>
        string? TcpBindAddress { get; }

        /// <summary>
        /// Maximum number of concurrent connections per LocalNodeGrain.
        /// Default: 40
        /// </summary>
        int MaxConnections { get; }

        /// <summary>
        /// Whether to enable payload compression for P2P messages.
        /// Default: true
        /// </summary>
        bool EnableCompression { get; }

        /// <summary>
        /// Minimum number of desired connections to maintain.
        /// Default: 10
        /// </summary>
        int MinDesiredConnections { get; }

        /// <summary>
        /// Maximum number of connections per remote address.
        /// Default: 3
        /// </summary>
        int MaxConnectionsPerAddress { get; }

        /// <summary>
        /// WebSocket listener port.
        /// Default: 0 (disabled)
        /// </summary>
        int WsPort { get; }

        /// <summary>
        /// Whether to enable WebSocket listener for inbound P2P connections.
        /// Default: false
        /// </summary>
        bool WsEnabled { get; }

        /// <summary>
        /// Network magic number used for P2P handshake.
        /// Defaults to N3 mainnet.
        /// </summary>
        uint NetworkMagic { get; }

        /// <summary>
        /// Full protocol settings for transaction and block validation.
        /// </summary>
        IProtocolSettings? ProtocolSettings { get; }

        /// <summary>
        /// Storage provider name (MemoryStore, LevelDB, RocksDB).
        /// Default: MemoryStore.
        /// </summary>
        string StorageEngine { get; }

        /// <summary>
        /// Storage path template for Neo system state.
        /// Default: Data_{0}.
        /// </summary>
        string? StoragePath { get; }

        /// <summary>
        /// Whether to use in-memory storage (for development/testing).
        /// Default: true
        /// </summary>
        bool UseMemoryStorage { get; }

        /// <summary>
        /// Connection string for persistent storage (when UseMemoryStorage is false).
        /// </summary>
        string? StorageConnectionString { get; }

        /// <summary>
        /// Whether to enable QUIC listener for inbound P2P connections.
        /// Default: false
        /// </summary>
        bool QuicEnabled { get; }

        /// <summary>
        /// QUIC listener port.
        /// Default: 0 (disabled)
        /// </summary>
        int QuicPort { get; }

        /// <summary>
        /// QUIC ALPN string.
        /// Default: "neo-p2p"
        /// </summary>
        string QuicAlpn { get; }

        /// <summary>
        /// Optional certificate path for QUIC listener.
        /// </summary>
        string? QuicCertificatePath { get; }

        /// <summary>
        /// Optional certificate password for QUIC listener.
        /// </summary>
        string? QuicCertificatePassword { get; }

        /// <summary>
        /// Maximum known inventory hashes per peer.
        /// Default: 1000
        /// </summary>
        int MaxKnownHashes { get; }
    }
}
