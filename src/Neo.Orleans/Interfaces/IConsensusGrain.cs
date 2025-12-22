// Copyright (C) 2015-2025 The Neo Project.
//
// IConsensusGrain.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

namespace Neo.Orleans.Interfaces
{
    /// <summary>
    /// Orleans Grain interface for dBFT consensus management.
    /// Replaces Akka.NET ConsensusService Actor.
    /// </summary>
    public interface IConsensusGrain : IGrainWithIntegerKey
    {
        /// <summary>
        /// Initializes the consensus grain with validator configuration.
        /// </summary>
        Task InitializeAsync(int myIndex, int validatorCount);

        /// <summary>
        /// Starts the consensus process.
        /// </summary>
        Task StartAsync();

        /// <summary>
        /// Stops the consensus process.
        /// </summary>
        Task StopAsync();

        /// <summary>
        /// Handles a consensus message from a peer.
        /// </summary>
        Task OnConsensusMessageAsync(byte[] message, string senderAddress);

        /// <summary>
        /// Gets the current consensus state.
        /// </summary>
        Task<ConsensusState> GetStateAsync();

        /// <summary>
        /// Gets the current view number.
        /// </summary>
        Task<byte> GetViewNumberAsync();

        /// <summary>
        /// Checks if this node is the primary for the current view.
        /// </summary>
        Task<bool> IsPrimaryAsync();
    }

    /// <summary>
    /// Consensus state information.
    /// </summary>
    [GenerateSerializer]
    public record ConsensusState(
        [property: Id(0)] byte ViewNumber,
        [property: Id(1)] uint BlockIndex,
        [property: Id(2)] ConsensusPhase Phase,
        [property: Id(3)] bool IsPrimary);

    /// <summary>
    /// Consensus phase in dBFT.
    /// </summary>
    public enum ConsensusPhase
    {
        Initial,
        Primary,
        Backup,
        RequestSent,
        ResponseSent,
        CommitSent,
        ViewChanging
    }
}
