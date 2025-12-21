// Copyright (C) 2015-2025 The Neo Project.
//
// MessageHandler.cs file belongs to the neo project and is free
// software distributed under the MIT software license.

using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace Neo.Network.P2P;

/// <summary>
/// Handles incoming P2P messages and routes them to appropriate handlers.
/// </summary>
public sealed class MessageHandler : IMessageHandler
{
    private readonly ConcurrentDictionary<MessageCommand, Func<byte[], IPeerConnection, Task>> _handlers = new();
    private readonly INeoSystemContext _context;

    /// <summary>
    /// Event raised when a message is received.
    /// </summary>
    public event EventHandler<MessageReceivedEventArgs>? MessageReceived;

    /// <summary>
    /// Event raised when a message handling error occurs.
    /// </summary>
    public event EventHandler<MessageErrorEventArgs>? MessageError;

    /// <summary>
    /// Creates a new message handler.
    /// </summary>
    /// <param name="context">The Neo system context.</param>
    public MessageHandler(INeoSystemContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        RegisterDefaultHandlers();
    }

    /// <summary>
    /// Registers a handler for a specific message command.
    /// </summary>
    public void RegisterHandler(MessageCommand command, Func<byte[], IPeerConnection, Task> handler)
    {
        _handlers[command] = handler ?? throw new ArgumentNullException(nameof(handler));
    }

    /// <summary>
    /// Handles an incoming message.
    /// </summary>
    public async Task HandleMessageAsync(MessageCommand command, byte[] payload, IPeerConnection peer)
    {
        MessageReceived?.Invoke(this, new MessageReceivedEventArgs(command, payload.Length, peer));

        try
        {
            if (_handlers.TryGetValue(command, out var handler))
            {
                await handler(payload, peer);
            }
            else
            {
                // Unknown command - log and ignore
                await HandleUnknownAsync(command, payload, peer);
            }
        }
        catch (Exception ex)
        {
            MessageError?.Invoke(this, new MessageErrorEventArgs(command, ex, peer));
        }
    }

    private void RegisterDefaultHandlers()
    {
        _handlers[MessageCommand.Version] = HandleVersionAsync;
        _handlers[MessageCommand.Verack] = HandleVerackAsync;
        _handlers[MessageCommand.Ping] = HandlePingAsync;
        _handlers[MessageCommand.Pong] = HandlePongAsync;
        _handlers[MessageCommand.GetAddr] = HandleGetAddrAsync;
        _handlers[MessageCommand.Addr] = HandleAddrAsync;
        _handlers[MessageCommand.GetHeaders] = HandleGetHeadersAsync;
        _handlers[MessageCommand.Headers] = HandleHeadersAsync;
        _handlers[MessageCommand.GetBlocks] = HandleGetBlocksAsync;
        _handlers[MessageCommand.GetData] = HandleGetDataAsync;
        _handlers[MessageCommand.Inv] = HandleInvAsync;
        _handlers[MessageCommand.Block] = HandleBlockAsync;
        _handlers[MessageCommand.Transaction] = HandleTransactionAsync;
        _handlers[MessageCommand.Mempool] = HandleMempoolAsync;
    }

    private Task HandleVersionAsync(byte[] payload, IPeerConnection peer)
    {
        // Parse version payload and validate
        // Send verack if valid
        return peer.SendAsync(MessageCommand.Verack, Array.Empty<byte>());
    }

    private Task HandleVerackAsync(byte[] payload, IPeerConnection peer)
    {
        // Mark peer as verified
        peer.SetVerified(true);
        return Task.CompletedTask;
    }

    private async Task HandlePingAsync(byte[] payload, IPeerConnection peer)
    {
        // Respond with pong using same payload
        await peer.SendAsync(MessageCommand.Pong, payload);
    }

    private Task HandlePongAsync(byte[] payload, IPeerConnection peer)
    {
        // Update peer latency
        return Task.CompletedTask;
    }

