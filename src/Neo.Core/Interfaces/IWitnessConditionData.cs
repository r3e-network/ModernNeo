// Copyright (C) 2015-2025 The Neo Project.
//
// IWitnessConditionData.cs file belongs to the neo project and is free
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
    /// Defines the data contract for a witness condition without VM dependencies.
    /// This interface allows lower layers to work with witness condition data without depending
    /// on the full WitnessCondition implementation that requires Neo.VM and ApplicationEngine.
    /// </summary>
    public interface IWitnessConditionData : ISerializable
    {
        /// <summary>
        /// The type of the witness condition as a byte value.
        /// </summary>
        byte TypeValue { get; }
    }
}
