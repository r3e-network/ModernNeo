// Copyright (C) 2015-2025 The Neo Project.
//
// ImportPrivKeyRpcMethod.cs file belongs to the neo project and is free
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
    public sealed class ImportPrivKeyRpcMethod : IRpcMethod
    {
        private readonly WalletManager _walletManager;

        public string Name => "importprivkey";

        public ImportPrivKeyRpcMethod(WalletManager walletManager)
        {
            _walletManager = walletManager ?? throw new ArgumentNullException(nameof(walletManager));
        }

        public Task<JToken?> ProcessAsync(JArray? parameters)
        {
            if (parameters is null || parameters.Count == 0 || parameters[0] is null)
                throw new RpcException(RpcError.InvalidParams.Code, "Missing WIF key.");

            var wif = parameters[0]!.AsString();
            if (string.IsNullOrWhiteSpace(wif))
                throw new RpcException(RpcError.InvalidParams.Code, "Invalid WIF key.");

            var wallet = WalletRpcHelper.GetWalletOrThrow(_walletManager);
            try
            {
                var account = wallet.Import(wif);
                wallet.Save();
                return Task.FromResult<JToken?>(WalletRpcHelper.WalletAccountToJson(account));
            }
            catch (Exception ex)
            {
                throw new RpcException(RpcError.InvalidParams.Code, "Failed to import private key.", new JString(ex.Message));
            }
        }
    }
}
