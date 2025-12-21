// Copyright (C) 2015-2025 The Neo Project.
//
// MessageCommand.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.IO.Caching;

namespace Neo.Network.P2P
{
    /// <summary>
    /// Represents the command of a message.
    /// </summary>
    public enum MessageCommand : byte
    {
        #region handshaking

        /// <summary>
        /// Sent when a connection is established.
        /// </summary>
        [ReflectionCache("Neo.Network.P2P.Payloads.VersionPayload, Neo.Protocol")]
        Version = 0x00,

        /// <summary>
        /// Sent to respond to Version messages.
        /// </summary>
        Verack = 0x01,

        #endregion

        #region connectivity

        /// <summary>
        /// Sent to request for remote nodes.
        /// </summary>
        GetAddr = 0x10,

        /// <summary>
        /// Sent to respond to GetAddr messages.
        /// </summary>
        [ReflectionCache("Neo.Network.P2P.Payloads.AddrPayload, Neo.Protocol")]
        Addr = 0x11,

        /// <summary>
        /// Sent to detect whether the connection has been disconnected.
        /// </summary>
        [ReflectionCache("Neo.Network.P2P.Payloads.PingPayload, Neo.Protocol")]
        Ping = 0x18,

        /// <summary>
        /// Sent to respond to Ping messages.
        /// </summary>
        [ReflectionCache("Neo.Network.P2P.Payloads.PingPayload, Neo.Protocol")]
        Pong = 0x19,

        #endregion

        #region synchronization

        /// <summary>
        /// Sent to request for headers.
        /// </summary>
        [ReflectionCache("Neo.Network.P2P.Payloads.GetBlockByIndexPayload, Neo.Protocol")]
        GetHeaders = 0x20,

        /// <summary>
        /// Sent to respond to GetHeaders messages.
        /// </summary>
        [ReflectionCache("Neo.Network.P2P.Payloads.HeadersPayload, Neo")]
        Headers = 0x21,

        /// <summary>
        /// Sent to request for blocks.
        /// </summary>
        [ReflectionCache("Neo.Network.P2P.Payloads.GetBlocksPayload, Neo.Protocol")]
        GetBlocks = 0x24,

        /// <summary>
        /// Sent to request for memory pool.
        /// </summary>
        Mempool = 0x25,

        /// <summary>
        /// Sent to relay inventories.
        /// </summary>
        [ReflectionCache("Neo.Network.P2P.Payloads.InvPayload, Neo.Protocol")]
        Inv = 0x27,

        /// <summary>
        /// Sent to request for inventories.
        /// </summary>
        [ReflectionCache("Neo.Network.P2P.Payloads.InvPayload, Neo.Protocol")]
        GetData = 0x28,

        /// <summary>
        /// Sent to request for blocks by index.
        /// </summary>
        [ReflectionCache("Neo.Network.P2P.Payloads.GetBlockByIndexPayload, Neo.Protocol")]
        GetBlockByIndex = 0x29,

        /// <summary>
        /// Sent to respond to GetData messages when the inventories are not found.
        /// </summary>
        [ReflectionCache("Neo.Network.P2P.Payloads.InvPayload, Neo.Protocol")]
        NotFound = 0x2a,

        /// <summary>
        /// Sent to send a transaction.
        /// </summary>
        [ReflectionCache("Neo.Network.P2P.Payloads.Transaction, Neo")]
        Transaction = 0x2b,

        /// <summary>
        /// Sent to send a block.
        /// </summary>
        [ReflectionCache("Neo.Network.P2P.Payloads.Block, Neo")]
        Block = 0x2c,

        /// <summary>
        /// Sent to send an extensible payload.
        /// </summary>
        [ReflectionCache("Neo.Network.P2P.Payloads.ExtensiblePayload, Neo")]
        Extensible = 0x2e,

        /// <summary>
        /// Sent to reject an inventory.
        /// </summary>
        Reject = 0x2f,

        #endregion

        #region SPV protocol

        /// <summary>
        /// Sent to load the bloom filter.
        /// </summary>
        [ReflectionCache("Neo.Network.P2P.Payloads.FilterLoadPayload, Neo.Protocol")]
        FilterLoad = 0x30,

        /// <summary>
        /// Sent to update the items for the bloom filter.
        /// </summary>
        [ReflectionCache("Neo.Network.P2P.Payloads.FilterAddPayload, Neo.Protocol")]
        FilterAdd = 0x31,

        /// <summary>
        /// Sent to clear the bloom filter.
        /// </summary>
        FilterClear = 0x32,

        /// <summary>
        /// Sent to send a filtered block.
        /// </summary>
        [ReflectionCache("Neo.Network.P2P.Payloads.MerkleBlockPayload, Neo")]
        MerkleBlock = 0x38,

        #endregion

        #region others

        /// <summary>
        /// Sent to send an alert.
        /// </summary>
        Alert = 0x40,

        #endregion
    }
}
