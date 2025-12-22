// Copyright (C) 2015-2025 The Neo Project.
//
// MessagePackGrainStorage.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Neo.Serialization.MessagePack;
using Orleans.Configuration;
using Orleans.Runtime;
using Orleans.Storage;

namespace Neo.Orleans.Storage
{
    /// <summary>
    /// Orleans grain storage provider using MessagePack serialization.
    /// Provides high-performance binary serialization for grain state persistence.
    /// </summary>
    public class MessagePackGrainStorage : IGrainStorage, ILifecycleParticipant<ISiloLifecycle>
    {
        private readonly string _name;
        private readonly MessagePackGrainStorageOptions _options;
        private readonly ILogger<MessagePackGrainStorage> _logger;
        private readonly Dictionary<string, byte[]> _storage = new();
        private readonly object _lock = new();

        public MessagePackGrainStorage(
            string name,
            MessagePackGrainStorageOptions options,
            ILogger<MessagePackGrainStorage> logger)
        {
            _name = name;
            _options = options;
            _logger = logger;
        }

        public Task ReadStateAsync<T>(string stateName, GrainId grainId, IGrainState<T> grainState)
        {
            var key = GetKey(stateName, grainId);

            lock (_lock)
            {
                if (_storage.TryGetValue(key, out var data))
                {
                    try
                    {
                        grainState.State = _options.UseCompression
                            ? NeoMessagePackSerializer.DeserializeCompressed<T>(data)
                            : NeoMessagePackSerializer.Deserialize<T>(data);
                        grainState.RecordExists = true;
                        grainState.ETag = ComputeETag(data);

                        _logger.LogDebug("Read state for grain {GrainId}, state {StateName}, size {Size} bytes",
                            grainId, stateName, data.Length);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to deserialize state for grain {GrainId}, state {StateName}",
                            grainId, stateName);
                        throw;
                    }
                }
                else
                {
                    grainState.State = default!;
                    grainState.RecordExists = false;
                    grainState.ETag = null;
                }
            }

            return Task.CompletedTask;
        }

        public Task WriteStateAsync<T>(string stateName, GrainId grainId, IGrainState<T> grainState)
        {
            var key = GetKey(stateName, grainId);

            lock (_lock)
            {
                try
                {
                    var data = _options.UseCompression
                        ? NeoMessagePackSerializer.SerializeCompressed(grainState.State)
                        : NeoMessagePackSerializer.Serialize(grainState.State);

                    // Optimistic concurrency check
                    if (grainState.ETag != null && _storage.TryGetValue(key, out var existing))
                    {
                        var existingETag = ComputeETag(existing);
                        if (existingETag != grainState.ETag)
                        {
                            throw new InconsistentStateException(
                                $"ETag mismatch for grain {grainId}, state {stateName}. " +
                                $"Expected: {grainState.ETag}, Actual: {existingETag}");
                        }
                    }

                    _storage[key] = data;
                    grainState.RecordExists = true;
                    grainState.ETag = ComputeETag(data);

                    _logger.LogDebug("Wrote state for grain {GrainId}, state {StateName}, size {Size} bytes",
                        grainId, stateName, data.Length);
                }
                catch (InconsistentStateException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to serialize state for grain {GrainId}, state {StateName}",
                        grainId, stateName);
                    throw;
                }
            }

            return Task.CompletedTask;
        }

        public Task ClearStateAsync<T>(string stateName, GrainId grainId, IGrainState<T> grainState)
        {
            var key = GetKey(stateName, grainId);

            lock (_lock)
            {
                _storage.Remove(key);
                grainState.State = default!;
                grainState.RecordExists = false;
                grainState.ETag = null;

                _logger.LogDebug("Cleared state for grain {GrainId}, state {StateName}", grainId, stateName);
            }

            return Task.CompletedTask;
        }

        public void Participate(ISiloLifecycle lifecycle)
        {
            lifecycle.Subscribe(
                observerName: _name,
                stage: ServiceLifecycleStage.ApplicationServices,
                onStart: ct =>
                {
                    _logger.LogInformation("MessagePack grain storage '{Name}' started", _name);
                    return Task.CompletedTask;
                },
                onStop: ct =>
                {
                    _logger.LogInformation("MessagePack grain storage '{Name}' stopped", _name);
                    return Task.CompletedTask;
                });
        }

        private static string GetKey(string stateName, GrainId grainId) =>
            $"{grainId}|{stateName}";

        private static string ComputeETag(byte[] data) =>
            Convert.ToBase64String(System.Security.Cryptography.SHA256.HashData(data)[..8]);
    }

    /// <summary>
    /// Options for MessagePack grain storage.
    /// </summary>
    public class MessagePackGrainStorageOptions
    {
        /// <summary>
        /// Whether to use LZ4 compression for stored data.
        /// Default: true (recommended for production)
        /// </summary>
        public bool UseCompression { get; set; } = true;
    }

    /// <summary>
    /// Factory for creating MessagePack grain storage instances.
    /// </summary>
    public static class MessagePackGrainStorageFactory
    {
        public static IGrainStorage Create(IServiceProvider services, string name)
        {
            var optionsMonitor = services.GetRequiredService<IOptionsMonitor<MessagePackGrainStorageOptions>>();
            var logger = services.GetRequiredService<ILogger<MessagePackGrainStorage>>();
            return new MessagePackGrainStorage(name, optionsMonitor.Get(name), logger);
        }
    }

    /// <summary>
    /// Extension methods for adding MessagePack grain storage to Orleans silo.
    /// </summary>
    public static class MessagePackGrainStorageExtensions
    {
        /// <summary>
        /// Adds MessagePack grain storage provider.
        /// </summary>
        public static ISiloBuilder AddMessagePackGrainStorage(
            this ISiloBuilder builder,
            string name,
            Action<MessagePackGrainStorageOptions>? configureOptions = null)
        {
            var options = new MessagePackGrainStorageOptions();
            configureOptions?.Invoke(options);

            builder.Services.AddKeyedSingleton<IGrainStorage>(name, (sp, key) =>
            {
                var logger = sp.GetRequiredService<ILogger<MessagePackGrainStorage>>();
                return new MessagePackGrainStorage(name, options, logger);
            });

            builder.Services.AddSingleton<ILifecycleParticipant<ISiloLifecycle>>(sp =>
            {
                var storage = sp.GetRequiredKeyedService<IGrainStorage>(name);
                return (ILifecycleParticipant<ISiloLifecycle>)storage;
            });

            return builder;
        }
    }
}
