// Copyright (C) 2015-2025 The Neo Project.
//
// IInteroperableBase.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

namespace Neo.Core.Interfaces
{
    /// <summary>
    /// Base marker interface for objects that can be converted to/from VM stack items,
    /// without direct dependency on Neo.VM types.
    /// </summary>
    /// <remarks>
    /// The full IInteroperable interface (in Neo.SmartContract) extends this
    /// interface and adds methods that require StackItem and IReferenceCounter.
    /// This base interface allows protocol types to be referenced without
    /// pulling in VM dependencies.
    /// </remarks>
    public interface IInteroperableBase
    {
        // Marker interface - no methods required.
        // The full IInteroperable interface adds:
        // - void FromStackItem(StackItem stackItem)
        // - StackItem ToStackItem(IReferenceCounter? referenceCounter)
        // - IInteroperable Clone()
        // - void FromReplica(IInteroperable replica)
    }
}
