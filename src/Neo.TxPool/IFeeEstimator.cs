// Copyright (C) 2015-2025 The Neo Project.
//
// IFeeEstimator.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

namespace Neo.TxPool
{
    /// <summary>
    /// Defines the interface for estimating transaction fees.
    /// </summary>
    public interface IFeeEstimator
    {
        /// <summary>
        /// Estimates the network fee per byte for a transaction to be included
        /// within the specified number of blocks.
        /// </summary>
        /// <param name="targetBlocks">Target number of blocks for inclusion (1 = next block).</param>
        /// <returns>Estimated fee per byte in GAS fractions.</returns>
        long EstimateFeePerByte(int targetBlocks = 1);

        /// <summary>
        /// Estimates the total network fee for a transaction of the given size.
        /// </summary>
        /// <param name="transactionSize">Size of the transaction in bytes.</param>
        /// <param name="targetBlocks">Target number of blocks for inclusion.</param>
        /// <returns>Estimated total network fee in GAS fractions.</returns>
        long EstimateNetworkFee(int transactionSize, int targetBlocks = 1);

        /// <summary>
        /// Gets the minimum fee per byte accepted by the network.
        /// </summary>
        long MinimumFeePerByte { get; }

        /// <summary>
        /// Gets the current average fee per byte in the memory pool.
        /// </summary>
        long AverageFeePerByte { get; }

        /// <summary>
        /// Gets fee statistics for the current memory pool state.
        /// </summary>
        FeeStatistics GetStatistics();
    }

    /// <summary>
    /// Represents fee statistics from the memory pool.
    /// </summary>
    public class FeeStatistics
    {
        /// <summary>
        /// Gets or sets the minimum fee per byte in the pool.
        /// </summary>
        public long MinFeePerByte { get; set; }

        /// <summary>
        /// Gets or sets the maximum fee per byte in the pool.
        /// </summary>
        public long MaxFeePerByte { get; set; }

        /// <summary>
        /// Gets or sets the average fee per byte in the pool.
        /// </summary>
        public long AverageFeePerByte { get; set; }

        /// <summary>
        /// Gets or sets the median fee per byte in the pool.
        /// </summary>
        public long MedianFeePerByte { get; set; }

        /// <summary>
        /// Gets or sets the number of transactions in the pool.
        /// </summary>
        public int TransactionCount { get; set; }

        /// <summary>
        /// Gets or sets the timestamp when statistics were calculated.
        /// </summary>
        public DateTime Timestamp { get; set; }
    }
}
