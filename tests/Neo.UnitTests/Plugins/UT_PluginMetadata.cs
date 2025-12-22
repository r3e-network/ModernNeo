// Copyright (C) 2015-2025 The Neo Project.
//
// UT_PluginMetadata.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

#nullable enable
#pragma warning disable MSTEST0039 // Use 'Assert.ThrowsExactly' instead of 'Assert.ThrowsException'
#pragma warning disable MSTEST0049 // Use 'CancellationToken' parameter

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.Plugins;
using System;
using System.Linq;

namespace Neo.UnitTests.Plugins
{
    [TestClass]
    public class UT_PluginMetadata
    {
        // Helper method to convert string array to PluginDependency array
        private static PluginDependency[] Deps(params string[] names) =>
            names.Select(n => new PluginDependency(n, "*")).ToArray();

        #region Constructor and Property Tests

        [TestMethod]
        public void TestPluginMetadata_Constructor_ValidData_Succeeds()
        {
            var metadata = new PluginMetadata
            {
                Name = "TestPlugin",
                Version = new Version(1, 2, 3),
                Description = "Test description",
                Dependencies = Deps("Dep1", "Dep2"),
                MinNeoVersion = new Version(3, 0, 0),
                MaxNeoVersion = new Version(4, 0, 0),
                Author = "Test Author",
                License = "MIT"
            };

            Assert.AreEqual("TestPlugin", metadata.Name);
            Assert.AreEqual(new Version(1, 2, 3), metadata.Version);
            Assert.AreEqual("Test description", metadata.Description);
            Assert.AreEqual(2, metadata.Dependencies.Count);
            Assert.AreEqual(new Version(3, 0, 0), metadata.MinNeoVersion);
            Assert.AreEqual(new Version(4, 0, 0), metadata.MaxNeoVersion);
            Assert.AreEqual("Test Author", metadata.Author);
            Assert.AreEqual("MIT", metadata.License);
        }

        [TestMethod]
        public void TestPluginMetadata_DefaultVersion_Is100()
        {
            var metadata = new PluginMetadata
            {
                Name = "TestPlugin"
            };

            Assert.AreEqual(new Version(1, 0, 0), metadata.Version);
        }

        [TestMethod]
        public void TestPluginMetadata_DefaultDescription_IsEmpty()
        {
            var metadata = new PluginMetadata
            {
                Name = "TestPlugin"
            };

            Assert.AreEqual(string.Empty, metadata.Description);
        }

        [TestMethod]
        public void TestPluginMetadata_DefaultDependencies_IsEmpty()
        {
            var metadata = new PluginMetadata
            {
                Name = "TestPlugin"
            };

            Assert.AreEqual(0, metadata.Dependencies.Count);
        }

        [TestMethod]
        public void TestPluginMetadata_NullMinNeoVersion_Allowed()
        {
            var metadata = new PluginMetadata
            {
                Name = "TestPlugin",
                MinNeoVersion = null
            };

            Assert.IsNull(metadata.MinNeoVersion);
        }

        [TestMethod]
        public void TestPluginMetadata_NullMaxNeoVersion_Allowed()
        {
            var metadata = new PluginMetadata
            {
                Name = "TestPlugin",
                MaxNeoVersion = null
            };

            Assert.IsNull(metadata.MaxNeoVersion);
        }

        #endregion

        #region Compatibility Tests

        [TestMethod]
        public void TestPluginMetadata_IsCompatibleWith_NoConstraints_AlwaysTrue()
        {
            var metadata = new PluginMetadata
            {
                Name = "TestPlugin",
                MinNeoVersion = null,
                MaxNeoVersion = null
            };

            Assert.IsTrue(metadata.IsCompatibleWith(new Version(1, 0, 0)));
            Assert.IsTrue(metadata.IsCompatibleWith(new Version(99, 99, 99)));
        }

        [TestMethod]
        public void TestPluginMetadata_IsCompatibleWith_MinVersionOnly_Enforced()
        {
            var metadata = new PluginMetadata
            {
                Name = "TestPlugin",
                MinNeoVersion = new Version(3, 0, 0)
            };

            Assert.IsFalse(metadata.IsCompatibleWith(new Version(2, 9, 9)));
            Assert.IsTrue(metadata.IsCompatibleWith(new Version(3, 0, 0)));
            Assert.IsTrue(metadata.IsCompatibleWith(new Version(3, 0, 1)));
            Assert.IsTrue(metadata.IsCompatibleWith(new Version(4, 0, 0)));
        }

