namespace Neo.Orleans.States;

/// <summary>
/// Represents a transaction item in the memory pool.
/// Stores serializable transaction data for Orleans persistence.
/// </summary>
[GenerateSerializer]
public class PoolItemState : IComparable<PoolItemState>
{
    /// <summary>
    /// Transaction hash as hex string (key).
    /// </summary>
    [Id(0)] public string HashHex { get; set; } = string.Empty;

    /// <summary>
    /// Transaction hash as byte array.
    /// </summary>
    [Id(1)] public byte[] Hash { get; set; } = Array.Empty<byte>();

    /// <summary>
    /// Fee per byte for priority sorting.
    /// </summary>
    [Id(2)] public long FeePerByte { get; set; }

    /// <summary>
    /// Network fee for secondary sorting.
    /// </summary>
    [Id(3)] public long NetworkFee { get; set; }

    /// <summary>
    /// System fee of the transaction.
    /// </summary>
    [Id(4)] public long SystemFee { get; set; }

    /// <summary>
    /// Block height until which the transaction is valid.
    /// </summary>
    [Id(5)] public uint ValidUntilBlock { get; set; }

    /// <summary>
    /// Timestamp when transaction was added to pool.
    /// </summary>
    [Id(6)] public DateTime Timestamp { get; set; }

    /// <summary>
    /// Serialized transaction data for reconstruction.
    /// </summary>
    [Id(7)] public byte[] SerializedData { get; set; } = Array.Empty<byte>();

    /// <summary>
    /// Compare by fee priority (higher fee = higher priority).
    /// </summary>
    public int CompareTo(PoolItemState? other)
    {
        if (other is null) return 1;

        // Higher FeePerByte = higher priority (descending)
        var result = other.FeePerByte.CompareTo(FeePerByte);
        if (result != 0) return result;

        // Higher NetworkFee = higher priority (descending)
        result = other.NetworkFee.CompareTo(NetworkFee);
        if (result != 0) return result;

        // Earlier timestamp = higher priority (ascending)
        result = Timestamp.CompareTo(other.Timestamp);
        if (result != 0) return result;

        // Hash as tiebreaker
        return string.Compare(HashHex, other.HashHex, StringComparison.Ordinal);
    }
}
