// Copyright (C) 2015-2025 The Neo Project.
//
// ConsensusMessageType.cs file belongs to the neo project and is free
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
    /// Types of consensus messages in dBFT protocol.
    /// </summary>
    public enum ConsensusMessageType : byte
    {
        /// <summary>
        /// Change view request when timeout occurs.
        /// </summary>
        ChangeView = 0x00,

        /// <summary>
        /// Prepare request from primary containing proposed block.
        /// </summary>
        PrepareRequest = 0x20,

        /// <summary>
        /// Prepare response from backup validators.
        /// </summary>
        PrepareResponse = 0x21,

        /// <summary>
        /// Commit message with signature.
        /// </summary>
        Commit = 0x30,

        /// <summary>
        /// Recovery request to get missed messages.
        /// </summary>
        RecoveryRequest = 0x40,

        /// <summary>
        /// Recovery message containing missed consensus data.
        /// </summary>
        RecoveryMessage = 0x41
    }
}
