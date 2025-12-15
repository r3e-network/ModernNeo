// Copyright (C) 2015-2025 The Neo Project.
//
// ITransactionAttributeData.cs file belongs to the neo project and is free
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
    /// Defines the data contract for a transaction attribute without external dependencies.
    /// This interface allows lower layers to work with attribute data without depending
    /// on the full TransactionAttribute implementation that requires DataCache and NativeContract.
    /// </summary>
    public interface ITransactionAttributeData : ISerializable
    {
        /// <summary>
        /// The type of the attribute as a byte value.
        /// </summary>
        byte TypeValue { get; }

        /// <summary>
        /// Indicates whether multiple instances of this attribute are allowed.
        /// </summary>
        bool AllowMultiple { get; }
    }
}
