// Copyright (C) 2015-2025 The Neo Project.
//
// IBlockQueryService.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Network.P2P.Payloads;
using System.Collections.Generic;

namespace Neo.Services.Blocks
{
    public interface IBlockQueryService
    {
        Block? GetBlockByIndex(uint index);
        Block? GetBlockByHash(string hashHex);
        IEnumerable<Block> GetBlocks(uint fromIndex, int count);
    }
}
