// Copyright (C) 2015-2025 The Neo Project.
//
// WitnessConditionType.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.IO.Caching;

namespace Neo.Network.P2P.Payloads.Conditions
{
    /// <summary>
    /// Represents the type of witness condition.
    /// </summary>
    public enum WitnessConditionType : byte
    {
        /// <summary>
        /// Indicates that the condition will always be met or not met.
        /// </summary>
        [ReflectionCache("Neo.Network.P2P.Payloads.Conditions.BooleanCondition, Neo")]
        Boolean = 0x00,

        /// <summary>
        /// Reverse another condition.
        /// </summary>
        [ReflectionCache("Neo.Network.P2P.Payloads.Conditions.NotCondition, Neo")]
        Not = 0x01,

        /// <summary>
        /// Indicates that all conditions must be met.
        /// </summary>
        [ReflectionCache("Neo.Network.P2P.Payloads.Conditions.AndCondition, Neo")]
        And = 0x02,

        /// <summary>
        /// Indicates that any of the conditions meets.
        /// </summary>
        [ReflectionCache("Neo.Network.P2P.Payloads.Conditions.OrCondition, Neo")]
        Or = 0x03,

        /// <summary>
        /// Indicates that the condition is met when the current context has the specified script hash.
        /// </summary>
        [ReflectionCache("Neo.Network.P2P.Payloads.Conditions.ScriptHashCondition, Neo")]
        ScriptHash = 0x18,

        /// <summary>
        /// Indicates that the condition is met when the current context has the specified group.
        /// </summary>
        [ReflectionCache("Neo.Network.P2P.Payloads.Conditions.GroupCondition, Neo")]
        Group = 0x19,

        /// <summary>
        /// Indicates that the condition is met when the current context is the entry point or is called by the entry point.
        /// </summary>
        [ReflectionCache("Neo.Network.P2P.Payloads.Conditions.CalledByEntryCondition, Neo")]
        CalledByEntry = 0x20,

        /// <summary>
        /// Indicates that the condition is met when the current context is called by the specified contract.
        /// </summary>
        [ReflectionCache("Neo.Network.P2P.Payloads.Conditions.CalledByContractCondition, Neo")]
        CalledByContract = 0x28,

        /// <summary>
        /// Indicates that the condition is met when the current context is called by the specified group.
        /// </summary>
        [ReflectionCache("Neo.Network.P2P.Payloads.Conditions.CalledByGroupCondition, Neo")]
        CalledByGroup = 0x29
    }
}