        [TestMethod]
        public void TestPluginMetadata_IsCompatibleWith_MaxVersionOnly_Enforced()
        {
            var metadata = new PluginMetadata
            {
                Name = "TestPlugin",
                MaxNeoVersion = new Version(4, 0, 0)
            };

            Assert.IsTrue(metadata.IsCompatibleWith(new Version(1, 0, 0)));
            Assert.IsTrue(metadata.IsCompatibleWith(new Version(3, 9, 9)));
            Assert.IsTrue(metadata.IsCompatibleWith(new Version(4, 0, 0)));
            Assert.IsFalse(metadata.IsCompatibleWith(new Version(4, 0, 1)));
        }

        [TestMethod]
        public void TestPluginMetadata_IsCompatibleWith_BothVersions_Enforced()
        {
            var metadata = new PluginMetadata
            {
                Name = "TestPlugin",
                MinNeoVersion = new Version(3, 0, 0),
                MaxNeoVersion = new Version(4, 0, 0)
            };

            Assert.IsFalse(metadata.IsCompatibleWith(new Version(2, 9, 9)));
            Assert.IsTrue(metadata.IsCompatibleWith(new Version(3, 0, 0)));
            Assert.IsTrue(metadata.IsCompatibleWith(new Version(3, 5, 0)));
            Assert.IsTrue(metadata.IsCompatibleWith(new Version(4, 0, 0)));
            Assert.IsFalse(metadata.IsCompatibleWith(new Version(4, 0, 1)));
        }

        [TestMethod]
        public void TestPluginMetadata_IsCompatibleWith_NullVersion_Throws()
        {
            var metadata = new PluginMetadata
            {
                Name = "TestPlugin"
            };

            Assert.ThrowsException<ArgumentNullException>(() =>
                metadata.IsCompatibleWith(null!));
        }

        #endregion

        #region Equality Tests

        [TestMethod]
        public void TestPluginMetadata_Equals_SameNameAndVersion_ReturnsTrue()
        {
            var metadata1 = new PluginMetadata
            {
                Name = "TestPlugin",
                Version = new Version(1, 0, 0)
            };

            var metadata2 = new PluginMetadata
            {
                Name = "TestPlugin",
                Version = new Version(1, 0, 0)
            };

            Assert.IsTrue(metadata1.Equals(metadata2));
        }

        [TestMethod]
        public void TestPluginMetadata_Equals_DifferentName_ReturnsFalse()
        {
            var metadata1 = new PluginMetadata
            {
                Name = "Plugin1",
                Version = new Version(1, 0, 0)
            };

            var metadata2 = new PluginMetadata
            {
                Name = "Plugin2",
                Version = new Version(1, 0, 0)
            };

            Assert.IsFalse(metadata1.Equals(metadata2));
        }

        [TestMethod]
        public void TestPluginMetadata_Equals_DifferentVersion_ReturnsFalse()
        {
            var metadata1 = new PluginMetadata
            {
                Name = "TestPlugin",
                Version = new Version(1, 0, 0)
            };

            var metadata2 = new PluginMetadata
            {
                Name = "TestPlugin",
                Version = new Version(2, 0, 0)
            };

            Assert.IsFalse(metadata1.Equals(metadata2));
        }

        [TestMethod]
        public void TestPluginMetadata_Equals_CaseInsensitiveName_ReturnsTrue()
        {
            var metadata1 = new PluginMetadata
            {
                Name = "TestPlugin",
                Version = new Version(1, 0, 0)
            };

            var metadata2 = new PluginMetadata
            {
                Name = "testplugin",
                Version = new Version(1, 0, 0)
            };

            Assert.IsTrue(metadata1.Equals(metadata2));
        }

        [TestMethod]
        public void TestPluginMetadata_Equals_Null_ReturnsFalse()
        {
            var metadata = new PluginMetadata
            {
                Name = "TestPlugin",
                Version = new Version(1, 0, 0)
            };

            Assert.IsFalse(metadata.Equals(null));
        }

