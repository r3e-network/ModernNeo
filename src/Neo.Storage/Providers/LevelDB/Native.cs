// Copyright (C) 2015-2025 The Neo Project.
//
// Native.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using System;
using System.Runtime.InteropServices;

namespace Neo.Persistence.Providers.LevelDB
{
    /// <summary>
    /// P/Invoke bindings for native LevelDB library.
    /// </summary>
    internal static partial class Native
    {
        private const string LevelDbLib = "libleveldb";

        // Database operations
        [LibraryImport(LevelDbLib, StringMarshalling = StringMarshalling.Utf8)]
        internal static partial IntPtr leveldb_open(IntPtr options, string name, out IntPtr error);

        [LibraryImport(LevelDbLib)]
        internal static partial void leveldb_close(IntPtr db);

        [LibraryImport(LevelDbLib)]
        internal static partial void leveldb_put(IntPtr db, IntPtr writeOptions, IntPtr key, nuint keyLen, IntPtr value, nuint valueLen, out IntPtr error);

        [LibraryImport(LevelDbLib)]
        internal static partial IntPtr leveldb_get(IntPtr db, IntPtr readOptions, IntPtr key, nuint keyLen, out nuint valueLen, out IntPtr error);

        [LibraryImport(LevelDbLib)]
        internal static partial void leveldb_delete(IntPtr db, IntPtr writeOptions, IntPtr key, nuint keyLen, out IntPtr error);

        // Options
        [LibraryImport(LevelDbLib)]
        internal static partial IntPtr leveldb_options_create();

        [LibraryImport(LevelDbLib)]
        internal static partial void leveldb_options_destroy(IntPtr options);

        [LibraryImport(LevelDbLib)]
        internal static partial void leveldb_options_set_create_if_missing(IntPtr options, byte value);

        [LibraryImport(LevelDbLib)]
        internal static partial void leveldb_options_set_max_open_files(IntPtr options, int value);

        [LibraryImport(LevelDbLib)]
        internal static partial void leveldb_options_set_write_buffer_size(IntPtr options, nuint value);

        [LibraryImport(LevelDbLib)]
        internal static partial void leveldb_options_set_block_size(IntPtr options, nuint value);

        [LibraryImport(LevelDbLib)]
        internal static partial void leveldb_options_set_compression(IntPtr options, int value);

        // Read options
        [LibraryImport(LevelDbLib)]
        internal static partial IntPtr leveldb_readoptions_create();

        [LibraryImport(LevelDbLib)]
        internal static partial void leveldb_readoptions_destroy(IntPtr options);

        [LibraryImport(LevelDbLib)]
        internal static partial void leveldb_readoptions_set_snapshot(IntPtr options, IntPtr snapshot);

        // Write options
        [LibraryImport(LevelDbLib)]
        internal static partial IntPtr leveldb_writeoptions_create();

        [LibraryImport(LevelDbLib)]
        internal static partial void leveldb_writeoptions_destroy(IntPtr options);

        [LibraryImport(LevelDbLib)]
        internal static partial void leveldb_writeoptions_set_sync(IntPtr options, byte value);

        // Iterator
        [LibraryImport(LevelDbLib)]
        internal static partial IntPtr leveldb_create_iterator(IntPtr db, IntPtr readOptions);

        [LibraryImport(LevelDbLib)]
        internal static partial void leveldb_iter_destroy(IntPtr iterator);

        [LibraryImport(LevelDbLib)]
        internal static partial byte leveldb_iter_valid(IntPtr iterator);

        [LibraryImport(LevelDbLib)]
        internal static partial void leveldb_iter_seek_to_first(IntPtr iterator);

        [LibraryImport(LevelDbLib)]
        internal static partial void leveldb_iter_seek_to_last(IntPtr iterator);

        [LibraryImport(LevelDbLib)]
        internal static partial void leveldb_iter_seek(IntPtr iterator, IntPtr key, nuint keyLen);

        [LibraryImport(LevelDbLib)]
        internal static partial void leveldb_iter_next(IntPtr iterator);

        [LibraryImport(LevelDbLib)]
        internal static partial void leveldb_iter_prev(IntPtr iterator);

        [LibraryImport(LevelDbLib)]
        internal static partial IntPtr leveldb_iter_key(IntPtr iterator, out nuint keyLen);

        [LibraryImport(LevelDbLib)]
        internal static partial IntPtr leveldb_iter_value(IntPtr iterator, out nuint valueLen);

        // Snapshot
        [LibraryImport(LevelDbLib)]
        internal static partial IntPtr leveldb_create_snapshot(IntPtr db);

        [LibraryImport(LevelDbLib)]
        internal static partial void leveldb_release_snapshot(IntPtr db, IntPtr snapshot);

        // WriteBatch
        [LibraryImport(LevelDbLib)]
        internal static partial IntPtr leveldb_writebatch_create();

        [LibraryImport(LevelDbLib)]
        internal static partial void leveldb_writebatch_destroy(IntPtr batch);

        [LibraryImport(LevelDbLib)]
        internal static partial void leveldb_writebatch_clear(IntPtr batch);

        [LibraryImport(LevelDbLib)]
        internal static partial void leveldb_writebatch_put(IntPtr batch, IntPtr key, nuint keyLen, IntPtr value, nuint valueLen);

        [LibraryImport(LevelDbLib)]
        internal static partial void leveldb_writebatch_delete(IntPtr batch, IntPtr key, nuint keyLen);

        [LibraryImport(LevelDbLib)]
        internal static partial void leveldb_write(IntPtr db, IntPtr writeOptions, IntPtr batch, out IntPtr error);

        // Memory management
        [LibraryImport(LevelDbLib)]
        internal static partial void leveldb_free(IntPtr ptr);

        // Helper methods
        internal static void CheckError(IntPtr error)
        {
            if (error != IntPtr.Zero)
            {
                var message = Marshal.PtrToStringUTF8(error);
                leveldb_free(error);
                throw new LevelDbException(message ?? "Unknown LevelDB error");
            }
        }
    }

    /// <summary>
    /// Exception thrown by LevelDB operations.
    /// </summary>
    public class LevelDbException : Exception
    {
        public LevelDbException(string message) : base(message) { }
    }
}
