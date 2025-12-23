// Copyright (C) 2015-2025 The Neo Project.
//
// WsServerConnection.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace Neo.Network.P2P.Transport
{
    /// <summary>
    /// Bridges a server-accepted WS connection for P2P messaging.
    /// </summary>
    public sealed class WsServerConnection : Neo.P2P.Abstractions.IProtocolBridge, Neo.P2P.Abstractions.IProtocolConnection, IDisposable
    {
        public record Start(IWsConnection Connection);
        public record Bind(Neo.P2P.Abstractions.IMessageTarget Target);
        public record Send(byte[] Data);

        private IWsConnection? _connection;
        private Neo.P2P.Abstractions.IMessageTarget? _target;
        private readonly ConcurrentQueue<byte[]> _pending = new();
        private bool _disposed;

        public void Initialize(IWsConnection connection)
        {
            _connection = connection ?? throw new ArgumentNullException(nameof(connection));
            _connection.OnMessageReceived += OnMessageAsync;
            _connection.OnDisconnected += OnDisconnected;
            _ = _connection.StartReceivingAsync(CancellationToken.None);
        }

        public void BindTarget(Neo.P2P.Abstractions.IMessageTarget target)
        {
            _target = target;
            FlushPending();
        }

        public void WriteBytes(byte[] data)
        {
            if (_connection != null)
                _ = _connection.SendAsync(data);
        }

        public void Close(bool abort)
        {
            Dispose();
        }

        public void Tell(object message)
        {
            switch (message)
            {
                case Neo.P2P.Abstractions.WriteBytes writeAbstraction:
                    WriteBytes(writeAbstraction.Data);
                    break;
                case Neo.P2P.Abstractions.CloseConnection:
                    Close(false);
                    break;
                case Neo.P2P.Abstractions.BridgeBind bindAbstraction:
                    BindTarget(bindAbstraction.Target);
                    break;
                case Start start:
                    Initialize(start.Connection);
                    break;
                case Bind bind:
                    BindTarget(bind.Target);
                    break;
                case Send send:
                    WriteBytes(send.Data);
                    break;
            }
        }

        private Task OnMessageAsync(ReadOnlyMemory<byte> data)
        {
            var arr = data.ToArray();
            var target = _target;
            if (target != null)
                target.Tell(new Neo.P2P.Abstractions.DataReceived(arr));
            else
                _pending.Enqueue(arr);
            return Task.CompletedTask;
        }

        private void OnDisconnected()
        {
            Dispose();
        }

        private void FlushPending()
        {
            var target = _target;
            if (target == null) return;
            while (_pending.TryDequeue(out var data))
            {
                target.Tell(new Neo.P2P.Abstractions.DataReceived(data));
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _connection?.DisposeAsync().GetAwaiter().GetResult();
        }
    }
}