        [TestMethod]
        public void TestPluginMetadata_Equals_SameReference_ReturnsTrue()
        {
            var metadata = new PluginMetadata
            {
                Name = "TestPlugin",
                Version = new Version(1, 0, 0)
            };

            Assert.IsTrue(metadata.Equals(metadata));
        }

        [TestMethod]
        public void TestPluginMetadata_GetHashCode_SameNameAndVersion_SameHash()
        {
            var metadata1 = new PluginMetadata
            {
                Name = "TestPlugin",
                Version = new Version(1, 0, 0)
            };

            var metadata2 = new PluginMetadata
            {
                Name = "TestPlugin",
                Version = new Version(1, 0, 0)
            };

            Assert.AreEqual(metadata1.GetHashCode(), metadata2.GetHashCode());
        }

        [TestMethod]
        public void TestPluginMetadata_GetHashCode_CaseInsensitive_SameHash()
        {
            var metadata1 = new PluginMetadata
            {
                Name = "TestPlugin",
                Version = new Version(1, 0, 0)
            };

            var metadata2 = new PluginMetadata
            {
                Name = "TESTPLUGIN",
                Version = new Version(1, 0, 0)
            };

            Assert.AreEqual(metadata1.GetHashCode(), metadata2.GetHashCode());
        }

        #endregion

        #region FromAttribute Tests

        [TestMethod]
        public void TestPluginMetadata_FromAttribute_ValidAttribute_Succeeds()
        {
            var attribute = new PluginMetadataAttribute("TestPlugin", "1.2.3")
            {
                Description = "Test description",
                Dependencies = new[] { "Dep1", "Dep2" },
                MinNeoVersion = "3.0.0",
                MaxNeoVersion = "4.0.0",
                Author = "Test Author",
                License = "MIT"
            };

            var metadata = PluginMetadata.FromAttribute(attribute);

            Assert.AreEqual("TestPlugin", metadata.Name);
            Assert.AreEqual(new Version(1, 2, 3), metadata.Version);
            Assert.AreEqual("Test description", metadata.Description);
            Assert.AreEqual(2, metadata.Dependencies.Count);
            Assert.AreEqual(new Version(3, 0, 0), metadata.MinNeoVersion);
            Assert.AreEqual(new Version(4, 0, 0), metadata.MaxNeoVersion);
            Assert.AreEqual("Test Author", metadata.Author);
            Assert.AreEqual("MIT", metadata.License);
        }

        [TestMethod]
        public void TestPluginMetadata_FromAttribute_NullAttribute_Throws()
        {
            Assert.ThrowsException<ArgumentNullException>(() =>
                PluginMetadata.FromAttribute(null!));
        }

        [TestMethod]
        public void TestPluginMetadata_FromAttribute_InvalidVersion_UsesDefault()
        {
            var attribute = new PluginMetadataAttribute("TestPlugin", "invalid");

            var metadata = PluginMetadata.FromAttribute(attribute);

            Assert.AreEqual(new Version(1, 0, 0), metadata.Version);
        }

        [TestMethod]
        public void TestPluginMetadata_FromAttribute_InvalidMinNeoVersion_Null()
        {
            var attribute = new PluginMetadataAttribute("TestPlugin", "1.0.0")
            {
                MinNeoVersion = "invalid"
            };

            var metadata = PluginMetadata.FromAttribute(attribute);

            Assert.IsNull(metadata.MinNeoVersion);
        }

        [TestMethod]
        public void TestPluginMetadata_FromAttribute_InvalidMaxNeoVersion_Null()
        {
            var attribute = new PluginMetadataAttribute("TestPlugin", "1.0.0")
            {
                MaxNeoVersion = "invalid"
            };

            var metadata = PluginMetadata.FromAttribute(attribute);

            Assert.IsNull(metadata.MaxNeoVersion);
        }

        [TestMethod]
        public void TestPluginMetadata_FromAttribute_EmptyDependencies_Filtered()
        {
            var attribute = new PluginMetadataAttribute("TestPlugin", "1.0.0")
            {
                Dependencies = new[] { "Dep1", "", "  ", null!, "Dep2" }
            };

            var metadata = PluginMetadata.FromAttribute(attribute);

            Assert.AreEqual(2, metadata.Dependencies.Count);
            Assert.IsTrue(metadata.Dependencies.Any(d => d.Name == "Dep1"));
            Assert.IsTrue(metadata.Dependencies.Any(d => d.Name == "Dep2"));
        }

