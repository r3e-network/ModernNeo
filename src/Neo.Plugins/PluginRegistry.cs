// Copyright (C) 2015-2025 The Neo Project.
//
// PluginRegistry.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Neo.Plugins
{
    /// <summary>
    /// Thread-safe plugin registry/catalog for discovered and/or loaded plugins.
    /// Supports registration, lookup, and search by name, version, and tags.
    /// </summary>
    public sealed class PluginRegistry
    {
        private readonly ConcurrentDictionary<string, ConcurrentDictionary<Version, PluginCatalogEntry>> _byName =
            new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Registers a plugin metadata entry (and optional instance) with optional tags.
        /// Adds or replaces the same name+version entry atomically.
        /// </summary>
        /// <param name="metadata">Plugin metadata.</param>
        /// <param name="instance">Optional plugin instance.</param>
        /// <param name="tags">Optional tags to associate.</param>
        /// <returns><c>true</c> if the entry was added or replaced; otherwise <c>false</c>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="metadata"/> is null.</exception>
        public bool Register(IPluginMetadata metadata, IPlugin? instance = null, IEnumerable<string>? tags = null)
        {
            if (metadata is null) throw new ArgumentNullException(nameof(metadata));

            var versionMap = _byName.GetOrAdd(metadata.Name, static _ => new ConcurrentDictionary<Version, PluginCatalogEntry>());
            var entry = new PluginCatalogEntry(metadata, instance, tags);
            versionMap[metadata.Version] = entry;
            return true;
        }

        /// <summary>
        /// Unregisters a plugin by name and optional version. If version is not specified,
        /// all versions of the plugin are removed.
        /// </summary>
        /// <param name="name">Plugin name.</param>
        /// <param name="version">Specific version to remove, or <c>null</c> to remove all versions.</param>
        /// <returns><c>true</c> if at least one entry was removed; otherwise <c>false</c>.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is null or whitespace.</exception>
        public bool Unregister(string name, Version? version = null)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Plugin name cannot be null or empty.", nameof(name));

            if (!_byName.TryGetValue(name, out var versionMap))
                return false;

            if (version is null)
            {
                return _byName.TryRemove(name, out _);
            }

            var removed = versionMap.TryRemove(version, out _);
            if (versionMap.IsEmpty)
            {
                _byName.TryRemove(name, out _);
            }
            return removed;
        }

        /// <summary>
        /// Attempts to get the highest version entry for a given plugin name.
        /// </summary>
        /// <param name="name">Plugin name.</param>
        /// <param name="entry">The resulting catalog entry, if found.</param>
        /// <returns><c>true</c> if found; otherwise <c>false</c>.</returns>
        public bool TryGet(string name, out PluginCatalogEntry? entry)
        {
            entry = null;
            if (string.IsNullOrWhiteSpace(name)) return false;
            if (!_byName.TryGetValue(name, out var versionMap) || versionMap.IsEmpty) return false;

            var kv = versionMap.OrderByDescending(k => k.Key).FirstOrDefault();
            if (kv.Key is null) return false;
            entry = kv.Value;
            return true;
        }

        /// <summary>
        /// Attempts to get an entry for a specific plugin version.
        /// </summary>
        /// <param name="name">Plugin name.</param>
        /// <param name="version">Exact version.</param>
        /// <param name="entry">The resulting catalog entry, if found.</param>
        /// <returns><c>true</c> if found; otherwise <c>false</c>.</returns>
        public bool TryGet(string name, Version version, out PluginCatalogEntry? entry)
        {
            entry = null;
            if (string.IsNullOrWhiteSpace(name)) return false;
            if (!_byName.TryGetValue(name, out var versionMap)) return false;
            if (!versionMap.TryGetValue(version, out var result)) return false;
            entry = result;
            return true;
        }

        /// <summary>
        /// Attempts to get the highest version entry that satisfies the specified version range.
        /// </summary>
        /// <param name="name">Plugin name.</param>
        /// <param name="versionRange">Version range expression (SemVer operators or NuGet-style brackets).</param>
        /// <param name="entry">The resulting catalog entry, if found.</param>
        /// <returns><c>true</c> if found; otherwise <c>false</c>.</returns>
        public bool TryGet(string name, string versionRange, out PluginCatalogEntry? entry)
        {
            entry = null;
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(versionRange)) return false;
            if (!_byName.TryGetValue(name, out var versionMap) || versionMap.IsEmpty) return false;

            var range = PluginVersionResolver.ParseVersionRange(versionRange);
            var match = versionMap.Keys
                .Where(range.Matches)
                .OrderByDescending(v => v)
                .FirstOrDefault();

            if (match is null) return false;
            entry = versionMap[match];
            return true;
        }

        /// <summary>
        /// Gets all catalog entries currently registered.
        /// </summary>
        public IEnumerable<PluginCatalogEntry> GetAll()
        {
            foreach (var (_, versions) in _byName)
            {
                foreach (var (_, entry) in versions)
                    yield return entry;
            }
        }

        /// <summary>
        /// Finds entries that match optional filters for name (substring), version range, and tags.
        /// </summary>
        /// <param name="name">Optional name or substring (case-insensitive). If contains '*', performs wildcard match.</param>
        /// <param name="versionRange">Optional version range expression.</param>
        /// <param name="tags">Optional set of tags that must all be present on the entry.</param>
        /// <returns>Enumerable of matching catalog entries.</returns>
        public IEnumerable<PluginCatalogEntry> Find(string? name = null, string? versionRange = null, IEnumerable<string>? tags = null)
        {
            var tagSet = tags is null ? null : new HashSet<string>(tags, StringComparer.OrdinalIgnoreCase);
            var hasWildcard = !string.IsNullOrEmpty(name) && name!.Contains('*', StringComparison.Ordinal);
            Func<string, bool> namePredicate = n =>
            {
                if (string.IsNullOrEmpty(name)) return true;
                if (hasWildcard)
                {
                    // Simple wildcard: only '*' supported, translates to "contains" segments in order.
                    var parts = name!.Split('*', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    var idx = 0;
                    var span = n.AsSpan();
                    foreach (var part in parts)
                    {
                        var pos = span[idx..].ToString().IndexOf(part, StringComparison.OrdinalIgnoreCase);
                        if (pos < 0) return false;
                        idx += pos + part.Length;
                    }
                    return true;
                }
                return n.Contains(name!, StringComparison.OrdinalIgnoreCase);
            };

            VersionRange? range = null;
            if (!string.IsNullOrWhiteSpace(versionRange))
            {
                range = PluginVersionResolver.ParseVersionRange(versionRange!);
            }

            foreach (var (pluginName, versions) in _byName)
            {
                if (!namePredicate(pluginName)) continue;

                foreach (var (version, entry) in versions)
                {
                    if (range is not null && !range.Matches(version)) continue;
                    if (tagSet is not null && !tagSet.IsSubsetOf(entry.Tags)) continue;
                    yield return entry;
                }
            }
        }
    }

    /// <summary>
    /// Represents a registered plugin entry within the catalog.
    /// </summary>
    public sealed class PluginCatalogEntry
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PluginCatalogEntry"/> class.
        /// </summary>
        /// <param name="metadata">Plugin metadata.</param>
        /// <param name="instance">Optional plugin instance.</param>
        /// <param name="tags">Optional tags.</param>
        public PluginCatalogEntry(IPluginMetadata metadata, IPlugin? instance, IEnumerable<string>? tags)
        {
            Metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
            Instance = instance;
            Tags = tags is null
                ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                : new HashSet<string>(tags, StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Gets the plugin metadata.
        /// </summary>
        public IPluginMetadata Metadata { get; }

        /// <summary>
        /// Gets the current plugin instance if loaded, otherwise <c>null</c>.
        /// </summary>
        public IPlugin? Instance { get; }

        /// <summary>
        /// Gets the associated tag set (case-insensitive).
        /// </summary>
        public ISet<string> Tags { get; }
    }
}
