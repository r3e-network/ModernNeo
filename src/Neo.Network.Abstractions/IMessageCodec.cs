// Copyright (C) 2015-2025 The Neo Project.
//
// IMessageCodec.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using System;

namespace Neo.Network.Abstractions
{
    /// <summary>
    /// Provides encoding and decoding of P2P messages.
    /// </summary>
    public interface IMessageCodec
    {
        /// <summary>
        /// Encodes a message for transmission.
        /// </summary>
        /// <param name="message">The message to encode.</param>
        /// <returns>The encoded bytes.</returns>
        byte[] Encode(INetworkMessage message);

        /// <summary>
        /// Tries to decode a message from received bytes.
        /// </summary>
        /// <param name="data">The received data.</param>
        /// <param name="message">The decoded message if successful.</param>
        /// <param name="bytesConsumed">The number of bytes consumed.</param>
        /// <returns>True if a complete message was decoded.</returns>
        bool TryDecode(ReadOnlySpan<byte> data, out INetworkMessage? message, out int bytesConsumed);
    }

    /// <summary>
    /// Represents a network message.
    /// </summary>
    public interface INetworkMessage
    {
        /// <summary>
        /// Gets the message command/type.
        /// </summary>
        string Command { get; }

        /// <summary>
        /// Gets the message payload.
        /// </summary>
        ReadOnlyMemory<byte> Payload { get; }

        /// <summary>
        /// Gets the message flags.
        /// </summary>
        MessageFlags Flags { get; }
    }

    /// <summary>
    /// Message flags for network messages.
    /// </summary>
    [Flags]
    public enum MessageFlags : byte
    {
        /// <summary>
        /// No flags.
        /// </summary>
        None = 0,

        /// <summary>
        /// Message is compressed.
        /// </summary>
        Compressed = 1
    }
}
