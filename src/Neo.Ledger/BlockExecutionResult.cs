// Copyright (C) 2015-2025 The Neo Project.
//
// BlockExecutionResult.cs file belongs to the neo project and is free
// software distributed under the MIT software license.

namespace Neo.Ledger;

/// <summary>
/// Default implementation of block execution result.
/// </summary>
public class BlockExecutionResult : IBlockExecutionResult
{
    /// <inheritdoc/>
    public byte[] TransactionHash { get; init; } = Array.Empty<byte>();

    /// <inheritdoc/>
    public byte State { get; init; }

    /// <inheritdoc/>
    public bool ShouldCommit { get; init; }

    /// <inheritdoc/>
    public object? ApplicationExecuted { get; init; }

    /// <summary>
    /// Creates a successful result that should be committed.
    /// </summary>
    public static BlockExecutionResult Success(byte[] hash, object applicationExecuted)
    {
        return new BlockExecutionResult
        {
            TransactionHash = hash,
            State = 1, // HALT
            ShouldCommit = true,
            ApplicationExecuted = applicationExecuted
        };
    }

    /// <summary>
    /// Creates a failed result that should not be committed.
    /// </summary>
    public static BlockExecutionResult Failure(byte[] hash, object applicationExecuted)
    {
        return new BlockExecutionResult
        {
            TransactionHash = hash,
            State = 2, // FAULT
            ShouldCommit = false,
            ApplicationExecuted = applicationExecuted
        };
    }
}
