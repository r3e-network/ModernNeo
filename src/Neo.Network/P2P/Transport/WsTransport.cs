// Copyright (C) 2015-2025 The Neo Project.
//
// WsTransport.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using System;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace Neo.Network.P2P.Transport
{
    /// <summary>
    /// WebSocket transport for P2P communication over ws/wss.
    /// </summary>
    public sealed class WsTransport : IAsyncDisposable
    {
        /// <summary>
        /// Connects to a remote peer via WebSocket.
        /// </summary>
        public async Task<WsPeerConnection> ConnectAsync(Uri remoteUri, CancellationToken cancellationToken = default)
        {
            if (remoteUri.Scheme != Uri.UriSchemeWs && remoteUri.Scheme != Uri.UriSchemeWss)
                throw new ArgumentException("Only ws:// or wss:// URIs supported", nameof(remoteUri));

            var client = new ClientWebSocket();
            await client.ConnectAsync(remoteUri, cancellationToken);
            return new WsPeerConnection(client, remoteUri);
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

}
