// Copyright (C) 2015-2025 The Neo Project.
//
// PluginAttribute.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using System;

namespace Neo.Plugins
{
    /// <summary>
    /// Declares assembly-level metadata for a Neo plugin.
    /// Use at the assembly level to enable discovery via reflection.
    /// </summary>
    /// <remarks>
    /// Example:
    /// <code>
    /// [assembly: Plugin("sample-plugin", "1.0.0", Description = "Sample plugin")]
    /// [assembly: PluginDependency("other-plugin", ">=1.0.0")]
    /// </code>
    /// </remarks>
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false)]
    public sealed class PluginAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PluginAttribute"/> class.
        /// </summary>
        /// <param name="name">Unique plugin name.</param>
        /// <param name="version">Semantic version (e.g., "1.2.3").</param>
        /// <exception cref="ArgumentException">Thrown when arguments are null or whitespace.</exception>
        public PluginAttribute(string name, string version)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Plugin name cannot be null or empty.", nameof(name));
            if (string.IsNullOrWhiteSpace(version))
                throw new ArgumentException("Plugin version cannot be null or empty.", nameof(version));

            Name = name;
            Version = version;
        }

        /// <summary>
        /// Gets the unique plugin name.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the declared plugin version string.
        /// </summary>
        public string Version { get; }

        /// <summary>
        /// Gets or sets the optional human-readable description.
        /// </summary>
        public string Description { get; init; } = string.Empty;

        /// <summary>
        /// Gets or sets the minimum supported Neo runtime version (string form).
        /// Optional.
        /// </summary>
        public string? MinNeoVersion { get; init; }

        /// <summary>
        /// Gets or sets the maximum supported Neo runtime version (string form).
        /// Optional.
        /// </summary>
        public string? MaxNeoVersion { get; init; }
    }

    /// <summary>
    /// Declares a dependency on another plugin for the current assembly.
    /// Multiple attributes can be specified.
    /// </summary>
    /// <remarks>
    /// Example:
    /// <code>
    /// [assembly: PluginDependency("neo-logger", "&gt;=2.0.0 &lt;3.0.0")]
    /// </code>
    /// </remarks>
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
    public sealed class PluginDependencyAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PluginDependencyAttribute"/> class.
        /// </summary>
        /// <param name="name">The dependent plugin name.</param>
        /// <param name="versionRange">The semantic version range expression.</param>
        /// <exception cref="ArgumentException">Thrown when arguments are null or whitespace.</exception>
        public PluginDependencyAttribute(string name, string versionRange)
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
        /// Gets the semantic version range expression for the dependency.
        /// </summary>
        public string VersionRange { get; }
    }
}
