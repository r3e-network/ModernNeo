// Copyright (C) 2015-2025 The Neo Project.
//
// IPluginServiceRegistrar.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Neo.Plugins
{
    /// <summary>
    /// Defines a hook interface for plugins to register their own services into the DI container.
    /// Plugins implementing this interface can customize their service registration and lifecycle.
    /// </summary>
    public interface IPluginServiceRegistrar
    {
        /// <summary>
        /// Configures services for the plugin. Called before the plugin is instantiated.
        /// </summary>
        /// <param name="services">The service collection to register services into.</param>
        /// <param name="pluginMetadata">Metadata about the plugin being registered.</param>
        void ConfigureServices(IServiceCollection services, PluginMetadata pluginMetadata);

        /// <summary>
        /// Called after the plugin's service provider has been built but before the plugin is initialized.
        /// Allows the plugin to perform additional setup with access to the service provider.
        /// </summary>
        /// <param name="serviceProvider">The plugin's service provider.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task OnServicesConfiguredAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Provides extension methods for service registration with lifecycle management.
    /// </summary>
    public static class PluginServiceRegistrarExtensions
    {
        /// <summary>
        /// Registers a service with a decorator pattern, allowing the plugin to wrap an existing service.
        /// </summary>
        /// <typeparam name="TService">The service type to decorate.</typeparam>
        /// <typeparam name="TDecorator">The decorator type.</typeparam>
        /// <param name="services">The service collection.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection Decorate<TService, TDecorator>(this IServiceCollection services)
            where TService : class
            where TDecorator : class, TService
        {
            // Find the existing service descriptor
            var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(TService));
            if (descriptor == null)
            {
                throw new InvalidOperationException($"Service {typeof(TService).Name} is not registered.");
            }

            // Remove the original descriptor
            services.Remove(descriptor);

            // Register the decorator with the same lifetime
            if (descriptor.ImplementationInstance != null)
            {
                // Instance-based registration
                var instance = descriptor.ImplementationInstance;
                services.Add(ServiceDescriptor.Describe(
                    typeof(TService),
                    sp => ActivatorUtilities.CreateInstance<TDecorator>(sp, instance),
                    descriptor.Lifetime));
            }
            else if (descriptor.ImplementationFactory != null)
            {
                // Factory-based registration
                var factory = descriptor.ImplementationFactory;
                services.Add(ServiceDescriptor.Describe(
                    typeof(TService),
                    sp =>
                    {
                        var inner = factory(sp);
                        return ActivatorUtilities.CreateInstance<TDecorator>(sp, inner);
                    },
                    descriptor.Lifetime));
            }
            else if (descriptor.ImplementationType != null)
            {
                // Type-based registration
                var implementationType = descriptor.ImplementationType;
                services.Add(ServiceDescriptor.Describe(
                    typeof(TService),
                    sp =>
                    {
                        var inner = ActivatorUtilities.CreateInstance(sp, implementationType);
                        return ActivatorUtilities.CreateInstance<TDecorator>(sp, inner);
                    },
                    descriptor.Lifetime));
            }

            return services;
        }

        /// <summary>
        /// Registers a service that will be disposed when the plugin is unloaded.
        /// </summary>
        /// <typeparam name="TService">The service type.</typeparam>
        /// <typeparam name="TImplementation">The implementation type.</typeparam>
        /// <param name="services">The service collection.</param>
        /// <param name="lifetime">The service lifetime (default: Scoped).</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddPluginService<TService, TImplementation>(
            this IServiceCollection services,
            ServiceLifetime lifetime = ServiceLifetime.Scoped)
            where TService : class
            where TImplementation : class, TService
        {
            services.Add(ServiceDescriptor.Describe(typeof(TService), typeof(TImplementation), lifetime));
            return services;
        }

        /// <summary>
        /// Registers a singleton service that will be shared across the plugin's lifetime.
        /// </summary>
        /// <typeparam name="TService">The service type.</typeparam>
        /// <typeparam name="TImplementation">The implementation type.</typeparam>
        /// <param name="services">The service collection.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddPluginSingleton<TService, TImplementation>(this IServiceCollection services)
            where TService : class
            where TImplementation : class, TService
        {
            return services.AddPluginService<TService, TImplementation>(ServiceLifetime.Singleton);
        }

        /// <summary>
        /// Registers a scoped service that will be created once per scope.
        /// </summary>
        /// <typeparam name="TService">The service type.</typeparam>
        /// <typeparam name="TImplementation">The implementation type.</typeparam>
        /// <param name="services">The service collection.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddPluginScoped<TService, TImplementation>(this IServiceCollection services)
            where TService : class
            where TImplementation : class, TService
        {
            return services.AddPluginService<TService, TImplementation>(ServiceLifetime.Scoped);
        }

        /// <summary>
        /// Registers a transient service that will be created each time it's requested.
        /// </summary>
        /// <typeparam name="TService">The service type.</typeparam>
        /// <typeparam name="TImplementation">The implementation type.</typeparam>
        /// <param name="services">The service collection.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddPluginTransient<TService, TImplementation>(this IServiceCollection services)
            where TService : class
            where TImplementation : class, TService
        {
            return services.AddPluginService<TService, TImplementation>(ServiceLifetime.Transient);
        }

        /// <summary>
        /// Tries to replace an existing service registration with a new implementation.
        /// </summary>
        /// <typeparam name="TService">The service type.</typeparam>
        /// <typeparam name="TImplementation">The new implementation type.</typeparam>
        /// <param name="services">The service collection.</param>
        /// <param name="lifetime">The service lifetime.</param>
        /// <returns>True if the service was replaced; false if it didn't exist.</returns>
        public static bool TryReplace<TService, TImplementation>(
            this IServiceCollection services,
            ServiceLifetime lifetime = ServiceLifetime.Scoped)
            where TService : class
            where TImplementation : class, TService
        {
            var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(TService));
            if (descriptor == null)
                return false;

            services.Remove(descriptor);
            services.Add(ServiceDescriptor.Describe(typeof(TService), typeof(TImplementation), lifetime));
            return true;
        }
    }

    /// <summary>
    /// Provides lifecycle callbacks for plugin services.
    /// </summary>
    public interface IPluginServiceLifecycle
    {
        /// <summary>
        /// Called when the plugin service is being initialized.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task OnInitializingAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Called when the plugin service is being disposed.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task OnDisposingAsync(CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Base class for plugin services with lifecycle management.
    /// </summary>
    public abstract class PluginServiceBase : IPluginServiceLifecycle, IAsyncDisposable
    {
        private int _disposed;

        /// <summary>
        /// Gets a value indicating whether this service has been disposed.
        /// </summary>
        protected bool IsDisposed => _disposed != 0;

        /// <inheritdoc/>
        public virtual Task OnInitializingAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public virtual Task OnDisposingAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// Disposes the service asynchronously.
        /// </summary>
        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                await OnDisposingAsync(default).ConfigureAwait(false);
                await DisposeAsyncCore().ConfigureAwait(false);
                GC.SuppressFinalize(this);
            }
        }

        /// <summary>
        /// Override this method to provide custom disposal logic.
        /// </summary>
        protected virtual ValueTask DisposeAsyncCore()
        {
            return ValueTask.CompletedTask;
        }

        /// <summary>
        /// Throws an exception if the service has been disposed.
        /// </summary>
        protected void ThrowIfDisposed()
        {
            if (_disposed != 0)
            {
                throw new ObjectDisposedException(GetType().Name);
            }
        }
    }
}
