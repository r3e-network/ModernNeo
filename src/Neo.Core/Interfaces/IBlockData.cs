// Copyright (C) 2015-2025 The Neo Project.
//
// IBlockData.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using System.Collections.Generic;

namespace Neo.Core.Interfaces
{
    /// <summary>
    /// Represents immutable block data without concrete dependencies.
    /// </summary>
    public interface IBlockData : IHeaderData
    {
        /// <summary>
        /// The number of transactions in the block.
        /// </summary>
        int TransactionCount { get; }

        /// <summary>
        /// The transactions in the block.
        /// </summary>
        IReadOnlyList<ITransactionData> Transactions { get; }
    }
}
