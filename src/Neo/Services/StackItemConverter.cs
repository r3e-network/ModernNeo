// Copyright (C) 2015-2025 The Neo Project.
//
// StackItemConverter.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Core.Interfaces;
using Neo.SmartContract;
using Neo.VM;
using Neo.VM.Types;
using System;

namespace Neo.Services
{
    /// <summary>
    /// Default implementation of <see cref="IStackItemConverter{TStackItem, TRefCounter}"/>
    /// that delegates to the existing IInteroperable interface methods.
    /// </summary>
    public class StackItemConverter : IStackItemConverter<StackItem, IReferenceCounter>
    {
        /// <summary>
        /// Singleton instance of the converter.
        /// </summary>
        public static readonly StackItemConverter Instance = new();

        private StackItemConverter() { }

        /// <inheritdoc/>
        public T FromStackItem<T>(StackItem stackItem) where T : IInteroperableBase, new()
        {
            if (!typeof(IInteroperable).IsAssignableFrom(typeof(T)))
            {
                throw new NotSupportedException($"Type {typeof(T).Name} does not implement IInteroperable");
            }

            var result = new T();
            if (result is IInteroperable interoperable)
            {
                interoperable.FromStackItem(stackItem);
            }
            return result;
        }

        /// <inheritdoc/>
        public StackItem ToStackItem(IInteroperableBase interoperable, IReferenceCounter? referenceCounter)
        {
            if (interoperable is IInteroperable i)
            {
                return i.ToStackItem(referenceCounter);
            }

            throw new NotSupportedException($"Type {interoperable.GetType().Name} does not implement IInteroperable");
        }
    }
}
