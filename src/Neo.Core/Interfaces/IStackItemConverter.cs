// Copyright (C) 2015-2025 The Neo Project.
//
// IStackItemConverter.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

namespace Neo.Core.Interfaces
{
    /// <summary>
    /// Service interface for converting objects to/from VM stack items.
    /// This separates VM conversion logic from the interoperable objects themselves,
    /// breaking the circular dependency between protocol types and Neo.VM.
    /// </summary>
    /// <remarks>
    /// Implementations of this interface will have access to StackItem and
    /// IReferenceCounter types from Neo.VM, allowing conversion without the
    /// IInteroperableBase interface needing to reference VM types directly.
    /// </remarks>
    /// <typeparam name="TStackItem">The stack item type (typically Neo.VM.Types.StackItem).</typeparam>
    /// <typeparam name="TRefCounter">The reference counter type (typically Neo.VM.IReferenceCounter).</typeparam>
    public interface IStackItemConverter<TStackItem, TRefCounter>
        where TStackItem : class
        where TRefCounter : class
    {
        /// <summary>
        /// Converts a stack item to an interoperable object.
        /// </summary>
        /// <typeparam name="T">The target type.</typeparam>
        /// <param name="stackItem">The stack item to convert.</param>
        /// <returns>The converted object.</returns>
        T FromStackItem<T>(TStackItem stackItem) where T : IInteroperableBase, new();

        /// <summary>
        /// Converts an interoperable object to a stack item.
        /// </summary>
        /// <param name="interoperable">The object to convert.</param>
        /// <param name="referenceCounter">The reference counter for the stack item.</param>
        /// <returns>The converted stack item.</returns>
        TStackItem ToStackItem(IInteroperableBase interoperable, TRefCounter? referenceCounter);
    }
}
