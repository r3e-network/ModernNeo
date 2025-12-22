// Copyright (C) 2015-2025 The Neo Project.
//
// NeoClientBuilderExtensions.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

namespace Neo.Orleans.Hosting
{
    /// <summary>
    /// Extension methods for configuring Neo Orleans client.
    /// </summary>
    public static class NeoClientBuilderExtensions
    {
        /// <summary>
        /// Configures the client with Neo-specific settings.
        /// </summary>
        /// <param name="clientBuilder">The client builder.</param>
        /// <param name="options">Optional Neo Orleans configuration options.</param>
        /// <returns>The client builder for chaining.</returns>
        public static IClientBuilder UseNeo(
            this IClientBuilder clientBuilder,
            Action<NeoOrleansOptions>? options = null)
        {
            var config = new NeoOrleansOptions();
            options?.Invoke(config);

            // Neo type serialization surrogates (UInt256, UInt160) are auto-discovered
            // via [RegisterConverter] attributes in Neo.Orleans.Serialization namespace.
            // No additional configuration needed - Orleans auto-discovers them.

            return clientBuilder;
        }
    }
}
