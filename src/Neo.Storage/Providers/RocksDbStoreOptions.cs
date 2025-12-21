// Copyright (C) 2015-2025 The Neo Project.
//
// RocksDbStoreOptions.cs file belongs to the neo project and is free
// software distributed under the MIT software license.

namespace Neo.Persistence.Providers;

/// <summary>
/// Configuration options for RocksDB store.
/// </summary>
public sealed class RocksDbStoreOptions
{
    /// <summary>
    /// Gets or sets whether to enable Bloom filter for faster key existence checks.
    /// Default: true
    /// </summary>
    public bool EnableBloomFilter { get; set; } = true;

    /// <summary>
    /// Gets or sets the number of bits per key for the Bloom filter.
    /// Higher values reduce false positive rate but increase memory usage.
    /// Default: 10 bits per key (~1% false positive rate)
    /// </summary>
    public int BloomFilterBitsPerKey { get; set; } = 10;

    /// <summary>
    /// Gets or sets the block cache size in bytes.
    /// Default: 64MB
    /// </summary>
    public ulong BlockCacheSize { get; set; } = 64 * 1024 * 1024;

    /// <summary>
    /// Gets or sets the write buffer size in bytes.
    /// Default: 64MB
    /// </summary>
    public ulong WriteBufferSize { get; set; } = 64 * 1024 * 1024;

    /// <summary>
    /// Gets or sets the maximum number of open files.
    /// Default: 1000
    /// </summary>
    public int MaxOpenFiles { get; set; } = 1000;

    /// <summary>
    /// Gets or sets the maximum total WAL (Write-Ahead Log) size in bytes.
    /// Default: 64MB
    /// </summary>
    public ulong MaxTotalWalSize { get; set; } = 64 * 1024 * 1024;

    /// <summary>
    /// Gets or sets the number of log files to keep.
    /// Default: 1
    /// </summary>
    public ulong KeepLogFileNum { get; set; } = 1;

    /// <summary>
    /// Gets or sets the compression type.
    /// Default: Snappy
    /// </summary>
    public RocksDbCompressionType CompressionType { get; set; } = RocksDbCompressionType.Snappy;

    /// <summary>
    /// Gets or sets whether to enable whole key filtering in Bloom filter.
    /// When true, the entire key is used for filtering (recommended).
    /// Default: true
    /// </summary>
    public bool WholeKeyFiltering { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to cache index and filter blocks.
    /// Default: true
    /// </summary>
    public bool CacheIndexAndFilterBlocks { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to pin L0 filter and index blocks in cache.
    /// Default: true
    /// </summary>
    public bool PinL0FilterAndIndexBlocksInCache { get; set; } = true;

    /// <summary>
    /// Creates a default instance of <see cref="RocksDbStoreOptions"/>.
    /// </summary>
    public static RocksDbStoreOptions Default => new();
}

/// <summary>
/// Compression types supported by RocksDB.
/// </summary>
public enum RocksDbCompressionType
{
    /// <summary>
    /// No compression.
    /// </summary>
    None = 0,

    /// <summary>
    /// Snappy compression (fast, moderate compression ratio).
    /// </summary>
    Snappy = 1,

    /// <summary>
    /// LZ4 compression (very fast, moderate compression ratio).
    /// </summary>
    Lz4 = 4,

    /// <summary>
    /// Zstd compression (slower, high compression ratio).
    /// </summary>
    Zstd = 7
}
