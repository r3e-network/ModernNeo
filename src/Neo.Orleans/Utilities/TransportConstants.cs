// Copyright (C) 2015-2025 The Neo Project.
//
// TransportConstants.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Network.P2P;
using System.Net;

namespace Neo.Orleans.Utilities
{
    /// <summary>
    /// Shared constants and utilities for transport services.
    /// Consolidates duplicate definitions from TcpTransportService, QuicTransportService,
    /// WsTransportService, and WsP2PListener.
    /// </summary>
    public static class TransportConstants
    {
        /// <summary>
        /// Maximum message size in bytes (payload + header overhead).
        /// </summary>
        public static readonly int MaxMessageBytes = Message.PayloadMaxSize + 16;

        /// <summary>
        /// Maximum message size for WebSocket with length prefix (payload + header + 4-byte length).
        /// </summary>
        public static readonly int MaxWsMessageBytes = Message.PayloadMaxSize + 16 + 4;

        /// <summary>
        /// Formats a connection key from address and port.
        /// </summary>
        /// <param name="address">The peer address.</param>
        /// <param name="port">The peer port.</param>
        /// <returns>A normalized connection key string.</returns>
        public static string FormatConnectionKey(string address, int port)
        {
            if (IPAddress.TryParse(address, out var ip))
                return new IPEndPoint(ip, port).ToString();

            return $"{address}:{port}";
        }
    }
}
