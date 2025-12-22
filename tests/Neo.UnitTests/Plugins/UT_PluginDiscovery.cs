// Copyright (C) 2015-2025 The Neo Project.
//
// UT_PluginDiscovery.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

#nullable enable

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.Plugins;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Neo.UnitTests.Plugins
{
    [TestClass]
    public class UT_PluginDiscovery
    {
        #region IPluginMetadata & PluginDependency Tests

        [TestMethod]
        public void PluginDependency_Constructor_ValidInputs_ShouldSucceed()
        {
            // Arrange & Act
            var dependency = new PluginDependency("test-plugin", ">=1.0.0");

            // Assert
            Assert.AreEqual("test-plugin", dependency.Name);
            Assert.AreEqual(">=1.0.0", dependency.VersionRange);
        }

        [TestMethod]
        [DataRow(null, ">=1.0.0")]
        [DataRow("", ">=1.0.0")]
        [DataRow("  ", ">=1.0.0")]
        [DataRow("test", null)]
        [DataRow("test", "")]
        [DataRow("test", "  ")]
        public void PluginDependency_Constructor_InvalidInputs_ShouldThrow(string name, string versionRange)
        {
            // Act & Assert
            Assert.ThrowsExactly<ArgumentException>(() => new PluginDependency(name, versionRange));
        }

        [TestMethod]
        public void PluginDependency_RecordEquality_ShouldWork()
        {
            // Arrange
            var dep1 = new PluginDependency("plugin-a", ">=1.0.0");
            var dep2 = new PluginDependency("plugin-a", ">=1.0.0");
            var dep3 = new PluginDependency("plugin-b", ">=1.0.0");

            // Assert
            Assert.AreEqual(dep1, dep2);
            Assert.AreNotEqual(dep1, dep3);
        }

        #endregion

        #region PluginAttribute Tests

        [TestMethod]
        public void PluginAttribute_Constructor_ValidInputs_ShouldSucceed()
        {
            // Arrange & Act
            var attr = new PluginAttribute("my-plugin", "1.2.3")
            {
                Description = "Test plugin",
                MinNeoVersion = "3.0.0",
                MaxNeoVersion = "4.0.0"
            };

            // Assert
            Assert.AreEqual("my-plugin", attr.Name);
            Assert.AreEqual("1.2.3", attr.Version);
            Assert.AreEqual("Test plugin", attr.Description);
            Assert.AreEqual("3.0.0", attr.MinNeoVersion);
            Assert.AreEqual("4.0.0", attr.MaxNeoVersion);
        }

        [TestMethod]
        [DataRow(null, "1.0.0")]
        [DataRow("", "1.0.0")]
        [DataRow("  ", "1.0.0")]
        [DataRow("plugin", null)]
        [DataRow("plugin", "")]
        [DataRow("plugin", "  ")]
        public void PluginAttribute_Constructor_InvalidInputs_ShouldThrow(string name, string version)
        {
            // Act & Assert
            Assert.ThrowsExactly<ArgumentException>(() => new PluginAttribute(name, version));
        }

        [TestMethod]
        public void PluginDependencyAttribute_Constructor_ValidInputs_ShouldSucceed()
        {
            // Arrange & Act
            var attr = new PluginDependencyAttribute("dep-plugin", "^1.0.0");

            // Assert
            Assert.AreEqual("dep-plugin", attr.Name);
            Assert.AreEqual("^1.0.0", attr.VersionRange);
        }

        [TestMethod]
        [DataRow(null, ">=1.0.0")]
        [DataRow("", ">=1.0.0")]
        [DataRow("plugin", null)]
        [DataRow("plugin", "")]
        public void PluginDependencyAttribute_Constructor_InvalidInputs_ShouldThrow(string name, string versionRange)
        {
            // Act & Assert
            Assert.ThrowsExactly<ArgumentException>(() => new PluginDependencyAttribute(name, versionRange));
        }

        #endregion

        #region PluginVersionResolver Tests

        [TestMethod]
        [DataRow("1.0.0", "1.0.0", true)]
        [DataRow("1.0.0", "=1.0.0", true)]
        [DataRow("1.0.0", "==1.0.0", true)]
        [DataRow("1.0.1", "1.0.0", false)]
        [DataRow("2.0.0", ">=1.0.0", true)]
        [DataRow("0.9.0", ">=1.0.0", false)]
        [DataRow("1.5.0", "<=2.0.0", true)]
        [DataRow("2.5.0", "<=2.0.0", false)]
        [DataRow("1.5.0", ">1.0.0", true)]
        [DataRow("1.0.0", ">1.0.0", false)]
        [DataRow("1.5.0", "<2.0.0", true)]
        [DataRow("2.0.0", "<2.0.0", false)]
        public void PluginVersionResolver_IsCompatible_SimpleOperators_ShouldWork(string version, string range, bool expected)
        {
            // Arrange
            var v = new Version(version);

            // Act
            var result = PluginVersionResolver.IsCompatible(v, range);

            // Assert
            Assert.AreEqual(expected, result);
        }

        [TestMethod]
        [DataRow("1.5.0", ">=1.0.0 <2.0.0", true)]
        [DataRow("0.9.0", ">=1.0.0 <2.0.0", false)]
        [DataRow("2.0.0", ">=1.0.0 <2.0.0", false)]
        [DataRow("1.0.0", ">=1.0.0 <=2.0.0", true)]
        [DataRow("2.0.0", ">=1.0.0 <=2.0.0", true)]
        [DataRow("2.0.1", ">=1.0.0 <=2.0.0", false)]
        public void PluginVersionResolver_IsCompatible_CompoundRanges_ShouldWork(string version, string range, bool expected)
        {
            // Arrange
            var v = new Version(version);

            // Act
            var result = PluginVersionResolver.IsCompatible(v, range);

            // Assert
            Assert.AreEqual(expected, result);
        }

        [TestMethod]
        [DataRow("1.2.3", "^1.2.3", true)]
        [DataRow("1.5.0", "^1.2.3", true)]
        [DataRow("1.9.9", "^1.2.3", true)]
        [DataRow("2.0.0", "^1.2.3", false)]
        [DataRow("0.2.3", "^0.2.3", true)]
        [DataRow("0.2.9", "^0.2.3", true)]
        [DataRow("0.3.0", "^0.2.3", false)]
        [DataRow("0.0.3", "^0.0.3", true)]
        [DataRow("0.0.4", "^0.0.3", false)]
        public void PluginVersionResolver_IsCompatible_CaretRanges_ShouldWork(string version, string range, bool expected)
        {
            // Arrange
            var v = new Version(version);

            // Act
            var result = PluginVersionResolver.IsCompatible(v, range);

            // Assert
            Assert.AreEqual(expected, result);
        }

        [TestMethod]
        [DataRow("1.2.3", "~1.2.3", true)]
        [DataRow("1.2.9", "~1.2.3", true)]
        [DataRow("1.3.0", "~1.2.3", false)]
        [DataRow("1.2.0", "~1.2", true)]
        [DataRow("1.2.9", "~1.2", true)]
        [DataRow("1.3.0", "~1.2", false)]
        [DataRow("1.5.0", "~1", true)]
        [DataRow("1.9.9", "~1", true)]
        [DataRow("2.0.0", "~1", false)]
        public void PluginVersionResolver_IsCompatible_TildeRanges_ShouldWork(string version, string range, bool expected)
        {
            // Arrange
            var v = new Version(version);

            // Act
            var result = PluginVersionResolver.IsCompatible(v, range);

            // Assert
            Assert.AreEqual(expected, result);
        }

        [TestMethod]
        [DataRow("1.0.0", "1.*", true)]
        [DataRow("1.5.0", "1.*", true)]
        [DataRow("1.9.9", "1.*", true)]
        [DataRow("2.0.0", "1.*", false)]
        [DataRow("1.2.0", "1.2.*", true)]
        [DataRow("1.2.9", "1.2.*", true)]
        [DataRow("1.3.0", "1.2.*", false)]
        [DataRow("1.0.0", "*", true)]
        [DataRow("99.99.99", "*", true)]
        public void PluginVersionResolver_IsCompatible_WildcardRanges_ShouldWork(string version, string range, bool expected)
        {
            // Arrange
            var v = new Version(version);

            // Act
            var result = PluginVersionResolver.IsCompatible(v, range);

            // Assert
            Assert.AreEqual(expected, result);
        }

        [TestMethod]
        [DataRow("1.0.0", "[1.0.0,2.0.0)", true)]
        [DataRow("1.5.0", "[1.0.0,2.0.0)", true)]
        [DataRow("2.0.0", "[1.0.0,2.0.0)", false)]
        [DataRow("1.0.0", "(1.0.0,2.0.0]", false)]
        [DataRow("1.5.0", "(1.0.0,2.0.0]", true)]
        [DataRow("2.0.0", "(1.0.0,2.0.0]", true)]
        [DataRow("1.0.0", "[1.0.0,2.0.0]", true)]
        [DataRow("2.0.0", "[1.0.0,2.0.0]", true)]
        [DataRow("1.0.0", "(1.0.0,2.0.0)", false)]
        [DataRow("2.0.0", "(1.0.0,2.0.0)", false)]
        [DataRow("1.5.0", "(1.0.0,2.0.0)", true)]
        public void PluginVersionResolver_IsCompatible_NuGetBracketRanges_ShouldWork(string version, string range, bool expected)
        {
            // Arrange
            var v = new Version(version);

            // Act
            var result = PluginVersionResolver.IsCompatible(v, range);

            // Assert
            Assert.AreEqual(expected, result);
        }

        [TestMethod]
        [DataRow("1.5.0", "1.0.0 - 2.0.0", true)]
        [DataRow("1.0.0", "1.0.0 - 2.0.0", true)]
        [DataRow("2.0.0", "1.0.0 - 2.0.0", true)]
        [DataRow("0.9.0", "1.0.0 - 2.0.0", false)]
        [DataRow("2.0.1", "1.0.0 - 2.0.0", false)]
        public void PluginVersionResolver_IsCompatible_HyphenRanges_ShouldWork(string version, string range, bool expected)
        {
            // Arrange
            var v = new Version(version);

            // Act
            var result = PluginVersionResolver.IsCompatible(v, range);

            // Assert
            Assert.AreEqual(expected, result);
        }

        [TestMethod]
        [DataRow("1.5.0", ">=1.0.0 <2.0.0 || >=3.0.0 <4.0.0", true)]
        [DataRow("3.5.0", ">=1.0.0 <2.0.0 || >=3.0.0 <4.0.0", true)]
        [DataRow("2.5.0", ">=1.0.0 <2.0.0 || >=3.0.0 <4.0.0", false)]
        [DataRow("1.0.0", "1.* || 2.*", true)]
        [DataRow("2.5.0", "1.* || 2.*", true)]
        [DataRow("3.0.0", "1.* || 2.*", false)]
        public void PluginVersionResolver_IsCompatible_OrRanges_ShouldWork(string version, string range, bool expected)
        {
            // Arrange
            var v = new Version(version);

            // Act
            var result = PluginVersionResolver.IsCompatible(v, range);

            // Assert
            Assert.AreEqual(expected, result);
        }

        [TestMethod]
        public void PluginVersionResolver_IsCompatible_NullVersion_ShouldThrow()
        {
            // Act & Assert
            Assert.ThrowsExactly<ArgumentNullException>(() => PluginVersionResolver.IsCompatible(null!, ">=1.0.0"));
        }

        [TestMethod]
        [DataRow(null)]
        [DataRow("")]
        [DataRow("  ")]
        public void PluginVersionResolver_IsCompatible_NullOrEmptyRange_ShouldThrow(string range)
        {
            // Arrange
            var v = new Version(1, 0, 0);

            // Act & Assert
            Assert.ThrowsExactly<ArgumentNullException>(() => PluginVersionResolver.IsCompatible(v, range));
        }

        #endregion

        #region PluginRegistry Tests

        [TestMethod]
        public void PluginRegistry_Register_ValidMetadata_ShouldSucceed()
        {
            // Arrange
            var registry = new PluginRegistry();
            var metadata = new TestPluginMetadata("test-plugin", new Version(1, 0, 0));

            // Act
            var result = registry.Register(metadata);

            // Assert
            Assert.IsTrue(result);
        }

        [TestMethod]
        public void PluginRegistry_Register_NullMetadata_ShouldThrow()
        {
            // Arrange
            var registry = new PluginRegistry();

            // Act & Assert
            Assert.ThrowsExactly<ArgumentNullException>(() => registry.Register(null!));
        }

        [TestMethod]
        public void PluginRegistry_Register_WithTags_ShouldStoreTagsCorrectly()
        {
            // Arrange
            var registry = new PluginRegistry();
            var metadata = new TestPluginMetadata("test-plugin", new Version(1, 0, 0));
            var tags = new[] { "storage", "database", "leveldb" };

            // Act
            registry.Register(metadata, null, tags);
            var success = registry.TryGet("test-plugin", out var entry);

            // Assert
            Assert.IsTrue(success);
            CollectionAssert.AreEquivalent(tags, entry!.Tags.ToArray());
        }

        [TestMethod]
        public void PluginRegistry_TryGet_ByName_ShouldReturnHighestVersion()
        {
            // Arrange
            var registry = new PluginRegistry();
            registry.Register(new TestPluginMetadata("plugin-a", new Version(1, 0, 0)));
            registry.Register(new TestPluginMetadata("plugin-a", new Version(2, 0, 0)));
            registry.Register(new TestPluginMetadata("plugin-a", new Version(1, 5, 0)));

            // Act
            var success = registry.TryGet("plugin-a", out var entry);

            // Assert
            Assert.IsTrue(success);
            Assert.AreEqual(new Version(2, 0, 0), entry!.Metadata.Version);
        }

        [TestMethod]
        public void PluginRegistry_TryGet_ByNameAndVersion_ShouldReturnExactVersion()
        {
            // Arrange
            var registry = new PluginRegistry();
            registry.Register(new TestPluginMetadata("plugin-a", new Version(1, 0, 0)));
            registry.Register(new TestPluginMetadata("plugin-a", new Version(2, 0, 0)));

            // Act
            var success = registry.TryGet("plugin-a", new Version(1, 0, 0), out var entry);

            // Assert
            Assert.IsTrue(success);
            Assert.AreEqual(new Version(1, 0, 0), entry!.Metadata.Version);
        }

        [TestMethod]
        public void PluginRegistry_TryGet_ByVersionRange_ShouldReturnHighestMatchingVersion()
        {
            // Arrange
            var registry = new PluginRegistry();
            registry.Register(new TestPluginMetadata("plugin-a", new Version(1, 0, 0)));
            registry.Register(new TestPluginMetadata("plugin-a", new Version(1, 5, 0)));
            registry.Register(new TestPluginMetadata("plugin-a", new Version(2, 0, 0)));
            registry.Register(new TestPluginMetadata("plugin-a", new Version(3, 0, 0)));

            // Act
            var success = registry.TryGet("plugin-a", ">=1.0.0 <2.0.0", out var entry);

            // Assert
            Assert.IsTrue(success);
            Assert.AreEqual(new Version(1, 5, 0), entry!.Metadata.Version);
        }

        [TestMethod]
        public void PluginRegistry_TryGet_NonExistentPlugin_ShouldReturnFalse()
        {
            // Arrange
            var registry = new PluginRegistry();

            // Act
            var success = registry.TryGet("non-existent", out var entry);

            // Assert
            Assert.IsFalse(success);
            Assert.IsNull(entry);
        }

        [TestMethod]
        public void PluginRegistry_Unregister_ByName_ShouldRemoveAllVersions()
        {
            // Arrange
            var registry = new PluginRegistry();
            registry.Register(new TestPluginMetadata("plugin-a", new Version(1, 0, 0)));
            registry.Register(new TestPluginMetadata("plugin-a", new Version(2, 0, 0)));

            // Act
            var result = registry.Unregister("plugin-a");
            var success = registry.TryGet("plugin-a", out _);

            // Assert
            Assert.IsTrue(result);
            Assert.IsFalse(success);
        }

        [TestMethod]
        public void PluginRegistry_Unregister_ByNameAndVersion_ShouldRemoveSpecificVersion()
        {
            // Arrange
            var registry = new PluginRegistry();
            registry.Register(new TestPluginMetadata("plugin-a", new Version(1, 0, 0)));
            registry.Register(new TestPluginMetadata("plugin-a", new Version(2, 0, 0)));

            // Act
            var result = registry.Unregister("plugin-a", new Version(1, 0, 0));
            var success1 = registry.TryGet("plugin-a", new Version(1, 0, 0), out _);
            var success2 = registry.TryGet("plugin-a", new Version(2, 0, 0), out _);

            // Assert
            Assert.IsTrue(result);
            Assert.IsFalse(success1);
            Assert.IsTrue(success2);
        }

        [TestMethod]
        public void PluginRegistry_GetAll_ShouldReturnAllEntries()
        {
            // Arrange
            var registry = new PluginRegistry();
            registry.Register(new TestPluginMetadata("plugin-a", new Version(1, 0, 0)));
            registry.Register(new TestPluginMetadata("plugin-a", new Version(2, 0, 0)));
            registry.Register(new TestPluginMetadata("plugin-b", new Version(1, 0, 0)));

            // Act
            var all = registry.GetAll().ToList();

            // Assert
            Assert.AreEqual(3, all.Count);
        }

        [TestMethod]
        public void PluginRegistry_Find_ByNameSubstring_ShouldReturnMatches()
        {
            // Arrange
            var registry = new PluginRegistry();
            registry.Register(new TestPluginMetadata("neo-storage", new Version(1, 0, 0)));
            registry.Register(new TestPluginMetadata("neo-rpc", new Version(1, 0, 0)));
            registry.Register(new TestPluginMetadata("other-plugin", new Version(1, 0, 0)));

            // Act
            var results = registry.Find(name: "neo").ToList();

            // Assert
            Assert.AreEqual(2, results.Count);
            Assert.IsTrue(results.Any(e => e.Metadata.Name == "neo-storage"));
            Assert.IsTrue(results.Any(e => e.Metadata.Name == "neo-rpc"));
        }

        [TestMethod]
        public void PluginRegistry_Find_ByWildcard_ShouldReturnMatches()
        {
            // Arrange
            var registry = new PluginRegistry();
            registry.Register(new TestPluginMetadata("neo-storage-leveldb", new Version(1, 0, 0)));
            registry.Register(new TestPluginMetadata("neo-storage-rocksdb", new Version(1, 0, 0)));
            registry.Register(new TestPluginMetadata("neo-rpc", new Version(1, 0, 0)));

            // Act
            var results = registry.Find(name: "neo-storage-*").ToList();

            // Assert
            Assert.AreEqual(2, results.Count);
        }

        [TestMethod]
        public void PluginRegistry_Find_ByVersionRange_ShouldReturnMatches()
        {
            // Arrange
            var registry = new PluginRegistry();
            registry.Register(new TestPluginMetadata("plugin-a", new Version(1, 0, 0)));
            registry.Register(new TestPluginMetadata("plugin-a", new Version(2, 0, 0)));
            registry.Register(new TestPluginMetadata("plugin-a", new Version(3, 0, 0)));

            // Act
            var results = registry.Find(versionRange: ">=1.0.0 <3.0.0").ToList();

            // Assert
            Assert.AreEqual(2, results.Count);
        }

        [TestMethod]
        public void PluginRegistry_Find_ByTags_ShouldReturnMatches()
        {
            // Arrange
            var registry = new PluginRegistry();
            registry.Register(new TestPluginMetadata("plugin-a", new Version(1, 0, 0)), null, new[] { "storage", "database" });
            registry.Register(new TestPluginMetadata("plugin-b", new Version(1, 0, 0)), null, new[] { "storage" });
            registry.Register(new TestPluginMetadata("plugin-c", new Version(1, 0, 0)), null, new[] { "rpc" });

            // Act
            var results = registry.Find(tags: new[] { "storage" }).ToList();

            // Assert
            Assert.AreEqual(2, results.Count);
        }

        [TestMethod]
        public void PluginRegistry_Find_CombinedFilters_ShouldReturnMatches()
        {
            // Arrange
            var registry = new PluginRegistry();
            registry.Register(new TestPluginMetadata("neo-storage", new Version(1, 0, 0)), null, new[] { "storage" });
            registry.Register(new TestPluginMetadata("neo-storage", new Version(2, 0, 0)), null, new[] { "storage" });
            registry.Register(new TestPluginMetadata("neo-rpc", new Version(1, 5, 0)), null, new[] { "rpc" });

            // Act
            var results = registry.Find(name: "neo", versionRange: ">=1.0.0 <2.0.0", tags: new[] { "storage" }).ToList();

            // Assert
            Assert.AreEqual(1, results.Count);
            Assert.AreEqual("neo-storage", results[0].Metadata.Name);
            Assert.AreEqual(new Version(1, 0, 0), results[0].Metadata.Version);
        }

        [TestMethod]
        public void PluginRegistry_CaseInsensitive_ShouldWork()
        {
            // Arrange
            var registry = new PluginRegistry();
            registry.Register(new TestPluginMetadata("Neo-Storage", new Version(1, 0, 0)));

            // Act
            var success1 = registry.TryGet("neo-storage", out _);
            var success2 = registry.TryGet("NEO-STORAGE", out _);

            // Assert
            Assert.IsTrue(success1);
            Assert.IsTrue(success2);
        }

        #endregion

        #region Test Helper Classes

        private class TestPluginMetadata : IPluginMetadata
        {
            public string Name { get; }
            public Version Version { get; }
            public string Description { get; }
            public IReadOnlyList<PluginDependency> Dependencies { get; }
            public Version? MinNeoVersion { get; }
            public Version? MaxNeoVersion { get; }

            public TestPluginMetadata(
                string name,
                Version version,
                string description = "Test plugin",
                IReadOnlyList<PluginDependency>? dependencies = null,
                Version? minNeoVersion = null,
                Version? maxNeoVersion = null)
            {
                Name = name;
                Version = version;
                Description = description;
                Dependencies = dependencies ?? Array.Empty<PluginDependency>();
                MinNeoVersion = minNeoVersion;
                MaxNeoVersion = maxNeoVersion;
            }
        }

        #endregion
    }
}
