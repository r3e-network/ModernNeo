using System.Collections.Concurrent;
using Orleans.Runtime;
using Orleans.Storage;

namespace Neo.Orleans.Storage;

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
