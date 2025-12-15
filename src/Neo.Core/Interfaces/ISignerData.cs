// Copyright (C) 2015-2025 The Neo Project.
//
// ISignerData.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.IO;

namespace Neo.Core.Interfaces
{
    /// <summary>
    /// Defines the data contract for a transaction signer without VM dependencies.
    /// This interface allows lower layers to work with signer data without depending
    /// on the full Signer implementation that requires Neo.VM.
    /// </summary>
    public interface ISignerData : ISerializable
    {
        /// <summary>
        /// The account of the signer.
        /// </summary>
        UInt160 Account { get; }

        /// <summary>
        /// The scopes of the witness as a byte value.
        /// </summary>
        byte ScopesValue { get; }

        /// <summary>
        /// The number of allowed contracts.
        /// </summary>
        int AllowedContractsCount { get; }

        /// <summary>
        /// The number of allowed groups.
        /// </summary>
        int AllowedGroupsCount { get; }

        /// <summary>
        /// The number of witness rules.
        /// </summary>
        int RulesCount { get; }
    }
}
