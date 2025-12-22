// Copyright (C) 2015-2025 The Neo Project.
//
// IRpcServer.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

namespace Neo.RPC
{
    /// <summary>
    /// Defines the interface for an RPC server.
    /// </summary>
    public interface IRpcServer : IDisposable
    {
        /// <summary>
        /// Gets whether the server is running.
        /// </summary>
        bool IsRunning { get; }

        /// <summary>
        /// Gets the server endpoint (e.g., "http://localhost:10332").
        /// </summary>
        string Endpoint { get; }

        /// <summary>
        /// Starts the RPC server.
        /// </summary>
        Task StartAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Stops the RPC server.
        /// </summary>
        Task StopAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Registers an RPC method handler.
        /// </summary>
        /// <param name="method">The method handler to register.</param>
        void RegisterMethod(IRpcMethod method);

        /// <summary>
        /// Unregisters an RPC method handler.
        /// </summary>
        /// <param name="methodName">The name of the method to unregister.</param>
        /// <returns>True if the method was unregistered; otherwise, false.</returns>
        bool UnregisterMethod(string methodName);
    }
}
