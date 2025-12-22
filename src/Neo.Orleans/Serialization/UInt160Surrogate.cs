// Copyright (C) 2015-2025 The Neo Project.
//
// UInt160Surrogate.cs file belongs to the neo project and is free
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
    /// Orleans surrogate for UInt160 serialization.
    /// </summary>
    [GenerateSerializer]
    public struct UInt160Surrogate
    {
        [Id(0)] public byte[] Data { get; set; }
    }

    /// <summary>
    /// Converter between UInt160 and its surrogate.
    /// </summary>
    [RegisterConverter]
    public sealed class UInt160SurrogateConverter : IConverter<UInt160, UInt160Surrogate>
    {
        public UInt160 ConvertFromSurrogate(in UInt160Surrogate surrogate) =>
            surrogate.Data?.Length == 20 ? new UInt160(surrogate.Data) : UInt160.Zero;

        public UInt160Surrogate ConvertToSurrogate(in UInt160 value) =>
            new() { Data = value.GetSpan().ToArray() };
    }
}