        [TestMethod]
        public void TestPluginMetadata_FromAttribute_NullDependencies_EmptyArray()
        {
            var attribute = new PluginMetadataAttribute("TestPlugin", "1.0.0")
            {
                Dependencies = null
            };

            var metadata = PluginMetadata.FromAttribute(attribute);

            Assert.AreEqual(0, metadata.Dependencies.Count);
        }

        #endregion

        #region PluginMetadataAttribute Tests

        [TestMethod]
        public void TestPluginMetadataAttribute_Constructor_ValidInputs_Succeeds()
        {
            var attribute = new PluginMetadataAttribute("TestPlugin", "1.2.3");

            Assert.AreEqual("TestPlugin", attribute.Name);
            Assert.AreEqual("1.2.3", attribute.Version);
        }

        [TestMethod]
        [DataRow(null, "1.0.0")]
        [DataRow("", "1.0.0")]
        [DataRow("  ", "1.0.0")]
        public void TestPluginMetadataAttribute_Constructor_InvalidName_Throws(string name, string version)
        {
            Assert.ThrowsException<ArgumentNullException>(() =>
                new PluginMetadataAttribute(name, version));
        }

        [TestMethod]
        [DataRow("TestPlugin", null)]
        [DataRow("TestPlugin", "")]
        [DataRow("TestPlugin", "  ")]
        public void TestPluginMetadataAttribute_Constructor_InvalidVersion_Throws(string name, string version)
        {
            Assert.ThrowsException<ArgumentNullException>(() =>
                new PluginMetadataAttribute(name, version));
        }

        [TestMethod]
        public void TestPluginMetadataAttribute_Properties_CanBeSet()
        {
            var attribute = new PluginMetadataAttribute("TestPlugin", "1.0.0")
            {
                Description = "Test",
                Dependencies = new[] { "Dep1" },
                MinNeoVersion = "3.0.0",
                MaxNeoVersion = "4.0.0",
                Author = "Author",
                License = "MIT"
            };

            Assert.AreEqual("Test", attribute.Description);
            Assert.AreEqual(1, attribute.Dependencies!.Length);
            Assert.AreEqual("3.0.0", attribute.MinNeoVersion);
            Assert.AreEqual("4.0.0", attribute.MaxNeoVersion);
            Assert.AreEqual("Author", attribute.Author);
            Assert.AreEqual("MIT", attribute.License);
        }

        #endregion

        #region Edge Case Tests

        [TestMethod]
        public void TestPluginMetadata_EmptyName_Allowed()
        {
            var metadata = new PluginMetadata
            {
                Name = string.Empty
            };

            Assert.AreEqual(string.Empty, metadata.Name);
        }

        [TestMethod]
        public void TestPluginMetadata_LongName_Allowed()
        {
            var longName = new string('A', 1000);
            var metadata = new PluginMetadata
            {
                Name = longName
            };

            Assert.AreEqual(longName, metadata.Name);
        }

        [TestMethod]
        public void TestPluginMetadata_SpecialCharactersInName_Allowed()
        {
            var metadata = new PluginMetadata
            {
                Name = "Test-Plugin_v1.0@neo"
            };

            Assert.AreEqual("Test-Plugin_v1.0@neo", metadata.Name);
        }

        [TestMethod]
        public void TestPluginMetadata_CircularDependency_Allowed()
        {
            var metadata = new PluginMetadata
            {
                Name = "TestPlugin",
                Dependencies = Deps("TestPlugin")
            };

            Assert.AreEqual(1, metadata.Dependencies.Count);
            Assert.AreEqual("TestPlugin", metadata.Dependencies[0].Name);
        }

        [TestMethod]
        public void TestPluginMetadata_DuplicateDependencies_Allowed()
        {
            var metadata = new PluginMetadata
            {
                Name = "TestPlugin",
                Dependencies = Deps("Dep1", "Dep1", "Dep1")
            };

            Assert.AreEqual(3, metadata.Dependencies.Count);
        }

        #endregion
    }
}
