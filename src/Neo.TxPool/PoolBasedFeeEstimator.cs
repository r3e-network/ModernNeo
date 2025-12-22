// Copyright (C) 2015-2025 The Neo Project.
//
// PoolBasedFeeEstimator.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Core.Interfaces;

namespace Neo.TxPool
{
    /// <summary>
    /// Fee estimator that analyzes the current memory pool to estimate fees.
    /// </summary>
    public class PoolBasedFeeEstimator : IFeeEstimator
    {
        private readonly ITransactionPool _pool;
        private readonly long _minimumFeePerByte;
        private readonly int _blockCapacity;

        /// <summary>
        /// Initializes a new instance of the <see cref="PoolBasedFeeEstimator"/> class.
        /// </summary>
        /// <param name="pool">The transaction pool to analyze.</param>
        /// <param name="minimumFeePerByte">Minimum fee per byte (default: 1000 GAS fractions).</param>
        /// <param name="blockCapacity">Estimated transactions per block (default: 500).</param>
        public PoolBasedFeeEstimator(
            ITransactionPool pool,
            long minimumFeePerByte = 1000,
            int blockCapacity = 500)
        {
            _pool = pool ?? throw new ArgumentNullException(nameof(pool));
            _minimumFeePerByte = minimumFeePerByte;
            _blockCapacity = blockCapacity;
        }

        /// <inheritdoc/>
        public long MinimumFeePerByte => _minimumFeePerByte;

        /// <inheritdoc/>
        public long AverageFeePerByte
        {
            get
            {
                var transactions = _pool.GetVerifiedTransactions().ToList();
                if (transactions.Count == 0)
                    return _minimumFeePerByte;

                return (long)transactions.Average(tx => tx.FeePerByte);
            }
        }

        /// <inheritdoc/>
        public long EstimateFeePerByte(int targetBlocks = 1)
        {
            if (targetBlocks < 1)
                targetBlocks = 1;

            var transactions = _pool.GetVerifiedTransactions()
                .OrderByDescending(tx => tx.FeePerByte)
                .ToList();

            if (transactions.Count == 0)
                return _minimumFeePerByte;

            // Calculate position in queue based on target blocks
            var targetPosition = targetBlocks * _blockCapacity;

            if (targetPosition >= transactions.Count)
            {
                // Pool is not full, minimum fee should work
                return _minimumFeePerByte;
            }

            // Get the fee at the target position (with some margin)
            var marginPosition = Math.Min(targetPosition - 1, transactions.Count - 1);
            var estimatedFee = transactions[marginPosition].FeePerByte;

            // Add 10% margin for safety
            return Math.Max((long)(estimatedFee * 1.1), _minimumFeePerByte);
        }

        /// <inheritdoc/>
        public long EstimateNetworkFee(int transactionSize, int targetBlocks = 1)
        {
            var feePerByte = EstimateFeePerByte(targetBlocks);
            return feePerByte * transactionSize;
        }

        /// <inheritdoc/>
        public FeeStatistics GetStatistics()
        {
            var transactions = _pool.GetVerifiedTransactions().ToList();
            var timestamp = DateTime.UtcNow;

            if (transactions.Count == 0)
            {
                return new FeeStatistics
                {
                    MinFeePerByte = _minimumFeePerByte,
                    MaxFeePerByte = _minimumFeePerByte,
                    AverageFeePerByte = _minimumFeePerByte,
                    MedianFeePerByte = _minimumFeePerByte,
                    TransactionCount = 0,
                    Timestamp = timestamp
                };
            }

            var fees = transactions.Select(tx => tx.FeePerByte).OrderBy(f => f).ToList();

            return new FeeStatistics
            {
                MinFeePerByte = fees.First(),
                MaxFeePerByte = fees.Last(),
                AverageFeePerByte = (long)fees.Average(),
                MedianFeePerByte = fees[fees.Count / 2],
                TransactionCount = transactions.Count,
                Timestamp = timestamp
            };
        }
    }
}
