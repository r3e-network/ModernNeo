// Copyright (C) 2015-2025 The Neo Project.
//
// TransactionRouterMessages.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Network.P2P.Payloads;

namespace Neo.Ledger
{
    /// <summary>
    /// Messages consumed by the transaction pre-verification router actor.
    /// </summary>
    public record PreverifyTransaction(Transaction Transaction, bool Relay);

    public record PreverifyCompleted(Transaction Transaction, bool Relay, VerifyResult Result);
}

