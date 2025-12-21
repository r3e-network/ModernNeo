// Copyright (C) 2015-2025 The Neo Project.
//
// IPlugin.cs file belongs to the neo project and is free
// software distributed under the MIT software license.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Neo.Plugins
{
    /// <summary>
    /// Base interface for all Neo plugins with lifecycle management.
    /// </summary>
    public interface IPlugin
    {
        /// <summary>
        /// Gets the unique name of the plugin.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Gets the version of the plugin.
        /// </summary>
        Version Version { get; }

        /// <summary>
        /// Gets the description of the plugin.
        /// </summary>
        string Description { get; }

        /// <summary>
        /// Gets the current state of the plugin.
        /// </summary>
        PluginState State { get; }

        /// <summary>
        /// Initializes the plugin. Called when the plugin is first loaded.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task InitializeAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Starts the plugin. Called after initialization.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task StartAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Stops the plugin. Called before shutdown.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task StopAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Shuts down the plugin. Called when the plugin is being unloaded.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task ShutdownAsync(CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Plugin lifecycle states.
    /// </summary>
    public enum PluginState
    {
        /// <summary>
        /// Plugin is not loaded.
        /// </summary>
        Unloaded = 0,

        /// <summary>
        /// Plugin is being initialized.
        /// </summary>
        Initializing = 1,

        /// <summary>
        /// Plugin is initialized but not started.
        /// </summary>
        Initialized = 2,

        /// <summary>
        /// Plugin is starting.
        /// </summary>
        Starting = 3,

        /// <summary>
        /// Plugin is running.
        /// </summary>
        Running = 4,

        /// <summary>
        /// Plugin is stopping.
        /// </summary>
        Stopping = 5,

        /// <summary>
        /// Plugin is stopped.
        /// </summary>
        Stopped = 6,

        /// <summary>
        /// Plugin encountered an error.
        /// </summary>
        Faulted = 7
    }

    /// <summary>
    /// Base class for plugins with default lifecycle implementation.
    /// </summary>
    public abstract class PluginBase : IPlugin, IAsyncDisposable
    {
        private PluginState _state = PluginState.Unloaded;

        /// <inheritdoc/>
        public abstract string Name { get; }

        /// <inheritdoc/>
        public virtual Version Version => GetType().Assembly.GetName().Version ?? new Version(1, 0, 0);

        /// <inheritdoc/>
        public virtual string Description => string.Empty;

        /// <inheritdoc/>
        public PluginState State
        {
            get => _state;
            protected set => _state = value;
        }

        /// <inheritdoc/>
        public virtual Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            State = PluginState.Initializing;
            OnInitialize();
            State = PluginState.Initialized;
            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public virtual Task StartAsync(CancellationToken cancellationToken = default)
        {
            State = PluginState.Starting;
            OnStart();
            State = PluginState.Running;
            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public virtual Task StopAsync(CancellationToken cancellationToken = default)
        {
            State = PluginState.Stopping;
            OnStop();
            State = PluginState.Stopped;
            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public virtual Task ShutdownAsync(CancellationToken cancellationToken = default)
        {
            OnShutdown();
            State = PluginState.Unloaded;
            return Task.CompletedTask;
        }

        /// <summary>
        /// Called during initialization. Override to add custom initialization logic.
        /// </summary>
        protected virtual void OnInitialize() { }

        /// <summary>
        /// Called when starting. Override to add custom start logic.
        /// </summary>
        protected virtual void OnStart() { }

        /// <summary>
        /// Called when stopping. Override to add custom stop logic.
        /// </summary>
        protected virtual void OnStop() { }

        /// <summary>
        /// Called during shutdown. Override to add custom shutdown logic.
        /// </summary>
        protected virtual void OnShutdown() { }

        /// <inheritdoc/>
        public virtual ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }
}
