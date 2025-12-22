// Copyright (C) 2015-2025 The Neo Project.
//
// ConsensusPhase.cs file belongs to the neo project and is free
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
    /// Represents the phases in the dBFT (delegated Byzantine Fault Tolerance) consensus algorithm.
    /// </summary>
    public enum ConsensusPhase : byte
    {
        /// <summary>
        /// Initial state before consensus starts.
        /// </summary>
        Initial = 0,

        /// <summary>
        /// Node is the primary (speaker) for this view.
        /// </summary>
        Primary = 1,

        /// <summary>
        /// Node is a backup (validator) for this view.
        /// </summary>
        Backup = 2,

        /// <summary>
        /// Primary has sent a prepare request.
        /// </summary>
        RequestSent = 3,

        /// <summary>
        /// Backup has received a prepare request.
        /// </summary>
        RequestReceived = 4,

        /// <summary>
        /// Backup has sent a prepare response.
        /// </summary>
        ResponseSent = 5,

        /// <summary>
        /// Node has sent a commit message.
        /// </summary>
        CommitSent = 6,

        /// <summary>
        /// View change is in progress due to timeout or failure.
        /// </summary>
        ViewChanging = 7,

        /// <summary>
        /// Block has been committed and persisted.
        /// </summary>
        BlockSent = 8
    }
}
