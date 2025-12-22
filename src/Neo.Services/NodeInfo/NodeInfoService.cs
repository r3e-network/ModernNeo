// Copyright (C) 2015-2025 The Neo Project.
//
// NodeInfoService.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo;
using Neo.Ledger;
using Neo.SmartContract.Native;

namespace Neo.Services.NodeInfo
{
    public sealed class NodeInfoService : INodeInfoService
    {
        private readonly NeoSystem _system;

        public NodeInfoService(NeoSystem system)
        {
            _system = system;
        }

        public string Network => _system.Settings.Network.ToString();

        public long Height => (long)NativeContract.Ledger.CurrentIndex(_system.StoreView);

        public int MempoolCount => _system.MemPool.Count;
    }
}
