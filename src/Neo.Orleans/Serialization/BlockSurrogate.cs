// Copyright (C) 2015-2025 The Neo Project.
//
// BlockSurrogate.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.

using Neo.Extensions;
using Neo.IO;
using Neo.Network.P2P.Payloads;
using Orleans;
using System;

namespace Neo.Orleans.Serialization
{
    /// <summary>
    /// Orleans surrogate for Block serialization.
    /// </summary>
    [GenerateSerializer]
    public struct BlockSurrogate
    {
        [Id(0)] public byte[] Data { get; set; }
    }

    /// <summary>
    /// Converter between Block and its surrogate.
    /// </summary>
    [RegisterConverter]
    public sealed class BlockSurrogateConverter : IConverter<Block, BlockSurrogate>
    {
        public Block ConvertFromSurrogate(in BlockSurrogate surrogate)
        {
            if (surrogate.Data == null || surrogate.Data.Length == 0)
                throw new FormatException("Block data is missing.");

            var reader = new MemoryReader(surrogate.Data);
            return reader.ReadSerializable<Block>();
        }

        public BlockSurrogate ConvertToSurrogate(in Block value) =>
            new() { Data = value.ToArray() };
    }
}
