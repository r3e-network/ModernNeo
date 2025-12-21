// Copyright (C) 2015-2025 The Neo Project.
//
// IPluginFactory.cs file belongs to the neo project and is free
// software distributed under the MIT software license.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;

namespace Neo.Plugins
{
    /// <summary>
    /// Defines a factory interface for creating plugin instances with custom instantiation logic.
    /// Allows plugins to control their own creation process and integrate with DI containers.
    /// </summary>
    public interface IPluginFactory
    {
        /// <summary>
        /// Creates a plugin instance of the specified type.
        /// </summary>
        /// <param name="pluginType">The type of plugin to create.</param>
        /// <param name="serviceProvider">The service provider for dependency resolution.</param>
        /// <returns>The created plugin instance, or null if creation failed.</returns>
        IPlugin? CreatePlugin(Type pluginType, IServiceProvider serviceProvider);

        /// <summary>
        /// Determines whether this factory can create the specified plugin type.
        /// </summary>
        /// <param name="pluginType">The type of plugin to check.</param>
        /// <returns>True if this factory can create the plugin; otherwise, false.</returns>
        bool CanCreatePlugin(Type pluginType);
    }

    /// <summary>
    /// Default plugin factory that uses constructor injection with DI container.
    /// </summary>
    public sealed class DefaultPluginFactory : IPluginFactory
    {
        /// <inheritdoc/>
        [RequiresUnreferencedCode("Plugin creation uses reflection to discover and invoke constructors")]
        [RequiresDynamicCode("Plugin creation may require dynamic code generation for constructor invocation")]
        public IPlugin? CreatePlugin(
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type pluginType,
            IServiceProvider serviceProvider)
        {
            if (pluginType == null)
                throw new ArgumentNullException(nameof(pluginType));
            if (serviceProvider == null)
                throw new ArgumentNullException(nameof(serviceProvider));

            if (!typeof(IPlugin).IsAssignableFrom(pluginType))
                throw new ArgumentException($"Type {pluginType.FullName} does not implement IPlugin.", nameof(pluginType));

            try
            {
                // Try to find a constructor with parameters
                var constructor = pluginType.GetConstructors()
                    .OrderByDescending(c => c.GetParameters().Length)
                    .FirstOrDefault();

                if (constructor == null)
                {
                    // No public constructor, try Activator
                    return Activator.CreateInstance(pluginType) as IPlugin;
                }

                var parameters = constructor.GetParameters();
                var args = new object?[parameters.Length];

                for (var i = 0; i < parameters.Length; i++)
                {
                    var paramType = parameters[i].ParameterType;
                    args[i] = serviceProvider.GetService(paramType);

                    // If parameter is required and service not found, fail
                    if (args[i] == null && !IsOptionalParameter(parameters[i]))
                    {
                        throw new InvalidOperationException(
                            $"Cannot resolve required parameter '{parameters[i].Name}' of type '{paramType.FullName}' for plugin '{pluginType.FullName}'.");
                    }
                }

                return constructor.Invoke(args) as IPlugin;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to create plugin instance of type '{pluginType.FullName}'.", ex);
            }
        }

        /// <inheritdoc/>
        public bool CanCreatePlugin(Type pluginType)
        {
            if (pluginType == null)
                return false;

            return typeof(IPlugin).IsAssignableFrom(pluginType) &&
                   !pluginType.IsAbstract &&
                   !pluginType.IsInterface;
        }

        private static bool IsOptionalParameter(ParameterInfo parameter)
        {
            return parameter.HasDefaultValue ||
                   parameter.IsOptional ||
                   IsNullableType(parameter.ParameterType);
        }

        private static bool IsNullableType(Type type)
        {
            if (!type.IsValueType)
                return true; // Reference types are nullable

            return Nullable.GetUnderlyingType(type) != null;
        }
    }

    /// <summary>
    /// Plugin factory that supports custom factory methods on the plugin type.
    /// Looks for a static Create method with signature: static IPlugin Create(IServiceProvider).
    /// </summary>
    public sealed class StaticFactoryMethodPluginFactory : IPluginFactory
    {
        private const string FactoryMethodName = "Create";

