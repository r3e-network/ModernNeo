// Copyright (C) 2015-2025 The Neo Project.
//
// IRpcMethod.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Json;

namespace Neo.RPC
{
    /// <summary>
    /// Defines a handler for an RPC method.
    /// </summary>
    public interface IRpcMethod
    {
        /// <summary>
        /// Gets the name of the RPC method.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Processes the RPC request and returns a result.
        /// </summary>
        /// <param name="parameters">The method parameters.</param>
        /// <returns>The result of the method invocation.</returns>
        Task<JToken?> ProcessAsync(JArray? parameters);
    }

    /// <summary>
    /// Attribute to mark a method as an RPC handler.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class RpcMethodAttribute : Attribute
    {
        /// <summary>
        /// Gets the name of the RPC method.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="RpcMethodAttribute"/> class.
        /// </summary>
        /// <param name="name">The name of the RPC method.</param>
        public RpcMethodAttribute(string name)
        {
            Name = name;
        }
    }
}
