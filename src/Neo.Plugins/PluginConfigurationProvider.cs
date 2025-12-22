// Copyright (C) 2015-2025 The Neo Project.
//
// PluginConfigurationProvider.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Neo.Plugins
{
    /// <summary>
    /// Provides plugin-specific configuration injection with hot-reload support.
    /// Integrates with IPluginConfigurationReloadable for automatic configuration updates.
    /// </summary>
    public sealed class PluginConfigurationProvider : IDisposable
    {
        private readonly ConcurrentDictionary<string, PluginConfigurationContext> _configurations = new(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<string, FileSystemWatcher> _watchers = new(StringComparer.OrdinalIgnoreCase);
        private readonly string _configurationDirectory;
        private readonly bool _enableHotReload;
        private readonly TimeSpan _reloadDebounce;
        private int _disposed;

        /// <summary>
        /// Gets the number of registered plugin configurations.
        /// </summary>
        public int ConfigurationCount => _configurations.Count;

        /// <summary>
        /// Creates a new plugin configuration provider.
        /// </summary>
        /// <param name="configurationDirectory">The directory containing plugin configuration files.</param>
        /// <param name="enableHotReload">Whether to enable hot-reload of configuration files.</param>
        /// <param name="reloadDebounce">The debounce time for configuration reload events.</param>
        public PluginConfigurationProvider(
            string configurationDirectory,
            bool enableHotReload = true,
            TimeSpan? reloadDebounce = null)
        {
            _configurationDirectory = configurationDirectory ?? throw new ArgumentNullException(nameof(configurationDirectory));
            _enableHotReload = enableHotReload;
            _reloadDebounce = reloadDebounce ?? TimeSpan.FromMilliseconds(500);
        }

        /// <summary>
        /// Loads configuration for a plugin from a JSON file.
        /// </summary>
        /// <param name="pluginName">The name of the plugin.</param>
        /// <param name="configFileName">The configuration file name (default: "config.json").</param>
        /// <returns>The configuration root, or null if the file doesn't exist.</returns>
        public IConfigurationRoot? LoadConfiguration(string pluginName, string configFileName = "config.json")
        {
            ThrowIfDisposed();

            if (string.IsNullOrWhiteSpace(pluginName))
                throw new ArgumentException("Plugin name cannot be null or whitespace.", nameof(pluginName));

            var configPath = Path.Combine(_configurationDirectory, pluginName, configFileName);
            if (!File.Exists(configPath))
            {
                // Try alternative path: PluginDirectory/config.json
                configPath = Path.Combine(_configurationDirectory, configFileName);
                if (!File.Exists(configPath))
                    return null;
            }

            var builder = new ConfigurationBuilder()
                .SetBasePath(Path.GetDirectoryName(configPath)!)
                .AddJsonFile(Path.GetFileName(configPath), optional: true, reloadOnChange: _enableHotReload);

            var configuration = builder.Build();

            var context = new PluginConfigurationContext(pluginName, configPath, configuration);
            _configurations[pluginName] = context;

            if (_enableHotReload)
            {
                SetupConfigurationWatcher(pluginName, configPath);
            }

            return configuration;
        }

        /// <summary>
        /// Gets the configuration for a plugin.
        /// </summary>
        /// <param name="pluginName">The name of the plugin.</param>
        /// <returns>The configuration root, or null if not loaded.</returns>
        public IConfigurationRoot? GetConfiguration(string pluginName)
        {
            ThrowIfDisposed();

            if (string.IsNullOrWhiteSpace(pluginName))
                throw new ArgumentException("Plugin name cannot be null or whitespace.", nameof(pluginName));

            return _configurations.TryGetValue(pluginName, out var context) ? context.Configuration : null;
        }

        /// <summary>
        /// Gets a configuration section for a plugin.
        /// </summary>
        /// <param name="pluginName">The name of the plugin.</param>
        /// <param name="sectionName">The section name.</param>
        /// <returns>The configuration section, or null if not found.</returns>
        public IConfigurationSection? GetSection(string pluginName, string sectionName)
        {
            ThrowIfDisposed();

            var configuration = GetConfiguration(pluginName);
            return configuration?.GetSection(sectionName);
        }

        /// <summary>
        /// Binds configuration to a strongly-typed object.
        /// </summary>
        /// <typeparam name="T">The type to bind to.</typeparam>
        /// <param name="pluginName">The name of the plugin.</param>
        /// <param name="sectionName">The section name (optional, uses root if null).</param>
        /// <returns>The bound configuration object, or default if configuration not found.</returns>
        public T? Bind<T>(string pluginName, string? sectionName = null) where T : class, new()
        {
            ThrowIfDisposed();

            var configuration = GetConfiguration(pluginName);
            if (configuration == null)
                return null;

            var instance = new T();
            if (string.IsNullOrWhiteSpace(sectionName))
            {
                configuration.Bind(instance);
            }
            else
            {
                var section = configuration.GetSection(sectionName);
                section.Bind(instance);
            }

            return instance;
        }

        /// <summary>
        /// Reloads configuration for a plugin.
        /// </summary>
        /// <param name="pluginName">The name of the plugin.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>True if the configuration was reloaded; false if not found.</returns>
        public async Task<bool> ReloadConfigurationAsync(string pluginName, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            if (!_configurations.TryGetValue(pluginName, out var context))
                return false;

            // Reload the configuration
            var newConfiguration = LoadConfiguration(pluginName, Path.GetFileName(context.ConfigPath));
            if (newConfiguration == null)
                return false;

            // Update the context
            context.Configuration = newConfiguration;
            context.LastReloadTime = DateTime.UtcNow;

            await Task.CompletedTask.ConfigureAwait(false);
            return true;
        }

        /// <summary>
        /// Unloads configuration for a plugin.
        /// </summary>
        /// <param name="pluginName">The name of the plugin.</param>
        /// <returns>True if the configuration was unloaded; false if not found.</returns>
        public bool UnloadConfiguration(string pluginName)
        {
            ThrowIfDisposed();

            if (!_configurations.TryRemove(pluginName, out var context))
                return false;

            // Dispose the watcher if it exists
            if (_watchers.TryRemove(pluginName, out var watcher))
            {
                try { watcher.Dispose(); }
                catch { }
            }

            // Dispose the configuration if it's disposable
            if (context.Configuration is IDisposable disposable)
            {
                try { disposable.Dispose(); }
                catch { }
            }

            return true;
        }

        /// <summary>
        /// Registers a callback to be invoked when a plugin's configuration changes.
        /// </summary>
        /// <param name="pluginName">The name of the plugin.</param>
        /// <param name="callback">The callback to invoke.</param>
        /// <returns>A disposable token that can be used to unregister the callback.</returns>
        public IDisposable? RegisterChangeCallback(string pluginName, Action<IConfigurationRoot> callback)
        {
            ThrowIfDisposed();

            if (callback == null)
                throw new ArgumentNullException(nameof(callback));

            var configuration = GetConfiguration(pluginName);
            if (configuration == null)
                return null;

            return configuration.GetReloadToken().RegisterChangeCallback(_ => callback(configuration), null);
        }

        private void SetupConfigurationWatcher(string pluginName, string configPath)
        {
            if (_watchers.ContainsKey(pluginName))
                return;

            var directory = Path.GetDirectoryName(configPath);
            var fileName = Path.GetFileName(configPath);

            if (string.IsNullOrEmpty(directory) || string.IsNullOrEmpty(fileName))
                return;

            if (!Directory.Exists(directory))
                return;

            var watcher = new FileSystemWatcher(directory, fileName)
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size,
                EnableRaisingEvents = true
            };

            watcher.Changed += (_, e) => OnConfigurationFileChanged(pluginName, e.FullPath);
            watcher.Created += (_, e) => OnConfigurationFileChanged(pluginName, e.FullPath);
            watcher.Renamed += (_, e) => OnConfigurationFileChanged(pluginName, e.FullPath);

            _watchers[pluginName] = watcher;
        }

        private async void OnConfigurationFileChanged(string pluginName, string configPath)
        {
            if (!_enableHotReload)
                return;

            // Debounce rapid changes
            await Task.Delay(_reloadDebounce).ConfigureAwait(false);

            try
            {
                await ReloadConfigurationAsync(pluginName).ConfigureAwait(false);
            }
            catch
            {
                // Silently ignore reload errors
            }
        }

        /// <summary>
        /// Disposes the plugin configuration provider.
        /// </summary>
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                // Dispose all watchers
                foreach (var watcher in _watchers.Values)
                {
                    try { watcher.Dispose(); }
                    catch { }
                }
                _watchers.Clear();

                // Dispose all configurations
                foreach (var context in _configurations.Values)
                {
                    if (context.Configuration is IDisposable disposable)
                    {
                        try { disposable.Dispose(); }
                        catch { }
                    }
                }
                _configurations.Clear();
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed != 0)
            {
                throw new ObjectDisposedException(nameof(PluginConfigurationProvider));
            }
        }
    }

    /// <summary>
    /// Represents the configuration context for a plugin.
    /// </summary>
    internal sealed class PluginConfigurationContext
    {
        public string PluginName { get; }
        public string ConfigPath { get; }
        public IConfigurationRoot Configuration { get; set; }
        public DateTime LoadedTime { get; }
        public DateTime LastReloadTime { get; set; }

        public PluginConfigurationContext(string pluginName, string configPath, IConfigurationRoot configuration)
        {
            PluginName = pluginName;
            ConfigPath = configPath;
            Configuration = configuration;
            LoadedTime = DateTime.UtcNow;
            LastReloadTime = LoadedTime;
        }
    }

    /// <summary>
    /// Extension methods for plugin configuration.
    /// </summary>
    public static class PluginConfigurationExtensions
    {
        /// <summary>
        /// Gets a configuration value with a default fallback.
        /// </summary>
        /// <typeparam name="T">The type of the value.</typeparam>
        /// <param name="configuration">The configuration.</param>
        /// <param name="key">The configuration key.</param>
        /// <param name="defaultValue">The default value if the key is not found.</param>
        /// <returns>The configuration value or the default value.</returns>
        public static T GetValueOrDefault<T>(this IConfiguration configuration, string key, T defaultValue)
        {
            var value = configuration[key];
            if (value == null)
                return defaultValue;

            try
            {
                return (T)Convert.ChangeType(value, typeof(T));
            }
            catch
            {
                return defaultValue;
            }
        }

        /// <summary>
        /// Checks if a configuration section exists and has values.
        /// </summary>
        /// <param name="configuration">The configuration.</param>
        /// <param name="sectionName">The section name.</param>
        /// <returns>True if the section exists and has values; otherwise, false.</returns>
        public static bool HasSection(this IConfiguration configuration, string sectionName)
        {
            var section = configuration.GetSection(sectionName);
            return section.Exists();
        }
    }
}
