// Copyright (C) 2015-2025 The Neo Project.
//
// LevelDbStore.cs file belongs to the neo project and is free
// software distributed under the MIT software license.

using Neo.Extensions;
using Neo.Persistence.Providers.LevelDB;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Neo.Persistence.Providers;

/// <summary>
/// A LevelDB-based <see cref="IStore"/> implementation for production use.
/// </summary>
public class LevelDbStore : IStore
{
    private readonly IntPtr _db;
    private readonly IntPtr _writeOptions;
    private readonly IntPtr _readOptions;
    private bool _disposed;

    /// <inheritdoc/>
    public event IStore.OnNewSnapshotDelegate? OnNewSnapshot;

    /// <summary>
    /// Initializes a new instance of the <see cref="LevelDbStore"/> class.
    /// </summary>
    /// <param name="path">The path to the database directory.</param>
    public LevelDbStore(string path)
    {
        var options = Native.leveldb_options_create();
        try
        {
            Native.leveldb_options_set_create_if_missing(options, 1);
            Native.leveldb_options_set_max_open_files(options, 1000);
            Native.leveldb_options_set_write_buffer_size(options, 4 * 1024 * 1024); // 4MB
            Native.leveldb_options_set_block_size(options, 4096);
            Native.leveldb_options_set_compression(options, 1); // Snappy compression

            _db = Native.leveldb_open(options, path, out var error);
            Native.CheckError(error);
        }
        finally
        {
            Native.leveldb_options_destroy(options);
        }

        _writeOptions = Native.leveldb_writeoptions_create();
        _readOptions = Native.leveldb_readoptions_create();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Delete(byte[] key)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        unsafe
        {
            fixed (byte* keyPtr = key)
            {
                Native.leveldb_delete(_db, _writeOptions, (IntPtr)keyPtr, (nuint)key.Length, out var error);
                Native.CheckError(error);
            }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        Native.leveldb_writeoptions_destroy(_writeOptions);
        Native.leveldb_readoptions_destroy(_readOptions);
        Native.leveldb_close(_db);
        GC.SuppressFinalize(this);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IStoreSnapshot GetSnapshot()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var snapshot = new LevelDbSnapshot(this, _db);
        OnNewSnapshot?.Invoke(this, snapshot);
        return snapshot;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Put(byte[] key, byte[] value)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        unsafe
        {
            fixed (byte* keyPtr = key)
            fixed (byte* valuePtr = value)
            {
                Native.leveldb_put(_db, _writeOptions, (IntPtr)keyPtr, (nuint)key.Length,
                    (IntPtr)valuePtr, (nuint)value.Length, out var error);
                Native.CheckError(error);
            }
        }
    }

    /// <inheritdoc/>
    public IEnumerable<(byte[] Key, byte[] Value)> Find(byte[]? keyOrPrefix, SeekDirection direction = SeekDirection.Forward)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        keyOrPrefix ??= [];
        var iterator = Native.leveldb_create_iterator(_db, _readOptions);

        try
        {
            if (direction == SeekDirection.Forward)
            {
                // Seek to the first key >= keyOrPrefix
                if (keyOrPrefix.Length == 0)
                {
                    Native.leveldb_iter_seek_to_first(iterator);
                }
                else
                {
                    unsafe
                    {
                        fixed (byte* keyPtr = keyOrPrefix)
                        {
                            Native.leveldb_iter_seek(iterator, (IntPtr)keyPtr, (nuint)keyOrPrefix.Length);
                        }
                    }
                }

                while (Native.leveldb_iter_valid(iterator) != 0)
                {
                    var key = GetIteratorKey(iterator);
                    var value = GetIteratorValue(iterator);
                    yield return (key, value);
                    Native.leveldb_iter_next(iterator);
                }
            }
            else
            {
                if (keyOrPrefix.Length == 0)
                    yield break;

                // For backward seek, find the last key <= keyOrPrefix
                unsafe
                {
                    fixed (byte* keyPtr = keyOrPrefix)
                    {
                        Native.leveldb_iter_seek(iterator, (IntPtr)keyPtr, (nuint)keyOrPrefix.Length);
                    }
                }

                // If we're past the end, seek to last
                if (Native.leveldb_iter_valid(iterator) == 0)
                {
                    Native.leveldb_iter_seek_to_last(iterator);
                }
                else
                {
                    // Check if current key is greater than keyOrPrefix
                    var currentKey = GetIteratorKey(iterator);
                    if (ByteArrayComparer.Default.Compare(currentKey, keyOrPrefix) > 0)
                    {
                        // Move back one position
                        Native.leveldb_iter_prev(iterator);
                    }
                }

                while (Native.leveldb_iter_valid(iterator) != 0)
                {
                    var key = GetIteratorKey(iterator);
                    var value = GetIteratorValue(iterator);
                    yield return (key, value);
                    Native.leveldb_iter_prev(iterator);
                }
            }
        }
        finally
        {
            Native.leveldb_iter_destroy(iterator);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte[]? TryGet(byte[] key)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        unsafe
        {
            fixed (byte* keyPtr = key)
            {
                var valuePtr = Native.leveldb_get(_db, _readOptions, (IntPtr)keyPtr,
                    (nuint)key.Length, out var valueLen, out var error);
                Native.CheckError(error);

                if (valuePtr == IntPtr.Zero)
                    return null;

                try
                {
                    var result = new byte[valueLen];
                    Marshal.Copy(valuePtr, result, 0, (int)valueLen);
                    return result;
                }
                finally
                {
                    Native.leveldb_free(valuePtr);
                }
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGet(byte[] key, [NotNullWhen(true)] out byte[]? value)
    {
        value = TryGet(key);
        return value != null;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Contains(byte[] key)
    {
        return TryGet(key) != null;
    }

    private static byte[] GetIteratorKey(IntPtr iterator)
    {
        var keyPtr = Native.leveldb_iter_key(iterator, out var keyLen);
        var key = new byte[keyLen];
        Marshal.Copy(keyPtr, key, 0, (int)keyLen);
        return key;
    }

    private static byte[] GetIteratorValue(IntPtr iterator)
    {
        var valuePtr = Native.leveldb_iter_value(iterator, out var valueLen);
        var value = new byte[valueLen];
        Marshal.Copy(valuePtr, value, 0, (int)valueLen);
        return value;
    }
}

/// <summary>
/// A snapshot of a LevelDB store.
/// </summary>
public class LevelDbSnapshot : IStoreSnapshot
{
    private readonly LevelDbStore _store;
    private readonly IntPtr _db;
    private readonly IntPtr _snapshot;
    private readonly IntPtr _readOptions;
    private readonly IntPtr _batch;
    private bool _disposed;

    public IStore Store => _store;

    internal LevelDbSnapshot(LevelDbStore store, IntPtr db)
    {
        _store = store;
        _db = db;
        _snapshot = Native.leveldb_create_snapshot(db);
        _readOptions = Native.leveldb_readoptions_create();
        Native.leveldb_readoptions_set_snapshot(_readOptions, _snapshot);
        _batch = Native.leveldb_writebatch_create();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Delete(byte[] key)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        unsafe
        {
            fixed (byte* keyPtr = key)
            {
                Native.leveldb_writebatch_delete(_batch, (IntPtr)keyPtr, (nuint)key.Length);
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Put(byte[] key, byte[] value)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        unsafe
        {
            fixed (byte* keyPtr = key)
            fixed (byte* valuePtr = value)
            {
                Native.leveldb_writebatch_put(_batch, (IntPtr)keyPtr, (nuint)key.Length,
                    (IntPtr)valuePtr, (nuint)value.Length);
            }
        }
    }

    public void Commit()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var writeOptions = Native.leveldb_writeoptions_create();
        try
        {
            Native.leveldb_write(_db, writeOptions, _batch, out var error);
            Native.CheckError(error);
        }
        finally
        {
            Native.leveldb_writeoptions_destroy(writeOptions);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        Native.leveldb_writebatch_destroy(_batch);
        Native.leveldb_readoptions_destroy(_readOptions);
        Native.leveldb_release_snapshot(_db, _snapshot);
        GC.SuppressFinalize(this);
    }

    public IEnumerable<(byte[] Key, byte[] Value)> Find(byte[]? keyOrPrefix, SeekDirection direction = SeekDirection.Forward)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        keyOrPrefix ??= [];
        var iterator = Native.leveldb_create_iterator(_db, _readOptions);

        try
        {
            if (direction == SeekDirection.Forward)
            {
                // Seek to the first key >= keyOrPrefix
                if (keyOrPrefix.Length == 0)
                {
                    Native.leveldb_iter_seek_to_first(iterator);
                }
                else
                {
                    unsafe
                    {
                        fixed (byte* keyPtr = keyOrPrefix)
                        {
                            Native.leveldb_iter_seek(iterator, (IntPtr)keyPtr, (nuint)keyOrPrefix.Length);
                        }
                    }
                }

                while (Native.leveldb_iter_valid(iterator) != 0)
                {
                    var key = GetIteratorKey(iterator);
                    var value = GetIteratorValue(iterator);
                    yield return (key, value);
                    Native.leveldb_iter_next(iterator);
                }
            }
            else
            {
                if (keyOrPrefix.Length == 0)
                    yield break;

                // For backward seek, find the last key <= keyOrPrefix
                unsafe
                {
                    fixed (byte* keyPtr = keyOrPrefix)
                    {
                        Native.leveldb_iter_seek(iterator, (IntPtr)keyPtr, (nuint)keyOrPrefix.Length);
                    }
                }

                // If we're past the end, seek to last
                if (Native.leveldb_iter_valid(iterator) == 0)
                {
                    Native.leveldb_iter_seek_to_last(iterator);
                }
                else
                {
                    // Check if current key is greater than keyOrPrefix
                    var currentKey = GetIteratorKey(iterator);
                    if (ByteArrayComparer.Default.Compare(currentKey, keyOrPrefix) > 0)
                    {
                        // Move back one position
                        Native.leveldb_iter_prev(iterator);
                    }
                }

                while (Native.leveldb_iter_valid(iterator) != 0)
                {
                    var key = GetIteratorKey(iterator);
                    var value = GetIteratorValue(iterator);
                    yield return (key, value);
                    Native.leveldb_iter_prev(iterator);
                }
            }
        }
        finally
        {
            Native.leveldb_iter_destroy(iterator);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte[]? TryGet(byte[] key)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        unsafe
        {
            fixed (byte* keyPtr = key)
            {
                var valuePtr = Native.leveldb_get(_db, _readOptions, (IntPtr)keyPtr,
                    (nuint)key.Length, out var valueLen, out var error);
                Native.CheckError(error);

                if (valuePtr == IntPtr.Zero)
                    return null;

                try
                {
                    var result = new byte[valueLen];
                    Marshal.Copy(valuePtr, result, 0, (int)valueLen);
                    return result;
                }
                finally
                {
                    Native.leveldb_free(valuePtr);
                }
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGet(byte[] key, [NotNullWhen(true)] out byte[]? value)
    {
        value = TryGet(key);
        return value != null;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Contains(byte[] key)
    {
        return TryGet(key) != null;
    }

    private static byte[] GetIteratorKey(IntPtr iterator)
    {
        var keyPtr = Native.leveldb_iter_key(iterator, out var keyLen);
        var key = new byte[keyLen];
        Marshal.Copy(keyPtr, key, 0, (int)keyLen);
        return key;
    }

    private static byte[] GetIteratorValue(IntPtr iterator)
    {
        var valuePtr = Native.leveldb_iter_value(iterator, out var valueLen);
        var value = new byte[valueLen];
        Marshal.Copy(valuePtr, value, 0, (int)valueLen);
        return value;
    }
}
