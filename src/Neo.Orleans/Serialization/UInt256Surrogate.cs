// Copyright (C) 2015-2025 The Neo Project.
//
// UInt256Surrogate.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Orleans;

namespace Neo.Orleans.Serialization
{
    /// <summary>
    /// Orleans surrogate for UInt256 serialization.
    /// </summary>
    [GenerateSerializer]
    public struct UInt256Surrogate
    {
        [Id(0)] public byte[] Data { get; set; }
    }

    /// <summary>
    /// Converter between UInt256 and its surrogate.
    /// </summary>
    [RegisterConverter]
    public sealed class UInt256SurrogateConverter : IConverter<UInt256, UInt256Surrogate>
    {
        public UInt256 ConvertFromSurrogate(in UInt256Surrogate surrogate) =>
            surrogate.Data?.Length == 32 ? new UInt256(surrogate.Data) : UInt256.Zero;

        public UInt256Surrogate ConvertToSurrogate(in UInt256 value) =>
            new() { Data = value.GetSpan().ToArray() };
    }
}
