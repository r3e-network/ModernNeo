// Copyright (C) 2015-2025 The Neo Project.
//
// PluginManager.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Threading;
using System.Threading.Tasks;

namespace Neo.Plugins
{
    /// <summary>
    /// Defines a capability for plugins that support reloading configuration at runtime.
    /// </summary>
    public interface IPluginConfigurationReloadable
    {
        /// <summary>
        /// Reloads the plugin configuration from the specified configuration file.
        /// </summary>
        /// <param name="configurationPath">The configuration file path that changed.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task ReloadConfigurationAsync(string configurationPath, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Plugin manager with discovery, dependency validation, compatibility checks,
    /// enable/disable support, and optional configuration reload notifications.
    /// </summary>
    public sealed class PluginManager : IAsyncDisposable
    {
        private readonly ConcurrentDictionary<string, PluginContext> _plugins = new(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<string, PluginCatalogEntry> _catalog = new(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<string, string> _aliases = new(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<string, FileSystemWatcher> _configWatchers = new(StringComparer.OrdinalIgnoreCase);
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<PluginManager>? _logger;
        private readonly PluginManagerOptions _options;
        private readonly FileSystemWatcher? _pluginWatcher;
        private readonly SemaphoreSlim _lock = new(1, 1);
        private bool _disposed;

        /// <summary>
        /// Event raised when plugin metadata is discovered.
        /// </summary>
        public event Action<PluginMetadata>? OnPluginMetadataDiscovered;

        /// <summary>
        /// Event raised when a plugin is loaded.
        /// </summary>
        public event Action<IPlugin>? OnPluginLoaded;

        /// <summary>
        /// Event raised when a plugin is unloaded.
        /// </summary>
        public event Action<string>? OnPluginUnloaded;

        /// <summary>
        /// Event raised when a plugin is enabled.
        /// </summary>
        public event Action<string>? OnPluginEnabled;

        /// <summary>
        /// Event raised when a plugin is disabled.
        /// </summary>
        public event Action<string>? OnPluginDisabled;

        /// <summary>
        /// Event raised when a plugin fails to load.
        /// </summary>
        public event Action<string, Exception>? OnPluginLoadFailed;

        /// <summary>
        /// Gets all loaded plugins.
        /// </summary>
        public IReadOnlyCollection<IPlugin> Plugins => _plugins.Values.Select(p => p.Plugin).ToList().AsReadOnly();

        /// <summary>
        /// Gets the discovered plugin metadata.
        /// </summary>
        public IReadOnlyCollection<PluginMetadata> DiscoveredPluginMetadata =>
            _catalog.Values.Select(e => e.Metadata).ToList().AsReadOnly();

        /// <summary>
        /// Gets the number of loaded plugins.
        /// </summary>
        public int PluginCount => _plugins.Count;

        /// <summary>
        /// Creates a new <see cref="PluginManager"/>.
        /// </summary>
        /// <param name="serviceProvider">Service provider used for constructor injection.</param>
        /// <param name="options">Optional manager options.</param>
        /// <param name="logger">Optional logger.</param>
        public PluginManager(
            IServiceProvider serviceProvider,
            PluginManagerOptions? options = null,
            ILogger<PluginManager>? logger = null)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _options = options ?? new PluginManagerOptions();
            _logger = logger;

            if (_options.EnableHotReload && Directory.Exists(_options.PluginDirectory))
            {
                _pluginWatcher = new FileSystemWatcher(_options.PluginDirectory, "*.dll")
                {
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName,
                    EnableRaisingEvents = true
                };
                _pluginWatcher.Changed += OnPluginFileChanged;
                _pluginWatcher.Created += OnPluginFileChanged;
                _pluginWatcher.Deleted += OnPluginFileDeleted;
            }
        }

        /// <summary>
        /// Scans the plugin directory, discovers metadata, validates dependencies and compatibility,
        /// and loads enabled plugins in dependency order.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        public async Task LoadAllAsync(CancellationToken cancellationToken = default)
        {
            if (!Directory.Exists(_options.PluginDirectory))
            {
                _logger?.LogWarning("Plugin directory does not exist: {Directory}", _options.PluginDirectory);
                return;
            }

            await RefreshCatalogAsync(cancellationToken).ConfigureAwait(false);

            var enabled = _catalog.Values
                .Where(static e => e.Enabled)
                .Select(static e => e.Metadata)
                .Where(m => m.IsCompatibleWith(_options.NeoVersion))
                .ToList();

            foreach (var incompatible in _catalog.Values
                         .Where(static e => e.Enabled)
                         .Select(static e => e.Metadata)
                         .Where(m => !m.IsCompatibleWith(_options.NeoVersion)))
            {
                _logger?.LogWarning(
                    "Plugin {Name} v{Version} is not compatible with Neo v{NeoVersion} (Min={Min}, Max={Max})",
                    incompatible.Name, incompatible.Version, _options.NeoVersion, incompatible.MinNeoVersion, incompatible.MaxNeoVersion);
            }

            var graph = new PluginDependencyGraph(enabled);
            graph.TryGetLoadOrder(out var order, out var validation);

            foreach (var (pluginName, missing) in validation.MissingDependencies)
            {
                _logger?.LogError("Plugin {Name} has missing dependencies: {Dependencies}", pluginName, string.Join(", ", missing));
            }

            foreach (var cycle in validation.Cycles)
            {
                _logger?.LogError("Plugin dependency cycle detected: {Cycle}", string.Join(" -> ", cycle));
            }

            foreach (var metadata in order)
            {
                if (cancellationToken.IsCancellationRequested) break;

                try
                {
                    if (!_catalog.TryGetValue(metadata.Name, out var entry))
                        continue;

                    await LoadPluginAsync(entry.AssemblyPath, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Failed to load plugin: {Name}", metadata.Name);
                    OnPluginLoadFailed?.Invoke(metadata.Name, ex);
                }
            }
        }

        /// <summary>
        /// Scans the plugin directory and updates the internal catalog with discovered plugin metadata.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        public async Task RefreshCatalogAsync(CancellationToken cancellationToken = default)
        {
            if (!Directory.Exists(_options.PluginDirectory))
                return;

            var pluginFiles = Directory.GetFiles(_options.PluginDirectory, "*.dll", SearchOption.AllDirectories);
            foreach (var file in pluginFiles)
            {
                if (cancellationToken.IsCancellationRequested) break;
                await DiscoverFromAssemblyPathAsync(file, cancellationToken).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Loads a plugin from the specified assembly path.
        /// </summary>
        /// <param name="assemblyPath">The plugin assembly path.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The loaded plugin instance, or null if the plugin is disabled or not discoverable.</returns>
        [RequiresUnreferencedCode("Plugin loading uses reflection to discover types, read metadata, and instantiate plugins")]
        [RequiresDynamicCode("Plugin loading may require dynamic code generation for type instantiation")]
        public async Task<IPlugin?> LoadPluginAsync(string assemblyPath, CancellationToken cancellationToken = default)
        {
            await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                return await LoadPluginInternalAsync(
                    assemblyPath,
                    new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                    cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _lock.Release();
            }
        }

        /// <summary>
        /// Unloads a loaded plugin by name.
        /// </summary>
        /// <param name="pluginName">The plugin name (or alias).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns><see langword="true"/> if unloaded; otherwise, <see langword="false"/>.</returns>
        public async Task<bool> UnloadPluginAsync(string pluginName, CancellationToken cancellationToken = default)
        {
            await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                return await UnloadPluginInternalAsync(pluginName, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _lock.Release();
            }
        }

        /// <summary>
        /// Gets a plugin instance by name.
        /// </summary>
        /// <param name="name">The plugin name (or alias).</param>
        /// <returns>The plugin instance, or null if not loaded.</returns>
        public IPlugin? GetPlugin(string name)
        {
            name = ResolvePluginName(name) ?? name;
            return _plugins.TryGetValue(name, out var ctx) ? ctx.Plugin : null;
        }

        /// <summary>
        /// Gets the first loaded plugin instance assignable to <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">The plugin type.</typeparam>
        /// <returns>The plugin instance, or null if not loaded.</returns>
        public T? GetPlugin<T>() where T : class, IPlugin
        {
            return _plugins.Values.Select(static c => c.Plugin).OfType<T>().FirstOrDefault();
        }

        /// <summary>
        /// Gets all loaded plugins that implement/derive from <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">The interface/base type.</typeparam>
        /// <returns>The matching plugin instances.</returns>
        public IEnumerable<T> GetPlugins<T>() where T : class
        {
            return _plugins.Values.Select(static c => c.Plugin).OfType<T>();
        }

        /// <summary>
        /// Gets metadata for a discovered plugin by name.
        /// </summary>
        /// <param name="name">The plugin name (or alias).</param>
        /// <returns>The metadata if discovered; otherwise, null.</returns>
        public PluginMetadata? GetPluginMetadata(string name)
        {
            name = ResolvePluginName(name) ?? name;
            return _catalog.TryGetValue(name, out var entry) ? entry.Metadata : null;
        }

        /// <summary>
        /// Gets a value indicating whether a plugin is loaded.
        /// </summary>
        /// <param name="name">The plugin name (or alias).</param>
        /// <returns><see langword="true"/> if loaded; otherwise, <see langword="false"/>.</returns>
        public bool IsLoaded(string name)
        {
            name = ResolvePluginName(name) ?? name;
            return _plugins.ContainsKey(name);
        }

        /// <summary>
        /// Gets a value indicating whether a discovered plugin is enabled.
        /// </summary>
        /// <param name="name">The plugin name (or alias).</param>
        /// <returns><see langword="true"/> if enabled; otherwise, <see langword="false"/>.</returns>
        public bool IsEnabled(string name)
        {
            name = ResolvePluginName(name) ?? name;
            return _catalog.TryGetValue(name, out var entry) && entry.Enabled;
        }

        /// <summary>
        /// Enables a plugin without uninstalling it. If the plugin is not loaded, it will be loaded.
        /// </summary>
        /// <param name="pluginName">The plugin name (or alias).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns><see langword="true"/> if enabled; otherwise, <see langword="false"/>.</returns>
        public async Task<bool> EnablePluginAsync(string pluginName, CancellationToken cancellationToken = default)
        {
            await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                pluginName = ResolvePluginName(pluginName) ?? pluginName;
                if (!_catalog.TryGetValue(pluginName, out var entry))
                    return false;

                if (entry.Enabled)
                    return true;

                entry.Enabled = true;
                _options.DisabledPlugins.Remove(pluginName);

                try
                {
                    if (_plugins.TryGetValue(pluginName, out var ctx))
                    {
                        if (_options.StartPluginsOnLoad)
                            await ctx.Plugin.StartAsync(cancellationToken).ConfigureAwait(false);
                    }
                    else
                    {
                        var loaded = await LoadPluginInternalAsync(entry.AssemblyPath, new HashSet<string>(StringComparer.OrdinalIgnoreCase), cancellationToken).ConfigureAwait(false);
                        if (loaded is null)
                            throw new InvalidOperationException($"Failed to enable plugin '{pluginName}'.");
                    }

                    OnPluginEnabled?.Invoke(pluginName);
                    return true;
                }
                catch (Exception ex)
                {
                    entry.Enabled = false;
                    _options.DisabledPlugins.Add(pluginName);
                    _logger?.LogError(ex, "Failed to enable plugin: {Name}", pluginName);
                    return false;
                }
            }
            finally
            {
                _lock.Release();
            }
        }

        /// <summary>
        /// Disables a plugin without uninstalling it. If the plugin is loaded, it will be stopped and optionally unloaded.
        /// </summary>
        /// <param name="pluginName">The plugin name (or alias).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns><see langword="true"/> if disabled; otherwise, <see langword="false"/>.</returns>
        public async Task<bool> DisablePluginAsync(string pluginName, CancellationToken cancellationToken = default)
        {
            await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                pluginName = ResolvePluginName(pluginName) ?? pluginName;
                if (!_catalog.TryGetValue(pluginName, out var entry))
                    return false;

                if (!entry.Enabled)
                    return true;

                entry.Enabled = false;
                _options.DisabledPlugins.Add(pluginName);
                OnPluginDisabled?.Invoke(pluginName);

                if (_plugins.TryGetValue(pluginName, out var ctx))
                {
                    try
                    {
                        if (ctx.Plugin.State == PluginState.Running)
                            await ctx.Plugin.StopAsync(cancellationToken).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, "Error stopping plugin during disable: {Name}", pluginName);
                    }

                    if (_options.UnloadPluginsOnDisable)
                        await UnloadPluginInternalAsync(pluginName, cancellationToken).ConfigureAwait(false);
                }

                return true;
            }
            finally
            {
                _lock.Release();
            }
        }

        /// <summary>
        /// Reloads configuration for a loaded plugin if it supports <see cref="IPluginConfigurationReloadable"/>.
        /// </summary>
        /// <param name="pluginName">The plugin name (or alias).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns><see langword="true"/> if reload was dispatched; otherwise, <see langword="false"/>.</returns>
        public async Task<bool> ReloadPluginConfigurationAsync(string pluginName, CancellationToken cancellationToken = default)
        {
            pluginName = ResolvePluginName(pluginName) ?? pluginName;
            if (!_plugins.TryGetValue(pluginName, out var ctx))
                return false;

            if (ctx.Plugin is not IPluginConfigurationReloadable reloadable)
                return false;

            var configPath = GetDefaultConfigurationPath(ctx.AssemblyPath);
            if (string.IsNullOrEmpty(configPath))
                return false;

            try
            {
                await reloadable.ReloadConfigurationAsync(configPath, cancellationToken).ConfigureAwait(false);
                return true;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to reload plugin configuration: {Name}", pluginName);
                return false;
            }
        }

        [RequiresUnreferencedCode("Plugin discovery uses reflection to load assemblies and discover plugin types")]
        [RequiresDynamicCode("Plugin discovery may require dynamic code generation")]
        private async Task<PluginCatalogEntry?> DiscoverFromAssemblyPathAsync(string assemblyPath, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!File.Exists(assemblyPath))
                return null;

            var assemblyBaseName = Path.GetFileNameWithoutExtension(assemblyPath);
            if (_aliases.TryGetValue(assemblyBaseName, out var knownName) &&
                _catalog.TryGetValue(knownName, out var knownEntry) &&
                string.Equals(knownEntry.AssemblyPath, assemblyPath, StringComparison.OrdinalIgnoreCase))
            {
                return knownEntry;
            }

            PluginLoadContext? discoveryContext = null;
            try
            {
                discoveryContext = new PluginLoadContext(assemblyPath);
                var assembly = discoveryContext.LoadFromAssemblyPath(assemblyPath);
                var pluginTypes = GetPluginTypes(assembly);
                if (pluginTypes.Count == 0)
                    return null;

                var pluginType = pluginTypes.First();
                var metadata = DiscoverPluginMetadata(pluginType, assemblyPath);
                var enabled = !_options.DisabledPlugins.Contains(metadata.Name);

                var entry = _catalog.AddOrUpdate(
                    metadata.Name,
                    _ => new PluginCatalogEntry(metadata, assemblyPath, enabled),
                    (_, existing) =>
                    {
                        existing.Metadata = metadata;
                        existing.AssemblyPath = assemblyPath;
                        existing.Enabled = enabled;
                        return existing;
                    });

                _aliases[assemblyBaseName] = metadata.Name;
                _aliases[metadata.Name] = metadata.Name;

                OnPluginMetadataDiscovered?.Invoke(metadata);
                return entry;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to discover plugin metadata: {Path}", assemblyPath);
                return null;
            }
            finally
            {
                discoveryContext?.Unload();
                await Task.CompletedTask.ConfigureAwait(false);
            }
        }

        [RequiresUnreferencedCode("Plugin metadata discovery uses reflection to read attributes and properties")]
        [RequiresDynamicCode("Plugin metadata discovery may require dynamic code generation")]
        private static PluginMetadata DiscoverPluginMetadata(Type pluginType, string assemblyPath)
        {
            // Preferred: public static PluginMetadata Metadata { get; }
            var metadataProperty = pluginType.GetProperty("Metadata", BindingFlags.Public | BindingFlags.Static);
            if (metadataProperty is not null && metadataProperty.PropertyType == typeof(PluginMetadata))
            {
                if (metadataProperty.GetValue(null) is PluginMetadata metadataFromProperty)
                    return NormalizeMetadata(metadataFromProperty, pluginType, assemblyPath);
            }

            // Assembly-level discovery via [assembly: Plugin(...)] and [assembly: PluginDependency(...)]
            var assemblyPluginAttribute = pluginType.Assembly.GetCustomAttribute<PluginAttribute>();
            if (assemblyPluginAttribute is not null)
            {
                if (!PluginMetadata.TryParseVersion(assemblyPluginAttribute.Version, out var version))
                    version = pluginType.Assembly.GetName().Version ?? new Version(1, 0, 0);

                PluginMetadata.TryParseVersion(assemblyPluginAttribute.MinNeoVersion, out var minNeoVersion);
                PluginMetadata.TryParseVersion(assemblyPluginAttribute.MaxNeoVersion, out var maxNeoVersion);

                var dependencies = pluginType.Assembly
                    .GetCustomAttributes<PluginDependencyAttribute>()
                    .Select(static d => new PluginDependency(d.Name, d.VersionRange))
                    .ToArray();

                var pluginAuthor = pluginType.Assembly.GetCustomAttribute<AssemblyCompanyAttribute>()?.Company;
                var license = pluginType.Assembly
                    .GetCustomAttributes<AssemblyMetadataAttribute>()
                    .FirstOrDefault(static a => string.Equals(a.Key, "License", StringComparison.OrdinalIgnoreCase) ||
                                               string.Equals(a.Key, "LicenseExpression", StringComparison.OrdinalIgnoreCase))
                    ?.Value;

                return new PluginMetadata
                {
                    Name = assemblyPluginAttribute.Name,
                    Version = version,
                    Description = assemblyPluginAttribute.Description ?? string.Empty,
                    Dependencies = dependencies,
                    MinNeoVersion = minNeoVersion,
                    MaxNeoVersion = maxNeoVersion,
                    Author = pluginAuthor,
                    License = license
                };
            }

            // Legacy type-level discovery via [PluginMetadata(...)]
            var attribute = pluginType.GetCustomAttribute<PluginMetadataAttribute>();
            if (attribute is not null)
                return NormalizeMetadata(PluginMetadata.FromAttribute(attribute), pluginType, assemblyPath);

            // Fallback: assembly name + assembly attributes
            var assemblyName = pluginType.Assembly.GetName();
            var description = pluginType.Assembly.GetCustomAttribute<AssemblyDescriptionAttribute>()?.Description ?? string.Empty;
            var author = pluginType.Assembly.GetCustomAttribute<AssemblyCompanyAttribute>()?.Company;

            return new PluginMetadata
            {
                Name = assemblyName.Name ?? Path.GetFileNameWithoutExtension(assemblyPath),
                Version = assemblyName.Version ?? new Version(1, 0, 0),
                Description = description,
                Dependencies = Array.Empty<PluginDependency>(),
                Author = author
            };
        }

        private static PluginMetadata NormalizeMetadata(PluginMetadata metadata, Type pluginType, string assemblyPath)
        {
            var name = metadata.Name;
            if (string.IsNullOrWhiteSpace(name))
                name = pluginType.Assembly.GetName().Name ?? Path.GetFileNameWithoutExtension(assemblyPath);

            return new PluginMetadata
            {
                Name = name,
                Version = metadata.Version,
                Description = metadata.Description ?? string.Empty,
                Dependencies = metadata.Dependencies ?? Array.Empty<PluginDependency>(),
                MinNeoVersion = metadata.MinNeoVersion,
                MaxNeoVersion = metadata.MaxNeoVersion,
                Author = metadata.Author,
                License = metadata.License
            };
        }

        [RequiresUnreferencedCode("Plugin loading uses reflection to discover types and instantiate plugins")]
        [RequiresDynamicCode("Plugin loading may require dynamic code generation")]
        private async Task<IPlugin?> LoadPluginInternalAsync(string assemblyPath, HashSet<string> loadingStack, CancellationToken cancellationToken)
        {
            var discovered = await DiscoverFromAssemblyPathAsync(assemblyPath, cancellationToken).ConfigureAwait(false);
            if (discovered is null)
                return null;

            var pluginName = discovered.Metadata.Name;
            if (!loadingStack.Add(pluginName))
                throw new InvalidOperationException($"Circular plugin dependency detected while loading '{pluginName}'.");

            try
            {
                if (!discovered.Enabled)
                {
                    _logger?.LogInformation("Plugin is disabled and will not be loaded: {Name}", pluginName);
                    return null;
                }

                if (!discovered.Metadata.IsCompatibleWith(_options.NeoVersion))
                {
                    _logger?.LogWarning(
                        "Plugin {Name} v{Version} is not compatible with Neo v{NeoVersion} (Min={Min}, Max={Max})",
                        discovered.Metadata.Name, discovered.Metadata.Version, _options.NeoVersion, discovered.Metadata.MinNeoVersion, discovered.Metadata.MaxNeoVersion);
                    return null;
                }

                foreach (var dependency in discovered.Metadata.Dependencies)
                {
                    var dependencyName = dependency.Name;
                    if (string.IsNullOrWhiteSpace(dependencyName))
                        continue;

                    var canonicalDependencyName = ResolvePluginName(dependencyName) ?? dependencyName;
                    if (_plugins.ContainsKey(canonicalDependencyName))
                        continue;

                    if (!_catalog.TryGetValue(canonicalDependencyName, out var depEntry))
                        throw new InvalidOperationException($"Missing dependency '{dependencyName}' required by plugin '{pluginName}'.");

                    if (!depEntry.Enabled)
                        throw new InvalidOperationException($"Dependency '{dependencyName}' required by plugin '{pluginName}' is disabled.");

                    if (!string.IsNullOrWhiteSpace(dependency.VersionRange) &&
                        !string.Equals(dependency.VersionRange, "*", StringComparison.Ordinal) &&
                        !PluginVersionResolver.IsCompatible(depEntry.Metadata.Version, dependency.VersionRange))
                    {
                        throw new InvalidOperationException(
                            $"Dependency '{dependencyName}' does not satisfy version range '{dependency.VersionRange}' for plugin '{pluginName}'.");
                    }

                    await LoadPluginInternalAsync(depEntry.AssemblyPath, loadingStack, cancellationToken).ConfigureAwait(false);
                    if (!_plugins.ContainsKey(canonicalDependencyName))
                        throw new InvalidOperationException($"Dependency '{dependencyName}' required by plugin '{pluginName}' could not be loaded.");
                }

                if (_plugins.ContainsKey(pluginName))
                {
                    if (_options.EnableHotReload)
                    {
                        await UnloadPluginInternalAsync(pluginName, cancellationToken).ConfigureAwait(false);
                    }
                    else
                    {
                        _logger?.LogWarning("Plugin already loaded: {Name}", pluginName);
                        return _plugins[pluginName].Plugin;
                    }
                }

                var loadContext = new PluginLoadContext(assemblyPath);
                var assembly = loadContext.LoadFromAssemblyPath(assemblyPath);

                var pluginTypes = GetPluginTypes(assembly);
                if (pluginTypes.Count == 0)
                {
                    _logger?.LogWarning("No plugin types found in assembly: {Path}", assemblyPath);
                    loadContext.Unload();
                    return null;
                }

                var pluginType = pluginTypes.First();
                var plugin = CreatePluginInstance(pluginType);
                if (plugin is null)
                {
                    _logger?.LogError("Failed to create plugin instance: {Type}", pluginType.FullName);
                    loadContext.Unload();
                    return null;
                }

                _aliases[plugin.Name] = pluginName;
                _plugins[pluginName] = new PluginContext(plugin, loadContext, assemblyPath);

                await plugin.InitializeAsync(cancellationToken).ConfigureAwait(false);
                if (_options.StartPluginsOnLoad)
                    await plugin.StartAsync(cancellationToken).ConfigureAwait(false);

                if (_options.EnableConfigReload)
                    TryEnableConfigWatcher(pluginName, assemblyPath);

                _logger?.LogInformation("Plugin loaded: {Name} v{Version}", plugin.Name, plugin.Version);
                OnPluginLoaded?.Invoke(plugin);

                return plugin;
            }
            finally
            {
                loadingStack.Remove(pluginName);
            }
        }

        private async Task<bool> UnloadPluginInternalAsync(string pluginName, CancellationToken cancellationToken)
        {
            pluginName = ResolvePluginName(pluginName) ?? pluginName;

            if (!_plugins.TryRemove(pluginName, out var ctx))
                return false;

            if (_configWatchers.TryRemove(pluginName, out var watcher))
            {
                try { watcher.Dispose(); }
                catch { }
            }

            try
            {
                if (ctx.Plugin.State == PluginState.Running)
                    await ctx.Plugin.StopAsync(cancellationToken).ConfigureAwait(false);

                await ctx.Plugin.ShutdownAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error during plugin shutdown: {Name}", pluginName);
            }

            try
            {
                if (ctx.Plugin is IAsyncDisposable asyncDisposable)
                    await asyncDisposable.DisposeAsync().ConfigureAwait(false);
                else if (ctx.Plugin is IDisposable disposable)
                    disposable.Dispose();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error disposing plugin: {Name}", pluginName);
            }

            ctx.LoadContext.Unload();

            _logger?.LogInformation("Plugin unloaded: {Name}", pluginName);
            OnPluginUnloaded?.Invoke(pluginName);
            return true;
        }

        [RequiresUnreferencedCode("Plugin instantiation uses reflection to discover and invoke constructors")]
        [RequiresDynamicCode("Plugin instantiation may require dynamic code generation for constructor invocation")]
        private IPlugin? CreatePluginInstance(
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type pluginType)
        {
            try
            {
                var constructor = pluginType.GetConstructors()
                    .OrderByDescending(static c => c.GetParameters().Length)
                    .FirstOrDefault();

                if (constructor == null)
                    return Activator.CreateInstance(pluginType) as IPlugin;

                var parameters = constructor.GetParameters();
                var args = new object?[parameters.Length];

                for (var i = 0; i < parameters.Length; i++)
                    args[i] = _serviceProvider.GetService(parameters[i].ParameterType);

                return constructor.Invoke(args) as IPlugin;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to create plugin instance: {Type}", pluginType.FullName);
                return null;
            }
        }

        private void TryEnableConfigWatcher(string pluginName, string assemblyPath)
        {
            if (_configWatchers.ContainsKey(pluginName))
                return;

            var configPath = GetDefaultConfigurationPath(assemblyPath);
            if (string.IsNullOrEmpty(configPath))
                return;

            var directory = Path.GetDirectoryName(configPath);
            var filename = Path.GetFileName(configPath);
            if (string.IsNullOrEmpty(directory) || string.IsNullOrEmpty(filename))
                return;

            if (!Directory.Exists(directory))
                return;

            var watcher = new FileSystemWatcher(directory, filename)
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size,
                EnableRaisingEvents = true
            };

            watcher.Changed += (_, e) => OnConfigFileChanged(pluginName, e.FullPath);
            watcher.Created += (_, e) => OnConfigFileChanged(pluginName, e.FullPath);
            watcher.Renamed += (_, e) => OnConfigFileChanged(pluginName, e.FullPath);

            _configWatchers[pluginName] = watcher;
        }

