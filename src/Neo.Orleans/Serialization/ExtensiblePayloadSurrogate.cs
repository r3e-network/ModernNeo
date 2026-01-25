// Copyright (C) 2015-2025 The Neo Project.
//
// ExtensiblePayloadSurrogate.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Extensions;
using Neo.IO;
using Neo.Network.P2P.Payloads;
using Orleans;
using System;

namespace Neo.Orleans.Serialization
{
    /// <summary>
    /// Orleans surrogate for ExtensiblePayload serialization.
    /// </summary>
    [GenerateSerializer]
    public struct ExtensiblePayloadSurrogate
    {
        [Id(0)] public byte[] Data { get; set; }
    }

    /// <summary>
    /// Converter between ExtensiblePayload and its surrogate.
    /// </summary>
    [RegisterConverter]
    public sealed class ExtensiblePayloadSurrogateConverter : IConverter<ExtensiblePayload, ExtensiblePayloadSurrogate>
    {
        public ExtensiblePayload ConvertFromSurrogate(in ExtensiblePayloadSurrogate surrogate)
        {
            if (surrogate.Data == null || surrogate.Data.Length == 0)
                throw new FormatException("ExtensiblePayload data is missing.");

            var reader = new MemoryReader(surrogate.Data);
            return reader.ReadSerializable<ExtensiblePayload>();
        }

        public ExtensiblePayloadSurrogate ConvertToSurrogate(in ExtensiblePayload value) =>
            new() { Data = value.ToArray() };
    }
}
