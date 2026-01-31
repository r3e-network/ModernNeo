// Copyright (C) 2015-2025 The Neo Project.
//
// ConsensusOptions.cs file belongs to the neo project and is free
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
    /// Consensus and network protocol configuration options for Neo Orleans.
    /// </summary>
    public sealed class ConsensusOptions
    {
        private const uint DefaultNetwork = 5195086;
        private static readonly string[] DefaultSeeds = { "seed1.neo.org:10333", "seed2.neo.org:10333", "seed3.neo.org:10333", "seed4.neo.org:10333", "seed5.neo.org:10333" };

        /// <summary>
        /// Network magic number used for P2P handshake.
        /// Defaults to 5195086 (N3 mainnet).
        /// </summary>
        public uint NetworkMagic { get; set; } = DefaultNetwork;

        /// <summary>
        /// Protocol version advertised during handshake.
        /// Default: 0
        /// </summary>
        public uint ProtocolVersion { get; set; }

        /// <summary>
        /// User agent string advertised during handshake.
        /// Default: "/Neo:{version}/" derived from the Neo assembly.
        /// </summary>
        public string UserAgent { get; set; } = $"/Neo:{typeof(NeoSystem).Assembly.GetName().Version?.ToString(3) ?? "0.0.0"}/";

        /// <summary>
        /// Seed list used for initial peer discovery.
        /// Defaults to N3 mainnet seeds.
        /// </summary>
        public IReadOnlyList<string> SeedList { get; set; } = DefaultSeeds;

        /// <summary>
        /// Full protocol settings for transaction and block validation.
        /// When using the new options API, prefer setting this to an IProtocolSettings implementation.
        /// </summary>
        public IProtocolSettings? ProtocolSettings { get; set; }
    }
}
