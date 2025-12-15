// Copyright (C) 2015-2025 The Neo Project.
//
// IWitnessRuleData.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.IO;

namespace Neo.Core.Interfaces
{
    /// <summary>
    /// Defines the data contract for a witness rule without VM dependencies.
    /// This interface allows lower layers to work with witness rule data without depending
    /// on the full WitnessRule implementation that requires Neo.VM.
    /// </summary>
    public interface IWitnessRuleData : ISerializable
    {
        /// <summary>
        /// The action to be taken if the current context meets with the rule.
        /// Uses byte to avoid dependency on WitnessRuleAction enum location.
        /// </summary>
        byte ActionValue { get; }
    }
}