        private async void OnConfigFileChanged(string pluginName, string configPath)
        {
            if (!_options.EnableConfigReload) return;

            await Task.Delay(_options.ConfigReloadDebounce).ConfigureAwait(false);

            try
            {
                await _lock.WaitAsync().ConfigureAwait(false);
                try
                {
                    if (!_plugins.TryGetValue(pluginName, out var ctx))
                        return;

                    if (ctx.Plugin is not IPluginConfigurationReloadable reloadable)
                        return;

                    await reloadable.ReloadConfigurationAsync(configPath).ConfigureAwait(false);
                }
                finally
                {
                    _lock.Release();
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to reload configuration for plugin: {Name}", pluginName);
            }
        }

        private async void OnPluginFileChanged(object sender, FileSystemEventArgs e)
        {
            if (!_options.EnableHotReload) return;

            await Task.Delay(_options.HotReloadDebounce).ConfigureAwait(false);

            try
            {
                await LoadPluginAsync(e.FullPath).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to hot-reload plugin: {Path}", e.FullPath);
                OnPluginLoadFailed?.Invoke(e.FullPath, ex);
            }
        }

        private async void OnPluginFileDeleted(object sender, FileSystemEventArgs e)
        {
            if (!_options.EnableHotReload) return;

            var assemblyBaseName = Path.GetFileNameWithoutExtension(e.FullPath);
            var pluginName = ResolvePluginName(assemblyBaseName) ?? assemblyBaseName;

            try
            {
                await UnloadPluginAsync(pluginName).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to unload deleted plugin: {Name}", pluginName);
            }
        }

        [RequiresUnreferencedCode("Plugin type discovery enumerates all types in the assembly using reflection")]
        [RequiresDynamicCode("Plugin type discovery may require dynamic code generation")]
        private static IReadOnlyList<Type> GetPluginTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes()
                    .Where(t => typeof(IPlugin).IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface)
                    .ToList();
            }
            catch (ReflectionTypeLoadException ex)
            {
                return ex.Types
                    .Where(static t => t is not null)
                    .Select(static t => t!)
                    .Where(t => typeof(IPlugin).IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface)
                    .ToList();
            }
        }

