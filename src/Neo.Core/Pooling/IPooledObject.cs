// Copyright (C) 2015-2025 The Neo Project.
//
// IPooledObject.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

namespace Neo.Core.Pooling
{
    /// <summary>
    /// Defines a contract for objects that can be pooled and reused.
    /// Pooled objects must implement this interface to ensure proper cleanup
    /// and reset before being returned to the pool.
    /// </summary>
    public interface IPooledObject
    {
        /// <summary>
        /// Resets the object to its initial state, preparing it for reuse.
        /// This method is called when the object is returned to the pool.
        /// Implementations should clear all state and release any resources
        /// that should not be retained between uses.
        /// </summary>
        void Reset();
    }
}
