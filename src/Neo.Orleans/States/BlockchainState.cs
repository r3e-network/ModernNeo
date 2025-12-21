namespace Neo.Orleans.States;

/// <summary>
/// Persistent state for BlockchainGrain.
/// Mirrors Akka.NET Blockchain Actor state with header cache and unverified blocks.
/// </summary>
[GenerateSerializer]
public class BlockchainState
{
    /// <summary>
    /// Current blockchain height (last persisted block index).
    /// </summary>
    [Id(0)] public uint Height { get; set; }

    /// <summary>
    /// Hash of the current (last persisted) block.
    /// </summary>
    [Id(1)] public byte[] CurrentBlockHash { get; set; } = Array.Empty<byte>();

    /// <summary>
    /// Timestamp of the current block.
    /// </summary>
    [Id(2)] public ulong Timestamp { get; set; }

    /// <summary>
    /// State root hash after the current block.
    /// </summary>
    [Id(3)] public byte[] StateRoot { get; set; } = Array.Empty<byte>();

    /// <summary>
    /// Whether the blockchain has been initialized with genesis block.
    /// </summary>
    [Id(4)] public bool IsInitialized { get; set; }

    /// <summary>
    /// Header cache - stores headers ahead of blocks for faster sync.
    /// Key: block index, Value: serialized header data.
    /// </summary>
    [Id(5)] public Dictionary<uint, HeaderCacheEntry> HeaderCache { get; set; } = new();

    /// <summary>
    /// Maximum header cache size.
    /// </summary>
    [Id(6)] public int MaxHeaderCacheSize { get; set; } = 10000;

    /// <summary>
    /// Highest header index in the cache.
    /// </summary>
    [Id(7)] public uint HeaderHeight { get; set; }

    /// <summary>
    /// Unverified blocks waiting for their predecessors.
    /// Key: block index, Value: list of unverified blocks at that index.
    /// </summary>
    [Id(8)] public Dictionary<uint, List<UnverifiedBlockEntry>> UnverifiedBlocks { get; set; } = new();

    /// <summary>
    /// Block cache - verified blocks waiting to be persisted.
    /// Key: block hash hex, Value: serialized block data.
    /// </summary>
    [Id(9)] public Dictionary<string, byte[]> BlockCache { get; set; } = new();
}

/// <summary>
/// Entry in the header cache.
/// </summary>
[GenerateSerializer]
public class HeaderCacheEntry
{
    /// <summary>
    /// Block index.
    /// </summary>
    [Id(0)] public uint Index { get; set; }

    /// <summary>
    /// Block hash.
    /// </summary>
    [Id(1)] public byte[] Hash { get; set; } = Array.Empty<byte>();

    /// <summary>
    /// Previous block hash.
    /// </summary>
    [Id(2)] public byte[] PrevHash { get; set; } = Array.Empty<byte>();

    /// <summary>
    /// Serialized header data.
    /// </summary>
    [Id(3)] public byte[] Data { get; set; } = Array.Empty<byte>();

    /// <summary>
    /// Block timestamp.
    /// </summary>
    [Id(4)] public ulong Timestamp { get; set; }
}

/// <summary>
/// Entry for an unverified block.
/// </summary>
[GenerateSerializer]
public class UnverifiedBlockEntry
{
    /// <summary>
    /// Block hash hex.
    /// </summary>
    [Id(0)] public string HashHex { get; set; } = string.Empty;

    /// <summary>
    /// Serialized block data.
    /// </summary>
    [Id(1)] public byte[] Data { get; set; } = Array.Empty<byte>();

    /// <summary>
    /// Sender addresses that sent this block.
    /// </summary>
    [Id(2)] public HashSet<string> Senders { get; set; } = new();
}
