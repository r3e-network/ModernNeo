// Copyright (C) 2015-2025 The Neo Project.
//
// StoreFactory.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Persistence.Providers;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Neo.Persistence
{
    public static class StoreFactory
    {
        private static readonly Dictionary<string, IStoreProvider> s_providers = new(StringComparer.OrdinalIgnoreCase);

        static StoreFactory()
        {
            var memProvider = new MemoryStoreProvider();
            RegisterProvider(memProvider);

            var levelDbProvider = new LevelDbStoreProvider();
            RegisterProvider(levelDbProvider);

            var rocksDbProvider = new RocksDbStoreProvider();
            RegisterProvider(rocksDbProvider);

            // Default cases
            s_providers.Add("", memProvider);
            s_providers.TryAdd("Memory", memProvider);
            s_providers.TryAdd("LevelDB", levelDbProvider);
            s_providers.TryAdd("RocksDB", rocksDbProvider);
        }

        public static void RegisterProvider(IStoreProvider provider)
        {
            s_providers.Add(provider.Name, provider);
        }

        /// <summary>
        /// Get store provider by name
        /// </summary>
        /// <param name="name">Name</param>
        /// <returns>Store provider</returns>
        public static IStoreProvider? GetStoreProvider(string name)
        {
            if (s_providers.TryGetValue(name, out var provider))
            {
                return provider;
            }

            return null;
        }

        /// <summary>
        /// Gets the registered provider names (excluding the default empty key).
        /// </summary>
        public static IReadOnlyCollection<string> GetProviderNames()
        {
            return s_providers.Keys.Where(name => !string.IsNullOrWhiteSpace(name)).ToArray();
        }

        /// <summary>
        /// Get store from name
        /// </summary>
        /// <param name="storageProvider">
        /// The storage engine used to create the <see cref="IStore"/> objects.
        /// If this parameter is <see langword="null"/>, a default in-memory storage engine will be used.
        /// </param>
        /// <param name="path">
        /// The path of the storage.
        /// If <paramref name="storageProvider"/> is the default in-memory storage engine, this parameter is ignored.
        /// </param>
        /// <returns>The storage engine.</returns>
        public static IStore GetStore(string storageProvider, string path)
        {
            return s_providers[storageProvider].GetStore(path);
        }
    }
}
