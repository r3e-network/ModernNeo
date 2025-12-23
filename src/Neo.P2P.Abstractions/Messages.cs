// Copyright (C) 2015-2025 The Neo Project.
//
// Messages.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using System.Net;

namespace Neo.P2P.Abstractions
{
    /// <summary>
    /// Transport-agnostic message target abstraction replacing Akka IActorRef.
    /// Implementations can wrap Orleans grain references, actors, or other messaging targets.
    /// </summary>
    public interface IMessageTarget
    {
        /// <summary>
        /// Sends a message to this target.
        /// </summary>
        /// <param name="message">The message to send.</param>
        void Tell(object message);
    }

    /// <summary>
    /// Minimal abstraction for a P2P bridge that can be bound to a protocol actor.
    /// Implementations should also accept concrete Bind messages for backward compatibility.
    /// </summary>
    public interface IProtocolBridge { }

    /// <summary>
    /// Minimal protocol connection operations for interface-first transport.
    /// Implementations may be actors; these methods should forward to the underlying transport.
    /// </summary>
    public interface IProtocolConnection
    {
        void WriteBytes(byte[] data);
        void Close(bool abort);
    }

    /// <summary>
    /// Transport-agnostic bridge accept used to attach a server-side connection bridge (WS/QUIC/etc.)
    /// to a protocol actor that speaks Neo P2P.
    /// </summary>
    public readonly record struct AcceptBridge(IMessageTarget Bridge, IPEndPoint Remote, IPEndPoint Local);

    /// <summary>
    /// Transport-agnostic bind message that instructs a bridge actor to forward
    /// subsequent bytes to the provided protocol actor target.
    /// </summary>
    public readonly record struct BridgeBind(IMessageTarget Target);

    /// <summary>
    /// Bytes received by a protocol connection.
    /// </summary>
    public readonly record struct DataReceived(byte[] Data);

    /// <summary>
    /// Request to write bytes to the underlying transport.
    /// </summary>
    public readonly record struct WriteBytes(byte[] Data);

    /// <summary>
    /// Request to close the underlying transport.
    /// </summary>
    public readonly record struct CloseConnection(bool Abort);

    /// <summary>
    /// Wrapper to pass a bridge to Connection constructor, distinguishing it from native TCP actors.
    /// </summary>
    public readonly record struct BridgeConnection(IMessageTarget Bridge);
}
