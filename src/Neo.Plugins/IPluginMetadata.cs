// Copyright (C) 2015-2025 The Neo Project.
//
// IPluginMetadata.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using System;
using System.Collections.Generic;

namespace Neo.Plugins
{
    /// <summary>
    /// Describes metadata for a plugin, including its identity, version, description,
    /// declared dependencies, and supported Neo runtime versions.
    /// </summary>
    public interface IPluginMetadata
    {
        /// <summary>
        /// Gets the unique name of the plugin.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Gets the semantic version of the plugin.
        /// </summary>
        Version Version { get; }

        /// <summary>
        /// Gets the human-readable description of the plugin.
        /// </summary>
        string Description { get; }

        /// <summary>
        /// Gets the list of declared plugin dependencies with version constraints.
        /// </summary>
        IReadOnlyList<PluginDependency> Dependencies { get; }

        /// <summary>
        /// Gets the minimum supported Neo runtime version, or <c>null</c> if unbounded.
        /// </summary>
        Version? MinNeoVersion { get; }

        /// <summary>
        /// Gets the maximum supported Neo runtime version, or <c>null</c> if unbounded.
        /// </summary>
        Version? MaxNeoVersion { get; }
    }

    /// <summary>
    /// Represents a dependency on another plugin along with an acceptable version range.
    /// </summary>
    public sealed record class PluginDependency
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PluginDependency"/> class.
        /// </summary>
        /// <param name="name">The dependent plugin name.</param>
        /// <param name="versionRange">The version range expression for the dependency.</param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> or <paramref name="versionRange"/> is null or empty.</exception>
        public PluginDependency(string name, string versionRange)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Dependency name cannot be null or empty.", nameof(name));
            if (string.IsNullOrWhiteSpace(versionRange))
                throw new ArgumentException("Dependency version range cannot be null or empty.", nameof(versionRange));

            Name = name;
            VersionRange = versionRange;
        }

        /// <summary>
        /// Gets the dependent plugin name.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the version range expression for the dependency (e.g., "&gt;=1.0.0 &lt;2.0.0").
        /// </summary>
        public string VersionRange { get; }
    }
}
