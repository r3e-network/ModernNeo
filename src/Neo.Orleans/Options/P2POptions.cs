// Copyright (C) 2015-2025 The Neo Project.
//
// P2POptions.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

namespace Neo.Orleans.Options
{
    /// <summary>
    /// P2P network configuration options for Neo Orleans.
    /// </summary>
    public sealed class P2POptions
    {
        /// <summary>
        /// TCP listener port for inbound P2P connections.
        /// Default: 20333
        /// </summary>
        public int TcpPort { get; set; } = 20333;

        /// <summary>
        /// TCP listener bind address (optional).
        /// Defaults to IPAddress.Any when not specified.
        /// </summary>
        public string? TcpBindAddress { get; set; }

        /// <summary>
        /// QUIC listener port.
        /// Default: 20335
        /// </summary>
        public int QuicPort { get; set; } = 20335;

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
        /// WebSocket listener port.
        /// Default: 20334
        /// </summary>
        public int WsPort { get; set; } = 20334;

        /// <summary>
        /// Maximum number of concurrent connections per LocalNodeGrain.
        /// Default: 100
        /// </summary>
        public int MaxConnections { get; set; } = 100;

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
        /// Maximum known inventory hashes per peer.
        /// Default: 1000
        /// </summary>
        public int MaxKnownHashes { get; set; } = 1000;

        /// <summary>
        /// Maximum transactions in memory pool.
        /// Default: 50000
        /// </summary>
        public int MaxMemoryPoolSize { get; set; } = 50000;

        /// <summary>
        /// Whether to enable payload compression for P2P messages.
        /// Default: true
        /// </summary>
        public bool EnableCompression { get; set; } = true;
    }
}
