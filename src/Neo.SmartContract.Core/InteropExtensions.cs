// Copyright (C) 2015-2025 The Neo Project.
//
// InteropExtensions.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.VM.Types;
using System.Runtime.CompilerServices;

namespace Neo.SmartContract
{
    /// <summary>
    /// Extension helpers for VM interoperability types.
    /// </summary>
    public static class InteropExtensions
    {
        /// <summary>
        /// Converts the <see cref="StackItem"/> to an <see cref="IInteroperable"/>.
        /// </summary>
        /// <typeparam name="T">The interoperable type.</typeparam>
        /// <param name="item">The stack item to convert.</param>
        /// <returns>The converted interoperable instance.</returns>
        public static T ToInteroperable<T>(this StackItem item) where T : IInteroperable
        {
            T t = (T)RuntimeHelpers.GetUninitializedObject(typeof(T));
            t.FromStackItem(item);
            return t;
        }
    }
}

