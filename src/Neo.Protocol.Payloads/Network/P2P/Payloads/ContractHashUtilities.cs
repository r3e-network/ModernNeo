// Copyright (C) 2015-2025 The Neo Project.
//
// ContractHashUtilities.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Extensions;
using Neo.SmartContract;
using Neo.VM;

namespace Neo.Network.P2P.Payloads
{
    internal static class ContractHashUtilities
    {
        internal static UInt160 GetContractHash(UInt160 sender, uint nefChecksum, string name)
        {
            using var sb = new ScriptBuilder();
            sb.Emit(OpCode.ABORT);
            sb.EmitPush(sender.ToArray());
            sb.EmitPush(nefChecksum);
            sb.EmitPush(name);
            return sb.ToArray().ToScriptHash();
        }
    }
}
