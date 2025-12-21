// Copyright (C) 2015-2025 The Neo Project.
//
// NeoResolver.cs file belongs to the neo project and is free
// software distributed under the MIT software license.

using System.Diagnostics.CodeAnalysis;
using MessagePack;
using MessagePack.Formatters;
using Neo.IO;
using Neo.Network.P2P.Payloads;
using Neo.Serialization.MessagePack.Formatters;

namespace Neo.Serialization.MessagePack.Resolvers;

/// <summary>
/// MessagePack resolver for Neo types.
/// Provides formatters for UInt160, UInt256, and explicitly registered ISerializable types.
/// </summary>
/// <remarks>
/// This resolver is NativeAOT compatible. It does not use dynamic code generation.
/// To add support for ISerializable types, use <see cref="RegisterSerializable{T}"/> at startup.
/// </remarks>
public sealed class NeoResolver : IFormatterResolver
{
    /// <summary>
    /// Singleton instance of the Neo resolver.
    /// </summary>
    public static readonly NeoResolver Instance = new();

    private NeoResolver() { }

    /// <summary>
    /// Registers an ISerializable type for MessagePack serialization.
    /// Call this method at application startup before any serialization occurs.
    /// </summary>
    /// <typeparam name="T">The ISerializable type to register.</typeparam>
    /// <remarks>
    /// This method is NativeAOT compatible as it uses explicit generic instantiation
    /// rather than runtime MakeGenericType calls.
    /// </remarks>
    public static void RegisterSerializable<T>() where T : ISerializable, new()
    {
        // ISerializableFormatter<T> implements IMessagePackFormatter<T?> (nullable)
        // Register for both T and T? lookups
        NeoResolverGetFormatterHelper.RegisterFormatterObject(typeof(T), ISerializableFormatter<T>.Instance);
    }

    /// <summary>
    /// Registers a custom formatter for a specific type.
    /// </summary>
    /// <typeparam name="T">The type to register.</typeparam>
    /// <param name="formatter">The formatter instance.</param>
    public static void RegisterFormatter<T>(IMessagePackFormatter<T> formatter)
    {
        NeoResolverGetFormatterHelper.RegisterFormatterObject(typeof(T), formatter);
    }

    public IMessagePackFormatter<T>? GetFormatter<T>()
    {
        return FormatterCache<T>.Formatter;
    }

    private static class FormatterCache<T>
    {
        public static readonly IMessagePackFormatter<T>? Formatter;

        static FormatterCache()
        {
            Formatter = (IMessagePackFormatter<T>?)NeoResolverGetFormatterHelper.GetFormatter(typeof(T));
        }
    }
}

internal static class NeoResolverGetFormatterHelper
{
    private static readonly Dictionary<Type, object> FormatterMap = new()
    {
        // Core hash types - these are always registered
        { typeof(UInt160), UInt160Formatter.Instance },
        { typeof(UInt256), UInt256Formatter.Instance },
        // Block and Transaction types
        { typeof(Block), BlockFormatter.Instance },
        { typeof(Header), HeaderFormatter.Instance },
        { typeof(Transaction), TransactionFormatter.Instance },
        { typeof(Witness), WitnessFormatter.Instance },
        { typeof(Signer), SignerFormatter.Instance },
    };

    // Lock for thread-safe registration
    private static readonly object _lock = new();

    /// <summary>
    /// Registers a formatter for a specific type.
    /// Thread-safe for concurrent registration during startup.
    /// </summary>
    internal static void RegisterFormatterObject(Type type, object formatter)
    {
        lock (_lock)
        {
            FormatterMap[type] = formatter;
        }
    }

    [UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
        Justification = "No dynamic code generation - only dictionary lookup")]
    public static object? GetFormatter(Type type)
    {
        // Direct lookup for registered types
        if (FormatterMap.TryGetValue(type, out var formatter))
            return formatter;

        // Check for nullable value types (e.g., UInt160?)
        var underlyingType = Nullable.GetUnderlyingType(type);
        if (underlyingType != null && FormatterMap.TryGetValue(underlyingType, out formatter))
            return formatter;

        // No dynamic MakeGenericType - types must be explicitly registered
        // This ensures NativeAOT compatibility
        return null;
    }
}