        private string? ResolvePluginName(string nameOrAlias)
        {
            if (string.IsNullOrWhiteSpace(nameOrAlias))
                return null;

            if (_aliases.TryGetValue(nameOrAlias, out var canonical))
                return canonical;

            return _catalog.ContainsKey(nameOrAlias) ? nameOrAlias : null;
        }

        private string? GetDefaultConfigurationPath(string assemblyPath)
        {
            if (string.IsNullOrEmpty(_options.PluginConfigFileName))
                return null;

            var directory = Path.GetDirectoryName(assemblyPath);
            if (string.IsNullOrEmpty(directory))
                return null;

            return Path.Combine(directory, _options.PluginConfigFileName);
        }

        /// <inheritdoc/>
        public async ValueTask DisposeAsync()
        {
            if (_disposed) return;
            _disposed = true;

            _pluginWatcher?.Dispose();

            foreach (var name in _plugins.Keys.ToList())
            {
                try
                {
                    await UnloadPluginAsync(name).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error unloading plugin during disposal: {Name}", name);
                }
            }

            foreach (var watcher in _configWatchers.Values)
            {
                try { watcher.Dispose(); }
                catch { }
            }

            _lock.Dispose();
        }

        private sealed class PluginCatalogEntry
        {
            public PluginMetadata Metadata { get; set; }
            public string AssemblyPath { get; set; }
            public bool Enabled { get; set; }

