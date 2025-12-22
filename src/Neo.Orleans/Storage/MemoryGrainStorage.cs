// Copyright (C) 2015-2025 The Neo Project.
//
// MemoryGrainStorage.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Orleans.Runtime;
using Orleans.Storage;
using System.Collections.Concurrent;

namespace Neo.Orleans.Storage
{
    /// <summary>
    /// In-memory grain storage provider for development and testing.
    /// Production should use RocksDB-backed storage.
    /// </summary>
    public class MemoryGrainStorage : IGrainStorage
    {
        private readonly ConcurrentDictionary<string, byte[]> _storage = new();

        public Task ReadStateAsync<T>(string stateName, GrainId grainId, IGrainState<T> grainState)
        {
            var key = GetKey(stateName, grainId);
            if (_storage.TryGetValue(key, out var data))
            {
                grainState.State = MessagePack.MessagePackSerializer.Deserialize<T>(data);
                grainState.ETag = ComputeETag(data);
                grainState.RecordExists = true;
            }
            else
            {
                grainState.RecordExists = false;
            }
            return Task.CompletedTask;
        }

        public Task WriteStateAsync<T>(string stateName, GrainId grainId, IGrainState<T> grainState)
        {
            var key = GetKey(stateName, grainId);
            var data = MessagePack.MessagePackSerializer.Serialize(grainState.State);
            _storage[key] = data;
            grainState.ETag = ComputeETag(data);
            grainState.RecordExists = true;
            return Task.CompletedTask;
        }

        public Task ClearStateAsync<T>(string stateName, GrainId grainId, IGrainState<T> grainState)
        {
            var key = GetKey(stateName, grainId);
            _storage.TryRemove(key, out _);
            grainState.RecordExists = false;
            grainState.ETag = null;
            return Task.CompletedTask;
        }

        private static string GetKey(string stateName, GrainId grainId) =>
            $"{grainId.Type}/{grainId.Key}/{stateName}";

        private static string ComputeETag(byte[] data) =>
            Convert.ToBase64String(System.Security.Cryptography.SHA256.HashData(data)[..8]);
    }
}
