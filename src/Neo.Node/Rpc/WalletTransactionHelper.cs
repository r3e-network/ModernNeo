// Copyright (C) 2015-2025 The Neo Project.
//
// WalletTransactionHelper.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Akka.Actor;
using Neo;
using Neo.Ledger;
using Neo.Network.P2P.Payloads;
using Neo.RPC;
using Neo.SmartContract;
using Neo.Wallets;
using System;
using System.Threading.Tasks;

namespace Neo.Node.Rpc
{
    internal static class WalletTransactionHelper
    {
        private static readonly TimeSpan DefaultAskTimeout = TimeSpan.FromSeconds(30);

        public static async Task<Transaction> SignAndRelayAsync(NeoSystemNode node, Wallet wallet, Transaction tx)
        {
            var context = new ContractParametersContext(node.System.StoreView, tx, node.System.Settings.Network);
            var signed = wallet.Sign(context);
            if (!signed || !context.Completed)
                throw new RpcException(-500, "Insufficient signatures.");

            tx.Witnesses = context.GetWitnesses();

            var result = await node.System.Blockchain.Ask<Neo.Ledger.Blockchain.RelayResult>(tx, DefaultAskTimeout);
            if (result.Result != VerifyResult.Succeed)
                throw new RpcException(-500, result.Result.ToString());

            return tx;
        }
    }
}
