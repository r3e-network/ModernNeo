// Copyright (C) 2015-2025 The Neo Project.
//
// TransactionAttributeType.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.IO.Caching;

namespace Neo.Network.P2P.Payloads
{
    /// <summary>
    /// Represents the type of a transaction attribute.
    /// </summary>
    public enum TransactionAttributeType : byte
    {
        /// <summary>
        /// Indicates that the transaction is of high priority.
        /// </summary>
        [ReflectionCache("Neo.Network.P2P.Payloads.HighPriorityAttribute, Neo")]
        HighPriority = 0x01,

        /// <summary>
        /// Indicates that the transaction is an oracle response.
        /// </summary>
        [ReflectionCache("Neo.Network.P2P.Payloads.OracleResponse, Neo")]
        OracleResponse = 0x11,

        /// <summary>
        /// Indicates that the transaction is not valid before a specified height.
        /// </summary>
        [ReflectionCache("Neo.Network.P2P.Payloads.NotValidBefore, Neo")]
        NotValidBefore = 0x20,

        /// <summary>
        /// Indicates that the transaction conflicts with another transaction.
        /// </summary>
        [ReflectionCache("Neo.Network.P2P.Payloads.Conflicts, Neo")]
        Conflicts = 0x21,

        /// <summary>
        /// Indicates that the transaction uses notary request service.
        /// </summary>
        [ReflectionCache("Neo.Network.P2P.Payloads.NotaryAssisted, Neo")]
        NotaryAssisted = 0x22
    }
}

