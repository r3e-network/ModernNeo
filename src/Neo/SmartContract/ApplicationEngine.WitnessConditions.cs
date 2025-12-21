// Copyright (C) 2015-2025 The Neo Project.
//
// ApplicationEngine.WitnessConditions.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Cryptography.ECC;
using Neo.Network.P2P.Payloads.Conditions;
using Neo.SmartContract.Native;
using System.Linq;

namespace Neo.SmartContract
{
    public partial class ApplicationEngine : IWitnessConditionContext
    {
        UInt160? IWitnessConditionContext.CurrentScriptHash => CurrentScriptHash;

        UInt160? IWitnessConditionContext.CallingScriptHash => CallingScriptHash;

        bool IWitnessConditionContext.IsCalledByEntry
        {
            get
            {
                var state = CurrentContext!.GetState<ExecutionContextState>();
                if (state.CallingContext is null) return true;
                state = state.CallingContext.GetState<ExecutionContextState>();
                return state.CallingContext is null;
            }
        }

        void IWitnessConditionContext.ValidateCallFlags(CallFlags requiredCallFlags)
        {
            ValidateCallFlags(requiredCallFlags);
        }

        bool IWitnessConditionContext.ContractHasGroup(UInt160 contractHash, ECPoint group)
        {
            ContractState? contract = NativeContract.ContractManagement.GetContract(SnapshotCache, contractHash);
            return contract is not null && contract.Manifest.Groups.Any(p => p.PubKey.Equals(group));
        }
    }
}

