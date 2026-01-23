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
using System.Buffers;
using System.Buffers.Binary;
using System.IO;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace Neo.Node
{
    internal sealed class ServerAcceptedWsConnection : IWsConnection
    {
        private const int MaxMessageBytes = 0x02000000 + 16 + 4;

        private readonly WebSocket _socket;
        private readonly SemaphoreSlim _writeLock = new(1, 1);
        private bool _disposed;
        public event Func<ReadOnlyMemory<byte>, Task>? OnMessageReceived;
        public event Action? OnDisconnected;

        public ServerAcceptedWsConnection(WebSocket socket)
        {
            _socket = socket;
        }

        public async Task StartReceivingAsync(CancellationToken cancellationToken = default)
        {
            var buffer = ArrayPool<byte>.Shared.Rent(64 * 1024);
            var stream = new MemoryStream();
            try
            {
                while (_socket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
                {
                    var result = await _socket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
                    if (result.MessageType == WebSocketMessageType.Close)
                        break;

                    if (result.MessageType != WebSocketMessageType.Binary)
                        continue;

                    if (result.Count > 0)
                        stream.Write(buffer, 0, result.Count);

                    if (stream.Length > MaxMessageBytes)
                        break;

                    if (!result.EndOfMessage)
                        continue;

                    var data = stream.ToArray();
                    stream.SetLength(0);

                    if (data.Length >= 4)
                    {
                        var length = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(0, 4));
                        if (length == data.Length - 4)
                        {
                            if (OnMessageReceived != null)
                                await OnMessageReceived(new ReadOnlyMemory<byte>(data, 4, length));
                            continue;
                        }
                    }

                    if (data.Length > 0 && OnMessageReceived != null)
                        await OnMessageReceived(new ReadOnlyMemory<byte>(data));
                }
            }
            catch (OperationCanceledException) { }
            catch (WebSocketException) { }
            catch { }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
                stream.Dispose();
                OnDisconnected?.Invoke();
            }
        }

        public async Task SendAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default)
        {
            if (data.Length + 4 > MaxMessageBytes)
                throw new ArgumentOutOfRangeException(nameof(data), $"Message length {data.Length} exceeds max {MaxMessageBytes - 4} bytes.");

            await _writeLock.WaitAsync(cancellationToken);
            try
            {
                var buffer = ArrayPool<byte>.Shared.Rent(4 + data.Length);
                try
                {
                    BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(0, 4), data.Length);
                    data.Span.CopyTo(buffer.AsSpan(4));
                    await _socket.SendAsync(new ArraySegment<byte>(buffer, 0, 4 + data.Length), WebSocketMessageType.Binary, true, cancellationToken);
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(buffer);
                }
            }
            finally
            {
                _writeLock.Release();
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (_disposed)
                return;

            _disposed = true;
            try
            {
                if (_socket.State == WebSocketState.Open)
                    await _socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
            }
            catch { }
            _socket.Dispose();
            _writeLock.Dispose();
        }
    }
}
