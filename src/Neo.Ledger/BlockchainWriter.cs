// Copyright (C) 2015-2025 The Neo Project.
//
// BlockchainWriter.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Core.Interfaces;

namespace Neo.Ledger
{
    /// <summary>
    /// Blockchain writer implementation that handles block persistence and verification.
    /// </summary>
    public sealed class BlockchainWriter : IBlockchainWriter
    {
        private readonly BlockchainState _state;
        private readonly IBlockExecutor _executor;
        private readonly IBlockValidator? _validator;
        private readonly SemaphoreSlim _persistLock = new(1, 1);

        /// <summary>
        /// Event raised when a block is persisted.
        /// </summary>
        public event EventHandler<BlockPersistedEventArgs>? BlockPersisted;

        /// <summary>
        /// Creates a new blockchain writer.
        /// </summary>
        /// <param name="state">The blockchain state to update.</param>
        /// <param name="executor">The block executor for transaction execution.</param>
        /// <param name="validator">Optional block validator.</param>
        public BlockchainWriter(
            BlockchainState state,
            IBlockExecutor executor,
            IBlockValidator? validator = null)
        {
            _state = state ?? throw new ArgumentNullException(nameof(state));
            _executor = executor ?? throw new ArgumentNullException(nameof(executor));
            _validator = validator;
        }

        /// <inheritdoc/>
        public async Task<bool> PersistBlockAsync(IBlockData block)
        {
            ArgumentNullException.ThrowIfNull(block);

            await _persistLock.WaitAsync();
            try
            {
                // Verify block if validator is available
                if (_validator != null)
                {
                    var verifyResult = await VerifyBlockAsync(block);
                    if (verifyResult != VerifyResult.Succeed)
                        return false;
                }

                // Check block index
                var expectedIndex = _state.Height + 1;
                if (block.Index != expectedIndex && _state.Height > 0)
                    return false;

                // Execute transactions (placeholder - actual execution would use ApplicationEngine)
                var transactions = new List<object>(); // Would be populated from block
                var results = _executor.Execute(transactions, new object(), block, new object());

                // Update state
                _state.OnBlockPersisted(block);

                // Raise event
                BlockPersisted?.Invoke(this, new BlockPersistedEventArgs(block, block.Index));

                return true;
            }
            finally
            {
                _persistLock.Release();
            }
        }

        /// <inheritdoc/>
        public async Task<int> ImportBlocksAsync(IEnumerable<IBlockData> blocks, bool verify = true)
        {
            ArgumentNullException.ThrowIfNull(blocks);

            var imported = 0;
            foreach (var block in blocks.OrderBy(b => b.Index))
            {
                if (verify)
                {
                    var result = await VerifyBlockAsync(block);
                    if (result != VerifyResult.Succeed)
                        continue;
                }

                if (await PersistBlockAsync(block))
                    imported++;
            }

            return imported;
        }

        /// <inheritdoc/>
        public async Task<VerifyResult> VerifyBlockAsync(IBlockData block)
        {
            ArgumentNullException.ThrowIfNull(block);

            // Check if block already exists
            var hashBytes = GetBlockHashBytes(block);
            if (await _state.ContainsBlockAsync(hashBytes))
                return VerifyResult.AlreadyExists;

            // Check previous block hash
            if (block.Index > 0)
            {
                var prevBlock = await _state.GetBlockByIndexAsync(block.Index - 1);
                if (prevBlock == null)
                    return VerifyResult.UnableToVerify;

                var prevHash = GetBlockHashBytes(prevBlock);
                if (!block.PrevHash.GetSpan().SequenceEqual(prevHash))
                    return VerifyResult.Invalid;
            }

            // Use custom validator if available
            if (_validator != null)
                return await _validator.ValidateBlockAsync(block);

            return VerifyResult.Succeed;
        }

        /// <inheritdoc/>
        public async Task<VerifyResult> VerifyTransactionAsync(ITransactionData transaction)
        {
            ArgumentNullException.ThrowIfNull(transaction);

            // Check if transaction already exists
            var hashBytes = transaction.Hash.GetSpan().ToArray();
            if (await _state.ContainsTransactionAsync(hashBytes))
                return VerifyResult.AlreadyExists;

            // Basic validation
            if (transaction.ValidUntilBlock <= _state.Height)
                return VerifyResult.Expired;

            // Use custom validator if available
            if (_validator != null)
                return await _validator.ValidateTransactionAsync(transaction);

            return VerifyResult.Succeed;
        }

        private static byte[] GetBlockHashBytes(IBlockData block)
        {
            if (block is IVerifiableBase verifiable)
                return verifiable.Hash.GetSpan().ToArray();
            return block.MerkleRoot.GetSpan().ToArray();
        }
    }

    /// <summary>
    /// Interface for custom block and transaction validation.
    /// </summary>
    public interface IBlockValidator
    {
        /// <summary>
        /// Validates a block.
        /// </summary>
        Task<VerifyResult> ValidateBlockAsync(IBlockData block);

        /// <summary>
        /// Validates a transaction.
        /// </summary>
        Task<VerifyResult> ValidateTransactionAsync(ITransactionData transaction);
    }
}
