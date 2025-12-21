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
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;

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

        /// <summary>
        /// Converts a byte array to an <see cref="ISerializable"/> object.
        /// </summary>
        /// <param name="value">The byte array to be converted.</param>
        /// <param name="type">The type to convert to.</param>
        /// <returns>The converted <see cref="ISerializable"/> object.</returns>
        [RequiresUnreferencedCode("AsSerializable uses Activator.CreateInstance which requires unreferenced code.")]
        public static ISerializable AsSerializable(this ReadOnlyMemory<byte> value,
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] Type type)
        {
            if (!typeof(ISerializable).GetTypeInfo().IsAssignableFrom(type))
                throw new InvalidCastException($"`{type.Name}` is not assignable from `ISerializable`");
            var serializable = (ISerializable)Activator.CreateInstance(type)!;
            MemoryReader reader = new(value);
            serializable.Deserialize(ref reader);
            return serializable;
        }

        /// <summary>
        /// Converts a byte array to an <see cref="ISerializable"/> object.
        /// Uses Activator.CreateInstance for types with required members.
        /// </summary>
        /// <typeparam name="T">The type to convert to.</typeparam>
        /// <param name="value">The byte array to be converted.</param>
        /// <returns>The converted <see cref="ISerializable"/> object.</returns>
        [RequiresUnreferencedCode("AsSerializable uses Activator.CreateInstance which requires unreferenced code.")]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T AsSerializable<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] T>(this ReadOnlyMemory<byte> value) where T : ISerializable
        {
            T serializable = (T)Activator.CreateInstance(typeof(T))!;
            MemoryReader reader = new(value);
            serializable.Deserialize(ref reader);
            return serializable;
        }

        /// <summary>
        /// Converts a byte array to an <see cref="ISerializable"/> object.
        /// Uses Activator.CreateInstance for types with required members.
        /// </summary>
        /// <typeparam name="T">The type to convert to.</typeparam>
        /// <param name="value">The byte array to be converted.</param>
        /// <returns>The converted <see cref="ISerializable"/> object.</returns>
        [RequiresUnreferencedCode("AsSerializable uses Activator.CreateInstance which requires unreferenced code.")]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T AsSerializable<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] T>(this byte[] value) where T : ISerializable
        {
            return AsSerializable<T>(value.AsMemory());
        }

        /// <summary>
        /// Converts a byte array to an <see cref="ISerializable"/> object.
        /// Optimized version that avoids reflection for types with parameterless constructors.
        /// </summary>
        /// <typeparam name="T">The type to convert to.</typeparam>
        /// <param name="value">The byte array to be converted.</param>
        /// <returns>The converted <see cref="ISerializable"/> object.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T AsSerializableFast<T>(this ReadOnlyMemory<byte> value) where T : ISerializable, new()
        {
            T serializable = new();
            MemoryReader reader = new(value);
            serializable.Deserialize(ref reader);
            return serializable;
        }

        /// <summary>
        /// Converts a byte array to an <see cref="ISerializable"/> object.
        /// Optimized version that avoids reflection for types with parameterless constructors.
        /// </summary>
        /// <typeparam name="T">The type to convert to.</typeparam>
        /// <param name="value">The byte array to be converted.</param>
        /// <returns>The converted <see cref="ISerializable"/> object.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T AsSerializableFast<T>(this byte[] value) where T : ISerializable, new()
        {
            return AsSerializableFast<T>(value.AsMemory());
        }

        /// <summary>
        /// Converts a span to an <see cref="ISerializable"/> object.
        /// Optimized version that avoids reflection for types with parameterless constructors.
        /// </summary>
        /// <typeparam name="T">The type to convert to.</typeparam>
        /// <param name="value">The span to be converted.</param>
        /// <returns>The converted <see cref="ISerializable"/> object.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T AsSerializableFast<T>(this ReadOnlySpan<byte> value) where T : ISerializable, new()
        {
            T serializable = new();
            MemoryReader reader = new(value.ToArray());
            serializable.Deserialize(ref reader);
            return serializable;
        }
    }
}
