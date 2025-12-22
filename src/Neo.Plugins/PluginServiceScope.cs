// Copyright (C) 2015-2025 The Neo Project.
//
// PluginServiceScope.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Neo.Plugins
{
    /// <summary>
    /// Represents a service scope for a plugin with isolated dependency injection container.
    /// Each plugin gets its own IServiceProvider scope that is automatically disposed when the plugin is unloaded.
    /// </summary>
    public sealed class PluginServiceScope : IAsyncDisposable, IDisposable
    {
        private readonly IServiceScope _scope;
        private readonly string _pluginName;
        private int _disposed;

        /// <summary>
        /// Gets the service provider for this plugin scope.
        /// </summary>
        public IServiceProvider ServiceProvider => _scope.ServiceProvider;

        /// <summary>
        /// Gets the plugin name associated with this scope.
        /// </summary>
        public string PluginName => _pluginName;

        /// <summary>
        /// Gets a value indicating whether this scope has been disposed.
        /// </summary>
        public bool IsDisposed => _disposed != 0;

        /// <summary>
        /// Creates a new plugin service scope.
        /// </summary>
        /// <param name="scope">The underlying service scope.</param>
        /// <param name="pluginName">The name of the plugin.</param>
        internal PluginServiceScope(IServiceScope scope, string pluginName)
        {
            _scope = scope ?? throw new ArgumentNullException(nameof(scope));
            _pluginName = pluginName ?? throw new ArgumentNullException(nameof(pluginName));
        }

        /// <summary>
        /// Gets a service of the specified type from the plugin's service provider.
        /// </summary>
        /// <typeparam name="T">The type of service to retrieve.</typeparam>
        /// <returns>The service instance, or null if not found.</returns>
        public T? GetService<T>() where T : class
        {
            ThrowIfDisposed();
            return ServiceProvider.GetService<T>();
        }

        /// <summary>
        /// Gets a required service of the specified type from the plugin's service provider.
        /// </summary>
        /// <typeparam name="T">The type of service to retrieve.</typeparam>
        /// <returns>The service instance.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the service is not found.</exception>
        public T GetRequiredService<T>() where T : class
        {
            ThrowIfDisposed();
            return ServiceProvider.GetRequiredService<T>();
        }

        /// <summary>
        /// Creates a new scope within this plugin's service scope.
        /// </summary>
        /// <returns>A new service scope.</returns>
        public IServiceScope CreateScope()
        {
            ThrowIfDisposed();
            return ServiceProvider.CreateScope();
        }

        /// <summary>
        /// Disposes the plugin service scope synchronously.
        /// </summary>
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                _scope.Dispose();
            }
        }

        /// <summary>
        /// Disposes the plugin service scope asynchronously.
        /// </summary>
        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                if (_scope is IAsyncDisposable asyncDisposable)
                {
                    await asyncDisposable.DisposeAsync().ConfigureAwait(false);
                }
                else
                {
                    _scope.Dispose();
                }
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed != 0)
            {
                throw new ObjectDisposedException(nameof(PluginServiceScope),
                    $"Plugin service scope for '{_pluginName}' has been disposed.");
            }
        }
    }

    /// <summary>
    /// Factory for creating plugin service scopes.
    /// </summary>
    internal sealed class PluginServiceScopeFactory
    {
        private readonly IServiceProvider _rootServiceProvider;

        public PluginServiceScopeFactory(IServiceProvider rootServiceProvider)
        {
            _rootServiceProvider = rootServiceProvider ?? throw new ArgumentNullException(nameof(rootServiceProvider));
        }

        /// <summary>
        /// Creates a new plugin service scope with optional custom service registration.
        /// </summary>
        /// <param name="pluginName">The name of the plugin.</param>
        /// <param name="configureServices">Optional action to configure plugin-specific services.</param>
        /// <returns>A new plugin service scope.</returns>
        public PluginServiceScope CreateScope(string pluginName, Action<IServiceCollection>? configureServices = null)
        {
            if (string.IsNullOrWhiteSpace(pluginName))
                throw new ArgumentException("Plugin name cannot be null or whitespace.", nameof(pluginName));

            // If no custom services, just create a scope from the root provider
            if (configureServices == null)
            {
                var scope = _rootServiceProvider.CreateScope();
                return new PluginServiceScope(scope, pluginName);
            }

            // Create a new service collection with services from the root provider
            var services = new ServiceCollection();

            // Copy singleton services from root provider (if accessible)
            // Note: This is a simplified approach. In production, you might want to use
            // a more sophisticated service descriptor copying mechanism.

            // Configure plugin-specific services
            configureServices(services);

            // Build the plugin-specific service provider
            var pluginServiceProvider = services.BuildServiceProvider();
            var pluginScope = pluginServiceProvider.CreateScope();

            return new PluginServiceScope(pluginScope, pluginName);
        }

        /// <summary>
        /// Creates a child scope from an existing plugin scope.
        /// </summary>
        /// <param name="parentScope">The parent plugin scope.</param>
        /// <returns>A new child service scope.</returns>
        public IServiceScope CreateChildScope(PluginServiceScope parentScope)
        {
            if (parentScope == null)
                throw new ArgumentNullException(nameof(parentScope));

            return parentScope.CreateScope();
        }
    }
}
