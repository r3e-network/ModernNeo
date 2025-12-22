// Copyright (C) 2015-2025 The Neo Project.
//
// IConsensusService.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

namespace Neo.Consensus
{
    /// <summary>
    /// Defines the interface for consensus service operations.
    /// This is a dependency-free abstraction for dBFT consensus.
    /// </summary>
    public interface IConsensusService
    {
        /// <summary>
        /// Gets whether the consensus service is currently running.
        /// </summary>
        bool IsRunning { get; }

        /// <summary>
        /// Gets the current consensus context.
        /// </summary>
        IConsensusContext? Context { get; }

        /// <summary>
        /// Starts the consensus service.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task StartAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Stops the consensus service.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task StopAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Handles a consensus message from a peer.
        /// </summary>
        /// <param name="message">The consensus message payload.</param>
        /// <param name="senderPublicKey">The sender's public key.</param>
        Task OnMessageAsync(IConsensusMessage message, byte[] senderPublicKey);

        /// <summary>
        /// Called when a new block is persisted.
        /// </summary>
        /// <param name="blockIndex">The index of the persisted block.</param>
        Task OnBlockPersistedAsync(uint blockIndex);

        /// <summary>
        /// Gets the current state snapshot.
        /// </summary>
        Task<ConsensusStateSnapshot> GetStateAsync();

        /// <summary>
        /// Raised when a consensus message needs to be broadcast.
        /// </summary>
        event EventHandler<ConsensusMessageEventArgs>? MessageGenerated;

        /// <summary>
        /// Raised when a block is ready to be committed.
        /// </summary>
        event EventHandler<BlockReadyEventArgs>? BlockReady;

        /// <summary>
        /// Raised when a view change occurs.
        /// </summary>
        event EventHandler<ViewChangedEventArgs>? ViewChanged;
    }

    /// <summary>
    /// Event arguments for consensus message generation.
    /// </summary>
    public class ConsensusMessageEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the consensus message to broadcast.
        /// </summary>
        public IConsensusMessage Message { get; }

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        public ConsensusMessageEventArgs(IConsensusMessage message)
        {
            Message = message;
        }
    }

    /// <summary>
    /// Event arguments for block ready event.
    /// </summary>
    public class BlockReadyEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the block index.
        /// </summary>
        public uint BlockIndex { get; }

        /// <summary>
        /// Gets the block hash.
        /// </summary>
        public byte[] BlockHash { get; }

        /// <summary>
        /// Gets the collected signatures.
        /// </summary>
        public IReadOnlyList<byte[]> Signatures { get; }

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        public BlockReadyEventArgs(uint blockIndex, byte[] blockHash, IReadOnlyList<byte[]> signatures)
        {
            BlockIndex = blockIndex;
            BlockHash = blockHash;
            Signatures = signatures;
        }
    }

    /// <summary>
    /// Event arguments for view change event.
    /// </summary>
    public class ViewChangedEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the previous view number.
        /// </summary>
        public byte OldViewNumber { get; }

        /// <summary>
        /// Gets the new view number.
        /// </summary>
        public byte NewViewNumber { get; }

        /// <summary>
        /// Gets the reason for view change.
        /// </summary>
        public ViewChangeReason Reason { get; }

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        public ViewChangedEventArgs(byte oldViewNumber, byte newViewNumber, ViewChangeReason reason)
        {
            OldViewNumber = oldViewNumber;
            NewViewNumber = newViewNumber;
            Reason = reason;
        }
    }

    /// <summary>
    /// Reasons for view change.
    /// </summary>
    public enum ViewChangeReason
    {
        /// <summary>
        /// Timeout waiting for primary.
        /// </summary>
        Timeout,

        /// <summary>
        /// Primary sent invalid block.
        /// </summary>
        InvalidBlock,

        /// <summary>
        /// Received view change request from other validators.
        /// </summary>
        ViewChangeRequest,

        /// <summary>
        /// Block was successfully committed.
        /// </summary>
        BlockCommitted
    }
}
