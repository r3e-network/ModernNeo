// Copyright (C) 2015-2025 The Neo Project.
//
// MemoryReaderExtensions.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Neo.IO
{
    /// <summary>
    /// A helper class for serialization of NEO objects.
    /// </summary>
    public static class MemoryReaderExtensions
    {
        /// <summary>
        /// Reads an <see cref="ISerializable"/> array from a <see cref="MemoryReader"/>.
        /// </summary>
        /// <typeparam name="T">The type of the array element.</typeparam>
        /// <param name="reader">The <see cref="MemoryReader"/> for reading data.</param>
        /// <param name="max">The maximum number of elements in the array.</param>
        /// <returns>The array read from the <see cref="MemoryReader"/>.</returns>
        [RequiresUnreferencedCode("ReadNullableArray uses RuntimeHelpers.GetUninitializedObject which requires unreferenced code.")]
        public static T?[] ReadNullableArray<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors)] T>(this ref MemoryReader reader, int max = 0x1000000)
            where T : class, ISerializable
        {
            var array = new T?[reader.ReadVarInt((ulong)max)];
            for (var i = 0; i < array.Length; i++)
                array[i] = reader.ReadBoolean() ? reader.ReadSerializable<T>() : null;
            return array;
        }

        /// <summary>
        /// Reads an <see cref="ISerializable"/> object from a <see cref="MemoryReader"/>.
        /// </summary>
        /// <typeparam name="T">The type of the <see cref="ISerializable"/> object.</typeparam>
        /// <param name="reader">The <see cref="MemoryReader"/> for reading data.</param>
        /// <returns>The object read from the <see cref="MemoryReader"/>.</returns>
        [RequiresUnreferencedCode("ReadSerializable uses RuntimeHelpers.GetUninitializedObject which requires unreferenced code.")]
        public static T ReadSerializable<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors)] T>(this ref MemoryReader reader)
            where T : ISerializable
        {
            T obj = (T)RuntimeHelpers.GetUninitializedObject(typeof(T));
            obj.Deserialize(ref reader);
            return obj;
        }

        /// <summary>
        /// Reads an <see cref="ISerializable"/> array from a <see cref="MemoryReader"/>.
        /// </summary>
        /// <typeparam name="T">The type of the array element.</typeparam>
        /// <param name="reader">The <see cref="MemoryReader"/> for reading data.</param>
        /// <param name="max">The maximum number of elements in the array.</param>
        /// <returns>The array read from the <see cref="MemoryReader"/>.</returns>
        [RequiresUnreferencedCode("ReadSerializableArray uses RuntimeHelpers.GetUninitializedObject which requires unreferenced code.")]
        public static T[] ReadSerializableArray<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors)] T>(this ref MemoryReader reader, int max = 0x1000000)
            where T : ISerializable
        {
            var array = new T[reader.ReadVarInt((ulong)max)];
            for (var i = 0; i < array.Length; i++)
            {
                array[i] = (T)RuntimeHelpers.GetUninitializedObject(typeof(T));
                array[i].Deserialize(ref reader);
            }
            return array;
        }
    }
}