    private Task HandleGetAddrAsync(byte[] payload, IPeerConnection peer)
    {
        // Send known peer addresses
        // Would serialize AddrPayload
        return peer.SendAsync(MessageCommand.Addr, Array.Empty<byte>());
    }

    private Task HandleAddrAsync(byte[] payload, IPeerConnection peer)
    {
        // Parse and add new peer addresses
        return Task.CompletedTask;
    }

    private Task HandleGetHeadersAsync(byte[] payload, IPeerConnection peer)
    {
        // Send requested headers
        return peer.SendAsync(MessageCommand.Headers, Array.Empty<byte>());
    }

    private Task HandleHeadersAsync(byte[] payload, IPeerConnection peer)
    {
        // Process received headers
        return Task.CompletedTask;
    }

    private Task HandleGetBlocksAsync(byte[] payload, IPeerConnection peer)
    {
        // Send block hashes via Inv
        return peer.SendAsync(MessageCommand.Inv, Array.Empty<byte>());
    }

    private Task HandleGetDataAsync(byte[] payload, IPeerConnection peer)
    {
        // Send requested inventory items
        return Task.CompletedTask;
    }

    private Task HandleInvAsync(byte[] payload, IPeerConnection peer)
    {
        // Request missing inventory items
        return peer.SendAsync(MessageCommand.GetData, payload);
    }

    private Task HandleBlockAsync(byte[] payload, IPeerConnection peer)
    {
        // Process received block
        // Would deserialize and validate block
        return Task.CompletedTask;
    }

    private Task HandleTransactionAsync(byte[] payload, IPeerConnection peer)
    {
        // Process received transaction
        // Would deserialize and add to mempool
        return Task.CompletedTask;
    }

    private Task HandleMempoolAsync(byte[] payload, IPeerConnection peer)
    {
        // Send mempool transaction hashes
        return peer.SendAsync(MessageCommand.Inv, Array.Empty<byte>());
    }

    private Task HandleUnknownAsync(MessageCommand command, byte[] payload, IPeerConnection peer)
    {
        // Log unknown command
        return Task.CompletedTask;
    }
}

/// <summary>
/// Interface for message handling.
/// </summary>
public interface IMessageHandler
{
    /// <summary>
    /// Handles an incoming message.
    /// </summary>
    Task HandleMessageAsync(MessageCommand command, byte[] payload, IPeerConnection peer);

    /// <summary>
    /// Registers a handler for a specific message command.
    /// </summary>
    void RegisterHandler(MessageCommand command, Func<byte[], IPeerConnection, Task> handler);
}

/// <summary>
/// Interface for peer connections.
/// </summary>
public interface IPeerConnection
{
    /// <summary>
    /// Gets the peer's remote endpoint.
    /// </summary>
    string RemoteEndpoint { get; }

    /// <summary>
    /// Gets whether the peer is verified.
    /// </summary>
    bool IsVerified { get; }

    /// <summary>
    /// Sets the verification status.
    /// </summary>
    void SetVerified(bool verified);

    /// <summary>
    /// Sends a message to the peer.
    /// </summary>
    Task SendAsync(MessageCommand command, byte[] payload);

    /// <summary>
    /// Disconnects the peer.
    /// </summary>
    Task DisconnectAsync(string reason);
}

/// <summary>
/// Event args for message received.
/// </summary>
public sealed class MessageReceivedEventArgs : EventArgs
{
    public MessageCommand Command { get; }
    public int PayloadSize { get; }
    public IPeerConnection Peer { get; }

    public MessageReceivedEventArgs(MessageCommand command, int payloadSize, IPeerConnection peer)
    {
        Command = command;
        PayloadSize = payloadSize;
        Peer = peer;
    }
}

/// <summary>
/// Event args for message error.
/// </summary>
public sealed class MessageErrorEventArgs : EventArgs
{
    public MessageCommand Command { get; }
    public Exception Exception { get; }
    public IPeerConnection Peer { get; }

    public MessageErrorEventArgs(MessageCommand command, Exception exception, IPeerConnection peer)
    {
        Command = command;
        Exception = exception;
        Peer = peer;
    }
}
