namespace Neo.Orleans.Interfaces;

/// <summary>
/// Orleans Grain interface for local P2P node management.
/// Replaces Akka.NET LocalNode Actor with seed nodes and connection management.
/// </summary>
public interface ILocalNodeGrain : IGrainWithIntegerKey
{
    /// <summary>
    /// Initializes the local node with configuration.
    /// </summary>
    Task InitializeAsync(LocalNodeConfig config);

    /// <summary>
    /// Starts the local node, beginning peer discovery.
    /// </summary>
    Task StartAsync();

    /// <summary>
    /// Stops the local node.
    /// </summary>
    Task StopAsync();

    /// <summary>
    /// Registers a new peer connection.
    /// </summary>
    Task RegisterPeerAsync(string address, int port, uint height);

    /// <summary>
    /// Registers a peer with full connection info.
    /// </summary>
    Task RegisterPeerAsync(PeerConnectionInfo info);

    /// <summary>
    /// Unregisters a peer connection.
    /// </summary>
    Task UnregisterPeerAsync(string address, int port);

    /// <summary>
    /// Updates a peer's reported block height.
    /// </summary>
    Task UpdatePeerHeightAsync(string address, int port, uint height);

    /// <summary>
    /// Checks if a new connection should be allowed.
    /// Validates nonce and network magic.
    /// </summary>
    Task<bool> AllowNewConnectionAsync(uint nonce, uint networkMagic, string address);

    /// <summary>
    /// Relays an inventory item to connected peers.
    /// </summary>
    Task RelayAsync(byte[] inventoryHash, byte inventoryType);

    /// <summary>
    /// Relays a block to peers that are behind.
    /// </summary>
    Task RelayBlockAsync(byte[] blockHash, uint blockIndex);

    /// <summary>
    /// Gets the count of connected peers.
    /// </summary>
    Task<int> GetConnectedPeerCountAsync();

    /// <summary>
    /// Gets the count of unconnected peers.
    /// </summary>
    Task<int> GetUnconnectedPeerCountAsync();

    /// <summary>
    /// Gets information about connected peers.
    /// </summary>
    Task<IEnumerable<PeerInfo>> GetConnectedPeersAsync();

    /// <summary>
    /// Gets unconnected peer addresses.
    /// </summary>
    Task<IEnumerable<string>> GetUnconnectedPeersAsync();

    /// <summary>
    /// Adds peers to the unconnected pool.
    /// </summary>
    Task AddPeersAsync(IEnumerable<string> addresses);

    /// <summary>
    /// Broadcasts a message to all connected peers.
    /// </summary>
    Task BroadcastAsync(byte[] message);

    /// <summary>
    /// Requests more peers when connections are low.
    /// </summary>
    Task RequestMorePeersAsync(int count);

    /// <summary>
    /// Gets the local node's nonce.
    /// </summary>
    Task<uint> GetNonceAsync();

    /// <summary>
    /// Gets the local node's user agent.
    /// </summary>
    Task<string> GetUserAgentAsync();

    /// <summary>
    /// Gets the local node state summary.
    /// </summary>
    Task<LocalNodeStateSummary> GetStateSummaryAsync();
}

/// <summary>
/// Configuration for initializing the local node.
/// </summary>
[GenerateSerializer]
public record LocalNodeConfig(
    [property: Id(0)] uint Nonce,
    [property: Id(1)] string UserAgent,
    [property: Id(2)] IReadOnlyList<string> SeedList,
    [property: Id(3)] int MaxConnections,
    [property: Id(4)] int ListenerPort,
    [property: Id(5)] uint NetworkMagic,
    [property: Id(6)] uint ProtocolVersion);

/// <summary>
/// Information about a connected peer.
/// </summary>
[GenerateSerializer]
public record PeerInfo(
    [property: Id(0)] string Address,
    [property: Id(1)] int Port,
    [property: Id(2)] uint Height,
    [property: Id(3)] uint Nonce,
    [property: Id(4)] string UserAgent,
    [property: Id(5)] bool IsFullNode);

/// <summary>
/// Full connection info for registering a peer.
/// </summary>
[GenerateSerializer]
public record PeerConnectionInfo(
    [property: Id(0)] string Address,
    [property: Id(1)] int Port,
    [property: Id(2)] uint Height,
    [property: Id(3)] uint Nonce,
    [property: Id(4)] string UserAgent,
    [property: Id(5)] bool IsFullNode,
    [property: Id(6)] int ListenerPort);

/// <summary>
/// Summary of local node state.
/// </summary>
[GenerateSerializer]
public record LocalNodeStateSummary(
    [property: Id(0)] int ConnectedCount,
    [property: Id(1)] int UnconnectedCount,
    [property: Id(2)] uint Nonce,
    [property: Id(3)] string UserAgent,
    [property: Id(4)] int ListenerPort,
    [property: Id(5)] bool IsStarted);
