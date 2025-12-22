// Copyright (C) 2015-2025 The Neo Project.
//
// ListAddressRpcMethod.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Json;
using Neo.RPC;
using System;
using System.Threading.Tasks;

namespace Neo.Node.Rpc
{
    public sealed class ListAddressRpcMethod : IRpcMethod
    {
        private readonly WalletManager _walletManager;

        public string Name => "listaddress";

        public ListAddressRpcMethod(WalletManager walletManager)
        {
            _walletManager = walletManager ?? throw new ArgumentNullException(nameof(walletManager));
        }

        public Task<JToken?> ProcessAsync(JArray? parameters)
        {
            if (parameters is not null && parameters.Count > 0)
                throw new RpcException(RpcError.InvalidParams.Code, "No parameters expected.");

            var wallet = WalletRpcHelper.GetWalletOrThrow(_walletManager);
            var result = new JArray();

            foreach (var account in wallet.GetAccounts())
                result.Add(WalletRpcHelper.WalletAccountToJson(account));

            return Task.FromResult<JToken?>(result);
        }
    }
}
