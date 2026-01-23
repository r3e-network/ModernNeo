// Copyright (C) 2015-2025 The Neo Project.
//
// LocalNode.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Network.P2P.Payloads;

namespace Neo.Network.P2P
{
    /// <summary>
    /// Message types used by the local node runtime.
    /// </summary>
    public static class LocalNode
    {
        /// <summary>
        /// Indicates the protocol version of the local node.
        /// </summary>
        public const uint ProtocolVersion = 0;

        /// <summary>
        /// Relay an inventory without re-verification.
        /// </summary>
        public record RelayDirectly(IInventory Inventory);

        /// <summary>
        /// Send an inventory directly to peers.
        /// </summary>
        public record SendDirectly(IInventory Inventory);
    }
}
