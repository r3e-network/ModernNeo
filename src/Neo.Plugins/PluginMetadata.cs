// Copyright (C) 2015-2025 The Neo Project.
//
// PluginMetadata.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace Neo.Plugins
{
    /// <summary>
    /// Represents declarative metadata for a plugin, including compatibility and dependency information.
    /// </summary>
    public sealed class PluginMetadata : IPluginMetadata, IEquatable<PluginMetadata>
    {
        /// <summary>
        /// Gets the unique plugin name.
        /// </summary>
        public required string Name { get; init; }

        /// <summary>
        /// Gets the plugin version.
        /// </summary>
        public Version Version { get; init; } = new(1, 0, 0);

        /// <summary>
        /// Gets the plugin description.
        /// </summary>
        public string Description { get; init; } = string.Empty;

        /// <summary>
        /// Gets the list of required plugin dependencies that must be loaded before this plugin.
        /// </summary>
        public IReadOnlyList<PluginDependency> Dependencies { get; init; } = Array.Empty<PluginDependency>();

        /// <summary>
        /// Gets the minimum compatible Neo version. If null, no minimum is enforced.
        /// </summary>
        public Version? MinNeoVersion { get; init; }

        /// <summary>
        /// Gets the maximum compatible Neo version. If null, no maximum is enforced.
        /// </summary>
        public Version? MaxNeoVersion { get; init; }

        /// <summary>
        /// Gets the plugin author information.
        /// </summary>
        public string? Author { get; init; }

        /// <summary>
        /// Gets the plugin license identifier or text.
        /// </summary>
        public string? License { get; init; }

        /// <summary>
        /// Determines whether this plugin is compatible with the specified Neo version based on
        /// <see cref="MinNeoVersion"/> and <see cref="MaxNeoVersion"/>.
        /// </summary>
        /// <param name="neoVersion">The Neo version to validate.</param>
        /// <returns><see langword="true"/> if the plugin is compatible; otherwise, <see langword="false"/>.</returns>
        public bool IsCompatibleWith(Version neoVersion)
        {
            if (neoVersion is null) throw new ArgumentNullException(nameof(neoVersion));
            if (MinNeoVersion is not null && neoVersion < MinNeoVersion) return false;
            if (MaxNeoVersion is not null && neoVersion > MaxNeoVersion) return false;
            return true;
        }

        /// <inheritdoc/>
        public bool Equals(PluginMetadata? other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return string.Equals(Name, other.Name, StringComparison.OrdinalIgnoreCase) && Version.Equals(other.Version);
        }

        /// <inheritdoc/>
        public override bool Equals(object? obj) => obj is PluginMetadata other && Equals(other);

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            return HashCode.Combine(
                StringComparer.OrdinalIgnoreCase.GetHashCode(Name),
                Version);
        }

        /// <summary>
        /// Creates a metadata instance from an attribute.
        /// </summary>
        /// <param name="attribute">The attribute instance.</param>
        /// <returns>The parsed metadata.</returns>
        public static PluginMetadata FromAttribute(PluginMetadataAttribute attribute)
        {
            if (attribute is null) throw new ArgumentNullException(nameof(attribute));

            var dependencies = attribute.Dependencies?
                .Where(static d => !string.IsNullOrWhiteSpace(d))
                .Select(static d => new PluginDependency(d.Trim(), "*"))
                .ToArray()
                ?? Array.Empty<PluginDependency>();

            if (!TryParseVersion(attribute.Version, out var version))
                version = new Version(1, 0, 0);

            TryParseVersion(attribute.MinNeoVersion, out var minNeoVersion);
            TryParseVersion(attribute.MaxNeoVersion, out var maxNeoVersion);

            return new PluginMetadata
            {
                Name = attribute.Name,
                Version = version,
                Description = attribute.Description ?? string.Empty,
                Dependencies = dependencies,
                MinNeoVersion = minNeoVersion,
                MaxNeoVersion = maxNeoVersion,
                Author = attribute.Author,
                License = attribute.License
            };
        }

        internal static bool TryParseVersion(string? value, [NotNullWhen(true)] out Version? version)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                version = null;
                return false;
            }

            if (Version.TryParse(value, out version))
                return true;

            version = null;
            return false;
        }
    }

    /// <summary>
    /// Declares plugin metadata for discovery without instantiating the plugin type.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class PluginMetadataAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PluginMetadataAttribute"/> class.
        /// </summary>
        /// <param name="name">The unique plugin name.</param>
        /// <param name="version">The plugin version.</param>
        public PluginMetadataAttribute(string name, string version)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentNullException(nameof(name), "Plugin name cannot be null or whitespace.");
            if (string.IsNullOrWhiteSpace(version))
                throw new ArgumentNullException(nameof(version), "Plugin version cannot be null or whitespace.");

            Name = name;
            Version = version;
        }

        /// <summary>
        /// Gets the plugin name.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the plugin version string.
        /// </summary>
        public string Version { get; }

        /// <summary>
        /// Gets or sets the plugin description.
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Gets or sets the required plugin names that must be loaded before this plugin.
        /// </summary>
        public string[]? Dependencies { get; set; }

        /// <summary>
        /// Gets or sets the minimum compatible Neo version string.
        /// </summary>
        public string? MinNeoVersion { get; set; }

        /// <summary>
        /// Gets or sets the maximum compatible Neo version string.
        /// </summary>
        public string? MaxNeoVersion { get; set; }

        /// <summary>
        /// Gets or sets the plugin author.
        /// </summary>
        public string? Author { get; set; }

        /// <summary>
        /// Gets or sets the plugin license.
        /// </summary>
        public string? License { get; set; }
    }
}
