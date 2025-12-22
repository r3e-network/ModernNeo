// Copyright (C) 2015-2025 The Neo Project.
//
// ITransactionQueryService.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Network.P2P.Payloads;
using System.Collections.Generic;

namespace Neo.Services.Transactions
{
    /// <summary>
    /// Service for querying transactions from the blockchain.
    /// </summary>
    public interface ITransactionQueryService
    {
        /// <summary>
        /// Gets a transaction by its hash.
        /// </summary>
        /// <param name="hashHex">Transaction hash in hex format (with or without 0x prefix).</param>
        /// <returns>The transaction if found, null otherwise.</returns>
        Transaction? GetTransactionByHash(string hashHex);

        /// <summary>
        /// Gets transactions from the mempool.
        /// </summary>
        /// <param name="count">Maximum number of transactions to return.</param>
        /// <returns>Collection of unconfirmed transactions.</returns>
        IEnumerable<Transaction> GetMempoolTransactions(int count);

        /// <summary>
        /// Checks if a transaction exists in the blockchain.
        /// </summary>
        /// <param name="hashHex">Transaction hash in hex format.</param>
        /// <returns>True if the transaction exists, false otherwise.</returns>
        bool TransactionExists(string hashHex);

        /// <summary>
        /// Gets the block index containing the transaction.
        /// </summary>
        /// <param name="hashHex">Transaction hash in hex format.</param>
        /// <returns>Block index if found, null otherwise.</returns>
        uint? GetTransactionBlockIndex(string hashHex);
    }
}