        /// <inheritdoc/>
        [RequiresUnreferencedCode("Plugin creation uses reflection to discover and invoke static factory methods")]
        [RequiresDynamicCode("Plugin creation may require dynamic code generation for method invocation")]
        public IPlugin? CreatePlugin(
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)] Type pluginType,
            IServiceProvider serviceProvider)
        {
            if (pluginType == null)
                throw new ArgumentNullException(nameof(pluginType));
            if (serviceProvider == null)
                throw new ArgumentNullException(nameof(serviceProvider));

            var factoryMethod = pluginType.GetMethod(
                FactoryMethodName,
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(IServiceProvider) },
                null);

            if (factoryMethod == null || !typeof(IPlugin).IsAssignableFrom(factoryMethod.ReturnType))
            {
                throw new InvalidOperationException(
                    $"Plugin type '{pluginType.FullName}' does not have a static factory method with signature: static IPlugin {FactoryMethodName}(IServiceProvider).");
            }

            try
            {
                return factoryMethod.Invoke(null, new object[] { serviceProvider }) as IPlugin;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Failed to create plugin instance using factory method '{FactoryMethodName}' on type '{pluginType.FullName}'.", ex);
            }
        }

        /// <inheritdoc/>
        [RequiresUnreferencedCode("Plugin type checking uses reflection to discover static factory methods")]
        public bool CanCreatePlugin(
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)] Type pluginType)
        {
            if (pluginType == null)
                return false;

            if (!typeof(IPlugin).IsAssignableFrom(pluginType) || pluginType.IsAbstract || pluginType.IsInterface)
                return false;

            var factoryMethod = pluginType.GetMethod(
                FactoryMethodName,
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(IServiceProvider) },
                null);

            return factoryMethod != null && typeof(IPlugin).IsAssignableFrom(factoryMethod.ReturnType);
        }
    }

    /// <summary>
    /// Composite plugin factory that tries multiple factories in order.
    /// </summary>
    public sealed class CompositePluginFactory : IPluginFactory
    {
        private readonly IPluginFactory[] _factories;

        /// <summary>
        /// Creates a new composite plugin factory.
        /// </summary>
        /// <param name="factories">The factories to try in order.</param>
        public CompositePluginFactory(params IPluginFactory[] factories)
        {
            _factories = factories ?? throw new ArgumentNullException(nameof(factories));
            if (_factories.Length == 0)
                throw new ArgumentException("At least one factory must be provided.", nameof(factories));
        }

        /// <inheritdoc/>
        public IPlugin? CreatePlugin(Type pluginType, IServiceProvider serviceProvider)
        {
            foreach (var factory in _factories)
            {
                if (factory.CanCreatePlugin(pluginType))
                {
                    try
                    {
                        return factory.CreatePlugin(pluginType, serviceProvider);
                    }
                    catch
                    {
                        // Try next factory
                        continue;
                    }
                }
            }

            throw new InvalidOperationException($"No factory could create plugin of type '{pluginType.FullName}'.");
        }

        /// <inheritdoc/>
        public bool CanCreatePlugin(Type pluginType)
        {
            return _factories.Any(f => f.CanCreatePlugin(pluginType));
        }
    }

    /// <summary>
    /// Plugin factory that uses a delegate for custom creation logic.
    /// </summary>
    public sealed class DelegatePluginFactory : IPluginFactory
    {
        private readonly Func<Type, IServiceProvider, IPlugin?> _factoryDelegate;
        private readonly Func<Type, bool>? _canCreateDelegate;

        /// <summary>
        /// Creates a new delegate plugin factory.
        /// </summary>
        /// <param name="factoryDelegate">The delegate to create plugin instances.</param>
        /// <param name="canCreateDelegate">Optional delegate to check if a type can be created.</param>
        public DelegatePluginFactory(
            Func<Type, IServiceProvider, IPlugin?> factoryDelegate,
            Func<Type, bool>? canCreateDelegate = null)
        {
            _factoryDelegate = factoryDelegate ?? throw new ArgumentNullException(nameof(factoryDelegate));
            _canCreateDelegate = canCreateDelegate;
        }

        /// <inheritdoc/>
        public IPlugin? CreatePlugin(Type pluginType, IServiceProvider serviceProvider)
        {
            return _factoryDelegate(pluginType, serviceProvider);
        }

        /// <inheritdoc/>
        public bool CanCreatePlugin(Type pluginType)
        {
            if (_canCreateDelegate != null)
                return _canCreateDelegate(pluginType);

            // Default: check if type implements IPlugin
            return typeof(IPlugin).IsAssignableFrom(pluginType) &&
                   !pluginType.IsAbstract &&
                   !pluginType.IsInterface;
        }
    }

    /// <summary>
    /// Extension methods for plugin factories.
    /// </summary>
    public static class PluginFactoryExtensions
    {
        /// <summary>
        /// Creates a plugin instance using the factory, with type parameter.
        /// </summary>
        /// <typeparam name="T">The plugin type.</typeparam>
        /// <param name="factory">The plugin factory.</param>
        /// <param name="serviceProvider">The service provider.</param>
        /// <returns>The created plugin instance.</returns>
        public static T? CreatePlugin<T>(this IPluginFactory factory, IServiceProvider serviceProvider)
            where T : class, IPlugin
        {
            if (factory == null)
                throw new ArgumentNullException(nameof(factory));

            return factory.CreatePlugin(typeof(T), serviceProvider) as T;
        }

        /// <summary>
        /// Tries to create a plugin instance, returning false if creation fails.
        /// </summary>
        /// <param name="factory">The plugin factory.</param>
        /// <param name="pluginType">The plugin type.</param>
        /// <param name="serviceProvider">The service provider.</param>
        /// <param name="plugin">The created plugin instance, or null if creation failed.</param>
        /// <returns>True if the plugin was created successfully; otherwise, false.</returns>
        public static bool TryCreatePlugin(
            this IPluginFactory factory,
            Type pluginType,
            IServiceProvider serviceProvider,
            out IPlugin? plugin)
        {
            if (factory == null)
                throw new ArgumentNullException(nameof(factory));

            try
            {
                plugin = factory.CreatePlugin(pluginType, serviceProvider);
                return plugin != null;
            }
            catch
            {
                plugin = null;
                return false;
            }
        }
    }
}
