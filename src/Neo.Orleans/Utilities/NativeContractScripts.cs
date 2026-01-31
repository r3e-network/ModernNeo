// Copyright (C) 2015-2025 The Neo Project.
//
// NativeContractScripts.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.SmartContract;
using Neo.VM;

namespace Neo.Orleans.Utilities
{
    /// <summary>
    /// Shared native contract scripts used by multiple grains.
    /// Consolidates duplicate script creation from BlockchainGrain and ConsensusGrain.
    /// </summary>
    public static class NativeContractScripts
    {
        /// <summary>
        /// Script for calling System.Contract.NativeOnPersist.
        /// </summary>
        public static readonly Script OnPersist;

        /// <summary>
        /// Script for calling System.Contract.NativePostPersist.
        /// </summary>
        public static readonly Script PostPersist;

        static NativeContractScripts()
        {
            using var onPersistBuilder = new ScriptBuilder();
            onPersistBuilder.EmitSysCall(ApplicationEngine.System_Contract_NativeOnPersist);
            OnPersist = new Script(onPersistBuilder.ToArray(), true);

            using var postPersistBuilder = new ScriptBuilder();
            postPersistBuilder.EmitSysCall(ApplicationEngine.System_Contract_NativePostPersist);
            PostPersist = new Script(postPersistBuilder.ToArray(), true);
        }
    }
}
