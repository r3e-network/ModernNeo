namespace Neo.Orleans.Interfaces;

/// <summary>
/// Orleans Grain interface for remote peer connection management.
/// Replaces Akka.NET RemoteNode Actor.
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
