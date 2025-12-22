// Copyright (C) 2015-2025 The Neo Project.
//
// QuicServerConnection.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Akka.Actor;
using Akka.IO;
using System;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;

namespace Neo.Network.P2P.Transport
{
    /// <summary>
    /// Bridges a server-accepted QUIC peer connection into Akka ByteString messages for RemoteNode.
    /// </summary>
    [SupportedOSPlatform("linux")]
    [SupportedOSPlatform("windows")]
    [SupportedOSPlatform("osx")]
    public sealed class QuicServerConnection : UntypedActor, Neo.P2P.Abstractions.IProtocolBridge, Neo.P2P.Abstractions.IProtocolConnection
    {
        public record Start(IQuicConnection Connection);
        public record Bind(IActorRef Target);
        public record Send(ByteString Data);

        private IQuicConnection _connection = null!;
        private IActorRef? _target;
        private readonly System.Collections.Concurrent.ConcurrentQueue<ByteString> _pending = new();

        protected override void OnReceive(object message)
        {
            switch (message)
            {
                case Neo.P2P.Abstractions.WriteBytes writeAbstraction:
                    _ = _connection.SendAsync(writeAbstraction.Data);
                    break;
                case Neo.P2P.Abstractions.CloseConnection closeAbstraction:
                    Context.Stop(Self);
                    break;
                case Tcp.Write write:
                    // Forward bytes over QUIC
                    _ = _connection.SendAsync(write.Data.ToArray());
                    if (write.Ack != null)
                        Sender.Tell(write.Ack);
                    break;
                case Tcp.Close _:
                case Tcp.Abort _:
                    Context.Stop(Self);
                    break;
                case Neo.P2P.Abstractions.BridgeBind bindAbstraction:
                    _target = bindAbstraction.Target;
                    FlushPending();
                    break;
                case Start start:
                    _connection = start.Connection;
                    _connection.OnMessageReceived += OnMessageAsync;
                    _connection.OnDisconnected += () => Context.Stop(Self);
                    _ = _connection.StartReceivingAsync(CancellationToken.None);
                    break;
                case Bind bind:
                    _target = bind.Target;
                    FlushPending();
                    break;

                case Send send:
                    _ = _connection.SendAsync(send.Data.ToArray());
                    break;
            }
        }

        public void WriteBytes(byte[] data)
        {
            _ = _connection.SendAsync(data);
        }

        public void Close(bool abort)
        {
            Context.Stop(Self);
        }

        private Task OnMessageAsync(ReadOnlyMemory<byte> data)
        {
            var arr = data.ToArray();
            var target = _target;
            if (target != null)
                target.Tell(new Neo.P2P.Abstractions.DataReceived(arr));
            else
                _pending.Enqueue(ByteString.FromBytes(arr));
            return Task.CompletedTask;
        }

        protected override void PreStart() { }

        protected override void PostStop()
        {
            if (_connection != null)
            {
                _connection.DisposeAsync().GetAwaiter().GetResult();
            }
            base.PostStop();
        }

        private void FlushPending()
        {
            var target = _target;
            if (target == null) return;
            while (_pending.TryDequeue(out var bs))
            {
                target.Tell(new Neo.P2P.Abstractions.DataReceived(bs.ToArray()));
            }
        }
    }
}
