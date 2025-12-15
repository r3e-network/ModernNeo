// Copyright (C) 2015-2025 The Neo Project.
//
// MemoryExtensions.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Extensions;
using System;

namespace Neo.IO
{
    /// <summary>
    /// Extension methods for memory types used in serialization.
    /// </summary>
    public static class MemoryExtensions
    {
        /// <summary>
        /// Gets the size of the specified array encoded in variable-length encoding.
        /// </summary>
        /// <param name="value">The specified array.</param>
        /// <returns>The size of the array.</returns>
        public static int GetVarSize(this ReadOnlyMemory<byte> value)
        {
            return value.Length.GetVarSize() + value.Length;
        }
    }
}
