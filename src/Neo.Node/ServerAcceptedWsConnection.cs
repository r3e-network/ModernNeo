// Copyright (C) 2015-2025 The Neo Project.
//
// ServerAcceptedWsConnection.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Network.P2P.Transport;
using System;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace Neo.Node
{
    internal sealed class ServerAcceptedWsConnection : IWsConnection
    {
        private readonly WebSocket _socket;
        public event Func<ReadOnlyMemory<byte>, Task>? OnMessageReceived;
        public event Action? OnDisconnected;

        public ServerAcceptedWsConnection(WebSocket socket)
        {
            _socket = socket;
        }

        public async Task StartReceivingAsync(CancellationToken cancellationToken = default)
        {
            var buffer = new byte[64 * 1024];
            try
            {
                while (_socket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
                {
                    var result = await _socket.ReceiveAsync(buffer, cancellationToken);
                    if (result.MessageType == WebSocketMessageType.Close)
                        break;
                    if (result.Count > 0 && OnMessageReceived != null)
                    {
                        await OnMessageReceived(new ReadOnlyMemory<byte>(buffer, 0, result.Count));
                    }
                }
            }
            catch (OperationCanceledException) { }
            finally
            {
                OnDisconnected?.Invoke();
            }
        }

        public Task SendAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default)
        {
            return _socket.SendAsync(new ArraySegment<byte>(data.ToArray()), WebSocketMessageType.Binary, true, cancellationToken);
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                if (_socket.State == WebSocketState.Open)
                    await _socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
            }
            catch { }
            _socket.Dispose();
        }
    }
}
