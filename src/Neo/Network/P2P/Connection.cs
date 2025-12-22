// Copyright (C) 2015-2025 The Neo Project.
//
// Connection.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Akka.Actor;
using Akka.IO;
using Neo.Extensions.Exceptions;
using Neo.P2P.Abstractions;
using System;
using System.Net;

namespace Neo.Network.P2P
{
    /// <summary>
    /// Represents a connection of the P2P network.
    /// </summary>
    public abstract class Connection : UntypedActor
    {
        internal class Close { public bool Abort; }
        internal class Ack : Tcp.Event { public static Ack Instance = new(); }

        /// <summary>
        /// connection initial timeout (in seconds) before any package has been accepted.
        /// </summary>
        private const int connectionTimeoutLimitStart = 10;

        /// <summary>
        /// connection timeout (in seconds) after every `OnReceived(ByteString data)` event.
        /// </summary>
        private const int connectionTimeoutLimit = 60;

        /// <summary>
        /// The address of the remote node.
        /// </summary>
        public IPEndPoint Remote { get; }

        /// <summary>
        /// The address of the local node.
        /// </summary>
        public IPEndPoint Local { get; }

        private ICancelable timer;
        private readonly IActorRef? tcp;
        private readonly IActorRef? bridge;
        private bool disconnected = false;

        /// <summary>
        /// Initializes a new instance of the <see cref="Connection"/> class.
        /// </summary>
        /// <param name="connection">The underlying connection object.</param>
        /// <param name="remote">The address of the remote node.</param>
        /// <param name="local">The address of the local node.</param>
        protected Connection(object connection, IPEndPoint remote, IPEndPoint local)
        {
            Remote = remote;
            Local = local;
            timer = Context.System.Scheduler.ScheduleTellOnceCancelable(TimeSpan.FromSeconds(connectionTimeoutLimitStart), Self, new Close { Abort = true }, ActorRefs.NoSender);
            switch (connection)
            {
                case BridgeConnection bc:
                    // Bridge actor - use abstraction messages (WriteBytes/CloseConnection)
                    bridge = bc.Bridge;
                    break;
                case IActorRef tcp:
                    // Native TCP actor - use Akka.IO.Tcp messages
                    this.tcp = tcp;
                    break;
            }
        }

        /// <summary>
        /// Disconnect from the remote node.
        /// </summary>
        /// <param name="abort">Indicates whether the TCP ABORT command should be sent.</param>
        public void Disconnect(bool abort = false)
        {
            disconnected = true;
            if (bridge != null)
            {
                bridge.Tell(new CloseConnection(abort));
            }
            else if (tcp != null)
            {
                tcp.Tell(abort ? Tcp.Abort.Instance : Tcp.Close.Instance);
            }
            Context.Stop(Self);
        }

        /// <summary>
        /// Called when a TCP ACK message is received.
        /// </summary>
        protected virtual void OnAck()
        {
        }

        /// <summary>
        /// Called when data is received.
        /// </summary>
        /// <param name="data">The received data.</param>
        protected abstract void OnData(ByteString data);

        protected override void OnReceive(object message)
        {
            switch (message)
            {
                case Close close:
                    Disconnect(close.Abort);
                    break;
                case Ack _:
                    OnAck();
                    break;
                case Neo.P2P.Abstractions.DataReceived data:
                    OnReceived(ByteString.FromBytes(data.Data));
                    break;
                case Tcp.Received received:
                    OnReceived(received.Data);
                    break;
                case Tcp.ConnectionClosed _:
                    Context.Stop(Self);
                    break;
            }
        }

        private void OnReceived(ByteString data)
        {
            timer.CancelIfNotNull();
            timer = Context.System.Scheduler.ScheduleTellOnceCancelable(TimeSpan.FromSeconds(connectionTimeoutLimit), Self, new Close { Abort = true }, ActorRefs.NoSender);
            data.TryCatch<ByteString, Exception>(OnData, (_, _) => Disconnect(true));
        }

        protected override void PostStop()
        {
            if (!disconnected)
            {
                if (bridge != null)
                    bridge.Tell(new CloseConnection(false));
                else
                    tcp?.Tell(Tcp.Close.Instance);
            }
            timer.CancelIfNotNull();
            base.PostStop();
        }

        /// <summary>
        /// Sends data to the remote node.
        /// </summary>
        /// <param name="data"></param>
        protected void SendData(ByteString data)
        {
            if (bridge != null)
            {
                bridge.Tell(new WriteBytes(data.ToArray()));
            }
            else if (tcp != null)
            {
                tcp.Tell(Tcp.Write.Create(data, Ack.Instance));
            }
        }
    }
}
