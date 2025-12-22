// Copyright (C) 2015-2025 The Neo Project.
//
// DumpPrivKeyRpcMethod.cs file belongs to the neo project and is free
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
    public sealed class DumpPrivKeyRpcMethod : IRpcMethod
    {
        private readonly WalletManager _walletManager;
        private readonly NeoSystemNode _node;

        public string Name => "dumpprivkey";

        public DumpPrivKeyRpcMethod(WalletManager walletManager, NeoSystemNode node)
        {
            _walletManager = walletManager ?? throw new ArgumentNullException(nameof(walletManager));
            _node = node ?? throw new ArgumentNullException(nameof(node));
        }

        public Task<JToken?> ProcessAsync(JArray? parameters)
        {
            if (parameters is null || parameters.Count == 0 || parameters[0] is null)
                throw new RpcException(RpcError.InvalidParams.Code, "Missing address.");

            var wallet = WalletRpcHelper.GetWalletOrThrow(_walletManager);
            var accountHash = WalletRpcHelper.ParseScriptHash(parameters[0]!, _node.System.Settings, "address");
            var account = wallet.GetAccount(accountHash);
            if (account is null)
                throw new RpcException(RpcError.InvalidParams.Code, "Account not found.");
            if (!account.HasKey)
                throw new RpcException(RpcError.InvalidParams.Code, "Account has no key.");

            var key = account.GetKey();
            if (key is null)
                throw new RpcException(RpcError.InternalError.Code, "Failed to retrieve account key.");

            return Task.FromResult<JToken?>(new JString(key.Export()));
        }
    }
}
