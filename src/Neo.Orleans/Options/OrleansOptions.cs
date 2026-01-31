// Copyright (C) 2015-2025 The Neo Project.
//
// OrleansOptions.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo;
using System.Collections.Generic;

namespace Neo.Orleans.Options
{
    /// <summary>
    /// Orleans-specific configuration options for Neo Orleans.
    /// </summary>
    public sealed class OrleansOptions : IOrleansOptions
    {
        /// <summary>
        /// Cluster ID for Orleans cluster.
        /// Default: "neo-cluster"
        /// </summary>
        public string ClusterId { get; set; } = "neo-cluster";

        /// <summary>
        /// Service ID for Orleans service.
        /// Default: "neo-service"
        /// </summary>
        public string ServiceId { get; set; } = "neo-service";

        /// <summary>
        /// Time after which idle grains are deactivated.
        /// Default: 2 hours
        /// </summary>
        public TimeSpan GrainCollectionAge { get; set; } = TimeSpan.FromHours(2);

        /// <summary>
        /// Validation mode for Orleans ingress.
        /// Default: Full
        /// </summary>
        public NeoValidationMode ValidationMode { get; set; } = NeoValidationMode.Full;

        /// <summary>
        /// TCP listener port for inbound P2P connections.
        /// Default: 0 (disabled)
        /// </summary>
        public int TcpPort { get; set; }

        /// <summary>
        /// User agent string advertised during handshake.
        /// Default: "/Neo:{version}/" derived from the Neo assembly.
        /// </summary>
        public string UserAgent { get; set; } = $"/Neo:{typeof(NeoSystem).Assembly.GetName().Version?.ToString(3) ?? "0.0.0"}/";

        /// <summary>
        /// Seed list used for initial peer discovery.
        /// Defaults to N3 mainnet seeds.
        /// </summary>
        public IReadOnlyList<string> SeedList { get; set; } = new List<string>
        {
            "seed1.neo.org:10333",
            "seed2.neo.org:10333",
            "seed3.neo.org:10333",
            "seed4.neo.org:10333",
            "seed5.neo.org:10333"
        };

        /// <summary>
        /// Protocol version advertised during handshake.
        /// Default: 0
        /// </summary>
        public uint ProtocolVersion { get; set; }

        /// <summary>
        /// Maximum transactions in memory pool.
        /// Default: 50000
        /// </summary>
        public int MaxMemoryPoolSize { get; set; } = 50000;

        /// <summary>
        /// TCP listener bind address (optional).
        /// Defaults to IPAddress.Any when not specified.
        /// </summary>
        public string? TcpBindAddress { get; set; }

        /// <summary>
        /// Maximum number of concurrent connections per LocalNodeGrain.
        /// Default: 40
        /// </summary>
        public int MaxConnections { get; set; } = 40;

        /// <summary>
        /// Whether to enable payload compression for P2P messages.
        /// Default: true
        /// </summary>
        public bool EnableCompression { get; set; } = true;

        /// <summary>
        /// Minimum number of desired connections to maintain.
        /// Default: 10
        /// </summary>
        public int MinDesiredConnections { get; set; } = 10;

        /// <summary>
        /// Maximum number of connections per remote address.
        /// Default: 3
        /// </summary>
        public int MaxConnectionsPerAddress { get; set; } = 3;

        /// <summary>
        /// WebSocket listener port.
        /// Default: 0 (disabled)
        /// </summary>
        public int WsPort { get; set; }

        /// <summary>
        /// Whether to enable WebSocket listener for inbound P2P connections.
        /// Default: false
        /// </summary>
        public bool WsEnabled { get; set; }

        /// <summary>
        /// Network magic number used for P2P handshake.
        /// Defaults to N3 mainnet.
        /// </summary>
        public uint NetworkMagic { get; set; } = 5195086;

        /// <summary>
        /// Full protocol settings for transaction and block validation.
        /// </summary>
        public IProtocolSettings? ProtocolSettings { get; set; }

        /// <summary>
        /// Storage provider name (MemoryStore, LevelDB, RocksDB).
        /// Default: MemoryStore.
        /// </summary>
        public string StorageEngine { get; set; } = nameof(Neo.Persistence.Providers.MemoryStore);

        /// <summary>
        /// Storage path template for Neo system state.
        /// Default: Data_{0}.
        /// </summary>
        public string? StoragePath { get; set; } = "Data_{0}";

        /// <summary>
        /// Whether to use in-memory storage (for development/testing).
        /// Default: true
        /// </summary>
        public bool UseMemoryStorage { get; set; } = true;

        /// <summary>
        /// Connection string for persistent storage (when UseMemoryStorage is false).
        /// </summary>
        public string? StorageConnectionString { get; set; }

        /// <summary>
        /// Whether to enable QUIC listener for inbound P2P connections.
        /// Default: false
        /// </summary>
        public bool QuicEnabled { get; set; }

        /// <summary>
        /// QUIC listener port.
        /// Default: 0 (disabled)
        /// </summary>
        public int QuicPort { get; set; }

        /// <summary>
        /// QUIC ALPN string.
        /// Default: "neo-p2p"
        /// </summary>
        public string QuicAlpn { get; set; } = "neo-p2p";

        /// <summary>
        /// Optional certificate path for QUIC listener.
        /// </summary>
        public string? QuicCertificatePath { get; set; }

        /// <summary>
        /// Optional certificate password for QUIC listener.
        /// </summary>
        public string? QuicCertificatePassword { get; set; }

        /// <summary>
        /// Maximum known inventory hashes per peer.
        /// Default: 1000
        /// </summary>
        public int MaxKnownHashes { get; set; } = 1000;
    }

    /// <summary>
    /// Validation mode for Orleans ingress.
    /// </summary>
    public enum NeoValidationMode
    {
        /// <summary>No protocol validation (test-only).</summary>
        None,
        /// <summary>State-independent validation only.</summary>
        Preverify,
        /// <summary>Full state-dependent validation.</summary>
        Full
    }
}
