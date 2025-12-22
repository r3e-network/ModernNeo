// Copyright (C) 2015-2025 The Neo Project.
//
// TransactionQueryService.cs file belongs to the neo project and is free
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

namespace Neo.Services.Transactions
{
    /// <summary>
    /// Service for querying transactions from the blockchain.
    /// </summary>
    public sealed class TransactionQueryService : ITransactionQueryService
    {
        private readonly NeoSystem _system;

        public TransactionQueryService(NeoSystem system)
        {
            _system = system ?? throw new ArgumentNullException(nameof(system));
        }

        public Transaction? GetTransactionByHash(string hashHex)
        {
            if (string.IsNullOrWhiteSpace(hashHex)) return null;

            if (hashHex.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                hashHex = hashHex[2..];

            try
            {
                var bytes = Convert.FromHexString(hashHex);
                if (bytes.Length != 32) return null;

                var hash = new UInt256(bytes);
                var state = NativeContract.Ledger.GetTransactionState(_system.StoreView, hash);
                return state?.Transaction;
            }
            catch
            {
                return null;
            }
        }

        public IEnumerable<Transaction> GetMempoolTransactions(int count)
        {
            if (count <= 0) yield break;

            var taken = 0;
            foreach (var tx in _system.MemPool.GetVerifiedTransactions())
            {
                if (taken >= count) yield break;
                yield return tx;
                taken++;
            }
        }

        public bool TransactionExists(string hashHex)
        {
            if (string.IsNullOrWhiteSpace(hashHex)) return false;

            if (hashHex.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                hashHex = hashHex[2..];

            try
            {
                var bytes = Convert.FromHexString(hashHex);
                if (bytes.Length != 32) return false;

                var hash = new UInt256(bytes);
                return NativeContract.Ledger.ContainsTransaction(_system.StoreView, hash);
            }
            catch
            {
                return false;
            }
        }

        public uint? GetTransactionBlockIndex(string hashHex)
        {
            if (string.IsNullOrWhiteSpace(hashHex)) return null;

            if (hashHex.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                hashHex = hashHex[2..];

            try
            {
                var bytes = Convert.FromHexString(hashHex);
                if (bytes.Length != 32) return null;

                var hash = new UInt256(bytes);
                var state = NativeContract.Ledger.GetTransactionState(_system.StoreView, hash);
                return state?.BlockIndex;
            }
            catch
            {
                return null;
            }
        }
    }
}
