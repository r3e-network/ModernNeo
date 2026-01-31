// Copyright (C) 2015-2025 The Neo Project.
//
// SerializationHelper.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Microsoft.Extensions.Logging;
using Neo.Core.Interfaces;
using Neo.Extensions;
using Neo.IO;
using Neo.Network.P2P.Payloads;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Neo.Orleans.Utilities
{
    /// <summary>
    /// Shared serialization helper methods for Orleans grains.
    /// Consolidates duplicate deserialization logic across grains.
    /// </summary>
    public static class SerializationHelper
    {
        private static readonly ILogger _logger = LoggerFactory.Create(b => b.AddConsole()).CreateLogger("SerializationHelper");
        /// <summary>
        /// Tries to deserialize a transaction from ITransactionData.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryDeserializeTransaction(ITransactionData transaction, [NotNullWhen(true)] out Transaction? tx)
        {
            if (transaction is Transaction fullTransaction)
            {
                tx = fullTransaction;
                return true;
            }
            return TryDeserialize(transaction.ToArray(), out tx);
        }

        /// <summary>
        /// Tries to deserialize a header from byte array.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryDeserializeHeader(byte[]? data, [NotNullWhen(true)] out Header? header) =>
            TryDeserialize(data, out header);

        /// <summary>
        /// Tries to deserialize a block from byte array.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryDeserializeBlock(byte[]? data, [NotNullWhen(true)] out Block? block) =>
            TryDeserialize(data, out block);

        /// <summary>
        /// Tries to deserialize an extensible payload from byte array.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryDeserializeExtensible(byte[]? data, [NotNullWhen(true)] out ExtensiblePayload? payload) =>
            TryDeserialize(data, out payload);

        /// <summary>
        /// Generic deserialization method that handles common error cases.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool TryDeserialize<T>(byte[]? data, [NotNullWhen(true)] out T? result) where T : class, ISerializable
        {
            result = null;
            if (data == null || data.Length == 0)
                return false;

            try
            {
                var reader = new MemoryReader(data);
                result = reader.ReadSerializable<T>();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    "Deserialization failed for {Type}. Data length: {Length}. Error: {Message}. Stack trace: {StackTrace}",
                    typeof(T).Name,
                    data.Length,
                    ex.Message,
                    ex.StackTrace);
                return false;
            }
        }
    }
}
