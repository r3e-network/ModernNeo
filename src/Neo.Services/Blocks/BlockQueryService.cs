// Copyright (C) 2015-2025 The Neo Project.
//
// BlockQueryService.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Network.P2P.Payloads;
using Neo.SmartContract.Native;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Neo.Services.Blocks
{
    public sealed class BlockQueryService : IBlockQueryService
    {
        private readonly NeoSystem _system;

        public BlockQueryService(NeoSystem system)
        {
            _system = system;
        }

        public Block? GetBlockByIndex(uint index)
        {
            return NativeContract.Ledger.GetBlock(_system.StoreView, index);
        }

        public Block? GetBlockByHash(string hashHex)
        {
            if (string.IsNullOrWhiteSpace(hashHex)) return null;
            if (hashHex.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                hashHex = hashHex[2..];
            try
            {
                var bytes = Convert.FromHexString(hashHex);
                if (bytes.Length != 32) return null;
                var hash = new UInt256(bytes);
                return NativeContract.Ledger.GetBlock(_system.StoreView, hash);
            }
            catch
            {
                return null;
            }
        }

        public IEnumerable<Block> GetBlocks(uint fromIndex, int count)
        {
            if (count <= 0) yield break;
            var end = fromIndex + (uint)count - 1;
            for (uint i = fromIndex; i <= end; i++)
            {
                var b = NativeContract.Ledger.GetBlock(_system.StoreView, i);
                if (b != null) yield return b;
            }
        }
    }
}
