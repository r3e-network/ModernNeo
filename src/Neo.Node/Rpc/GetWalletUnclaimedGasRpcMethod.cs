// Copyright (C) 2015-2025 The Neo Project.
//
// GetWalletUnclaimedGasRpcMethod.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Json;
using Neo.RPC;
using Neo.SmartContract.Native;
using System;
using System.Numerics;
using System.Threading.Tasks;

namespace Neo.Node.Rpc
{
    public sealed class GetWalletUnclaimedGasRpcMethod : IRpcMethod
    {
        private readonly WalletManager _walletManager;
        private readonly NeoSystemNode _node;

        public string Name => "getwalletunclaimedgas";

        public GetWalletUnclaimedGasRpcMethod(WalletManager walletManager, NeoSystemNode node)
        {
            _walletManager = walletManager ?? throw new ArgumentNullException(nameof(walletManager));
            _node = node ?? throw new ArgumentNullException(nameof(node));
        }

        public Task<JToken?> ProcessAsync(JArray? parameters)
        {
            if (parameters is not null && parameters.Count > 0)
                throw new RpcException(RpcError.InvalidParams.Code, "No parameters expected.");

            var wallet = WalletRpcHelper.GetWalletOrThrow(_walletManager);
            var snapshot = _node.System.StoreView;
            var currentIndex = NativeContract.Ledger.CurrentIndex(snapshot);
            var end = currentIndex == uint.MaxValue ? currentIndex : currentIndex + 1;

            BigInteger total = BigInteger.Zero;
            foreach (var account in wallet.GetAccounts())
            {
                if (account.WatchOnly)
                    continue;
                total += NativeContract.NEO.UnclaimedGas(snapshot, account.ScriptHash, end);
            }

            return Task.FromResult<JToken?>(new JString(total.ToString()));
        }
    }
}
