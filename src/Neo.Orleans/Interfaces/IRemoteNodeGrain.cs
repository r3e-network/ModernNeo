// Copyright (C) 2015-2025 The Neo Project.
//
// IRemoteNodeGrain.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.IO;
using Neo.Network.P2P;

namespace Neo.Orleans.Interfaces
{
    /// <summary>
    /// Orleans Grain interface for remote peer connection management.
    /// Handles peer messaging and connection state.
    /// </summary>
    public interface IRemoteNodeGrain : IGrainWithStringKey
    {
        /// <summary>
        /// Handles an incoming protocol message.
        /// </summary>
        Task HandleMessageAsync(byte[] message);

        /// <summary>
        /// Sends a message to the remote peer.
        /// </summary>
        Task SendAsync(byte[] message);

        /// <summary>
        /// Sends a message to the remote peer using the negotiated compression settings.
        /// </summary>
        Task SendMessageAsync(MessageCommand command, ISerializable? payload = null);

        /// <summary>
        /// Gets the connection state.
        /// </summary>
        Task<ConnectionState> GetStateAsync();

        /// <summary>
        /// Disconnects from the remote peer.
        /// </summary>
        Task DisconnectAsync();

        /// <summary>
        /// Gets the remote peer's reported height.
        /// </summary>
        Task<uint> GetRemoteHeightAsync();

        /// <summary>
        /// Checks if an inventory hash is known.
        /// </summary>
        Task<bool> IsKnownHashAsync(byte[] hash);

        /// <summary>
        /// Adds an inventory hash to the known set.
        /// </summary>
        Task AddKnownHashAsync(byte[] hash);

        /// <summary>
        /// Initiates the version handshake.
        /// </summary>
        Task StartHandshakeAsync(uint localHeight, uint nonce, string userAgent);

        /// <summary>
        /// Updates connection info after successful handshake.
        /// </summary>
        Task CompleteHandshakeAsync(uint remoteHeight, int listenerPort, bool isFullNode, string userAgent);
    }

    /// <summary>
    /// Connection state for a remote node.
    /// </summary>
    public enum ConnectionState
    {
        Disconnected,
        Connecting,
        Connected,
        Handshaking,
        Active
    }
}