            public PluginCatalogEntry(PluginMetadata metadata, string assemblyPath, bool enabled)
            {
                Metadata = metadata;
                AssemblyPath = assemblyPath;
                Enabled = enabled;
            }
        }

        private sealed class PluginContext
        {
            public IPlugin Plugin { get; }
            public PluginLoadContext LoadContext { get; }
            public string AssemblyPath { get; }
            public DateTime LoadedAt { get; }

            public PluginContext(IPlugin plugin, PluginLoadContext loadContext, string assemblyPath)
            {
                Plugin = plugin;
                LoadContext = loadContext;
                AssemblyPath = assemblyPath;
                LoadedAt = DateTime.UtcNow;
            }
        }
    }

    /// <summary>
    /// Isolated assembly load context for plugins.
    /// </summary>
    internal sealed class PluginLoadContext : AssemblyLoadContext
    {
        private readonly AssemblyDependencyResolver _resolver;

        /// <summary>
        /// Initializes a new instance of the <see cref="PluginLoadContext"/> class.
        /// </summary>
        /// <param name="pluginPath">The plugin assembly path.</param>
        public PluginLoadContext(string pluginPath) : base(isCollectible: true)
        {
            _resolver = new AssemblyDependencyResolver(pluginPath);
        }

        protected override Assembly? Load(AssemblyName assemblyName)
        {
            // Ensure Neo.Plugins is unified with the default load context, even if the plugin ships its own copy.
            var hostPluginsAssemblyName = typeof(IPlugin).Assembly.GetName().Name;
            if (!string.IsNullOrEmpty(hostPluginsAssemblyName) &&
                string.Equals(assemblyName.Name, hostPluginsAssemblyName, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
            return assemblyPath != null ? LoadFromAssemblyPath(assemblyPath) : null;
        }

        protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
        {
            var libraryPath = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
            return libraryPath != null ? LoadUnmanagedDllFromPath(libraryPath) : IntPtr.Zero;
        }
    }

    /// <summary>
    /// Configuration options for <see cref="PluginManager"/>.
    /// </summary>
    public sealed class PluginManagerOptions
    {
        /// <summary>
        /// Directory to scan for plugins. Default: "Plugins".
        /// </summary>
        public string PluginDirectory { get; set; } = "Plugins";

        /// <summary>
        /// Enable hot-reload of plugins when files change. Default: false.
        /// </summary>
        public bool EnableHotReload { get; set; } = false;

        /// <summary>
        /// Debounce time for hot-reload. Default: 500ms.
        /// </summary>
        public TimeSpan HotReloadDebounce { get; set; } = TimeSpan.FromMilliseconds(500);

        /// <summary>
        /// Gets or sets the Neo version used for plugin compatibility checks.
        /// </summary>
        public Version NeoVersion { get; set; } = typeof(PluginManager).Assembly.GetName().Version ?? new Version(0, 0);

        /// <summary>
        /// Gets the set of disabled plugins.
        /// </summary>
        public HashSet<string> DisabledPlugins { get; } = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Enables configuration reload notifications for plugins implementing <see cref="IPluginConfigurationReloadable"/>.
        /// Default: false.
        /// </summary>
        public bool EnableConfigReload { get; set; } = false;

        /// <summary>
        /// The configuration file name to watch next to the plugin assembly. Default: "config.json".
        /// </summary>
        public string PluginConfigFileName { get; set; } = "config.json";

        /// <summary>
        /// Debounce time for configuration reload notifications. Default: 500ms.
        /// </summary>
        public TimeSpan ConfigReloadDebounce { get; set; } = TimeSpan.FromMilliseconds(500);

        /// <summary>
        /// Starts plugins after a successful load and initialization. Default: true.
        /// </summary>
        public bool StartPluginsOnLoad { get; set; } = true;

        /// <summary>
        /// Unloads plugins when they are disabled. Default: false.
        /// </summary>
        public bool UnloadPluginsOnDisable { get; set; } = false;
    }
}

