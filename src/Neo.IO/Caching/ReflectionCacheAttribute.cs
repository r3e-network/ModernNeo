// Copyright (C) 2015-2025 The Neo Project.
//
// ReflectionCacheAttribute.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using System;
using System.Diagnostics.CodeAnalysis;

namespace Neo.IO.Caching
{
    /// <summary>
    /// Attribute for mapping enum values to payload types via reflection cache.
    /// </summary>
    /// <param name="typeName">Fully qualified type name including assembly</param>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
    internal class ReflectionCacheAttribute
        ([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] string typeName) : Attribute
    {
        /// <summary>
        /// Gets the type name.
        /// </summary>
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)]
        public string TypeName { get; } = typeName;

        /// <summary>
        /// Gets the resolved Type from the type name.
        /// WARNING: This uses Type.GetType which is not trim-safe.
        /// </summary>
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)]
        public Type? Type => Type.GetType(TypeName);

        /// <summary>
        /// Gets the resolved Type from the type name with explicit warning.
        /// </summary>
        [RequiresUnreferencedCode("Type.GetType is not trim-safe. The type might be removed during trimming.")]
        [return: DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)]
        public Type? GetTypeUnsafe() => Type.GetType(TypeName);
    }
}
