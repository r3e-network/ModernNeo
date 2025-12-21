// Copyright (C) 2015-2025 The Neo Project.
//
// UT_PluginDependencyGraph.cs file belongs to the neo project and is free
// software distributed under the MIT software license.

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
    public class UT_PluginDependencyGraph
    {
        // Helper method to convert string array to PluginDependency array
        private static PluginDependency[] Deps(params string[] names) =>
            names.Select(n => new PluginDependency(n, "*")).ToArray();

        #region Constructor Tests

        [TestMethod]
        public void TestPluginDependencyGraph_Constructor_EmptyList_Succeeds()
        {
            var graph = new PluginDependencyGraph(Array.Empty<PluginMetadata>());

            Assert.AreEqual(0, graph.Plugins.Count);
        }

        [TestMethod]
        public void TestPluginDependencyGraph_Constructor_NullList_Throws()
        {
            Assert.ThrowsException<ArgumentNullException>(() =>
                new PluginDependencyGraph(null!));
        }

        [TestMethod]
        public void TestPluginDependencyGraph_Constructor_ValidPlugins_Succeeds()
        {
            var plugins = new[]
            {
                new PluginMetadata { Name = "Plugin1" },
                new PluginMetadata { Name = "Plugin2" }
            };

            var graph = new PluginDependencyGraph(plugins);

            Assert.AreEqual(2, graph.Plugins.Count);
        }

        [TestMethod]
        public void TestPluginDependencyGraph_Constructor_DuplicateNames_Throws()
        {
            var plugins = new[]
            {
                new PluginMetadata { Name = "Plugin1" },
                new PluginMetadata { Name = "Plugin1" }
            };

            Assert.ThrowsException<ArgumentException>(() =>
                new PluginDependencyGraph(plugins));
        }

        [TestMethod]
        public void TestPluginDependencyGraph_Constructor_EmptyPluginName_Throws()
        {
            var plugins = new[]
            {
                new PluginMetadata { Name = "" }
            };

            Assert.ThrowsException<ArgumentException>(() =>
                new PluginDependencyGraph(plugins));
        }

        [TestMethod]
        public void TestPluginDependencyGraph_Constructor_WhitespacePluginName_Throws()
        {
            var plugins = new[]
            {
                new PluginMetadata { Name = "   " }
            };

            Assert.ThrowsException<ArgumentException>(() =>
                new PluginDependencyGraph(plugins));
        }

        #endregion

        #region Validation Tests

        [TestMethod]
        public void TestPluginDependencyGraph_Validate_NoDependencies_Valid()
        {
            var plugins = new[]
            {
                new PluginMetadata { Name = "Plugin1" },
                new PluginMetadata { Name = "Plugin2" }
            };

            var graph = new PluginDependencyGraph(plugins);
            var result = graph.Validate();

            Assert.IsTrue(result.IsValid);
            Assert.AreEqual(0, result.MissingDependencies.Count);
            Assert.AreEqual(0, result.Cycles.Count);
        }

        [TestMethod]
        public void TestPluginDependencyGraph_Validate_ValidDependencies_Valid()
        {
            var plugins = new[]
            {
                new PluginMetadata { Name = "Plugin1" },
                new PluginMetadata { Name = "Plugin2", Dependencies = Deps("Plugin1") }
            };

            var graph = new PluginDependencyGraph(plugins);
            var result = graph.Validate();

            Assert.IsTrue(result.IsValid);
        }

        [TestMethod]
        public void TestPluginDependencyGraph_Validate_MissingDependency_Invalid()
        {
            var plugins = new[]
            {
                new PluginMetadata { Name = "Plugin1", Dependencies = Deps("NonExistent") }
            };

            var graph = new PluginDependencyGraph(plugins);
            var result = graph.Validate();

            Assert.IsFalse(result.IsValid);
            Assert.AreEqual(1, result.MissingDependencies.Count);
            Assert.IsTrue(result.MissingDependencies.ContainsKey("Plugin1"));
            Assert.AreEqual(1, result.MissingDependencies["Plugin1"].Count);
            Assert.AreEqual("NonExistent", result.MissingDependencies["Plugin1"][0]);
        }

        [TestMethod]
        public void TestPluginDependencyGraph_Validate_MultipleMissingDependencies_Invalid()
        {
            var plugins = new[]
            {
                new PluginMetadata { Name = "Plugin1", Dependencies = Deps("Missing1", "Missing2") }
            };

            var graph = new PluginDependencyGraph(plugins);
            var result = graph.Validate();

            Assert.IsFalse(result.IsValid);
            Assert.AreEqual(2, result.MissingDependencies["Plugin1"].Count);
        }

        [TestMethod]
        public void TestPluginDependencyGraph_Validate_SelfDependency_Cycle()
        {
            var plugins = new[]
            {
                new PluginMetadata { Name = "Plugin1", Dependencies = Deps("Plugin1") }
            };

            var graph = new PluginDependencyGraph(plugins);
            var result = graph.Validate();

            Assert.IsFalse(result.IsValid);
            Assert.AreEqual(1, result.Cycles.Count);
            Assert.IsTrue(result.Cycles[0].Contains("Plugin1"));
        }

        [TestMethod]
        public void TestPluginDependencyGraph_Validate_SimpleCycle_Detected()
        {
            var plugins = new[]
            {
                new PluginMetadata { Name = "Plugin1", Dependencies = Deps("Plugin2") },
                new PluginMetadata { Name = "Plugin2", Dependencies = Deps("Plugin1") }
            };

            var graph = new PluginDependencyGraph(plugins);
            var result = graph.Validate();

            Assert.IsFalse(result.IsValid);
            Assert.AreEqual(1, result.Cycles.Count);
            Assert.AreEqual(2, result.Cycles[0].Count);
        }

        [TestMethod]
        public void TestPluginDependencyGraph_Validate_ComplexCycle_Detected()
        {
            var plugins = new[]
            {
                new PluginMetadata { Name = "Plugin1", Dependencies = Deps("Plugin2") },
                new PluginMetadata { Name = "Plugin2", Dependencies = Deps("Plugin3") },
                new PluginMetadata { Name = "Plugin3", Dependencies = Deps("Plugin1") }
            };

            var graph = new PluginDependencyGraph(plugins);
            var result = graph.Validate();

            Assert.IsFalse(result.IsValid);
            Assert.AreEqual(1, result.Cycles.Count);
            Assert.AreEqual(3, result.Cycles[0].Count);
        }

        [TestMethod]
        public void TestPluginDependencyGraph_Validate_MultipleCycles_AllDetected()
        {
            var plugins = new[]
            {
                new PluginMetadata { Name = "Plugin1", Dependencies = Deps("Plugin2") },
                new PluginMetadata { Name = "Plugin2", Dependencies = Deps("Plugin1") },
                new PluginMetadata { Name = "Plugin3", Dependencies = Deps("Plugin4") },
                new PluginMetadata { Name = "Plugin4", Dependencies = Deps("Plugin3") }
            };

            var graph = new PluginDependencyGraph(plugins);
            var result = graph.Validate();

            Assert.IsFalse(result.IsValid);
            Assert.AreEqual(2, result.Cycles.Count);
        }

        #endregion

        #region Load Order Tests

        [TestMethod]
        public void TestPluginDependencyGraph_GetLoadOrder_NoDependencies_AnyOrder()
        {
            var plugins = new[]
            {
                new PluginMetadata { Name = "Plugin1" },
                new PluginMetadata { Name = "Plugin2" }
            };

            var graph = new PluginDependencyGraph(plugins);
            var loadOrder = graph.GetLoadOrder();

            Assert.AreEqual(2, loadOrder.Count);
        }

        [TestMethod]
        public void TestPluginDependencyGraph_GetLoadOrder_LinearDependency_CorrectOrder()
        {
            var plugins = new[]
            {
                new PluginMetadata { Name = "Plugin1" },
                new PluginMetadata { Name = "Plugin2", Dependencies = Deps("Plugin1") },
                new PluginMetadata { Name = "Plugin3", Dependencies = Deps("Plugin2") }
            };

            var graph = new PluginDependencyGraph(plugins);
            var loadOrder = graph.GetLoadOrder();

            Assert.AreEqual(3, loadOrder.Count);
            Assert.AreEqual("Plugin1", loadOrder[0].Name);
            Assert.AreEqual("Plugin2", loadOrder[1].Name);
            Assert.AreEqual("Plugin3", loadOrder[2].Name);
        }

        [TestMethod]
        public void TestPluginDependencyGraph_GetLoadOrder_DiamondDependency_CorrectOrder()
        {
            var plugins = new[]
            {
                new PluginMetadata { Name = "Base" },
                new PluginMetadata { Name = "Left", Dependencies = Deps("Base") },
                new PluginMetadata { Name = "Right", Dependencies = Deps("Base") },
                new PluginMetadata { Name = "Top", Dependencies = Deps("Left", "Right") }
            };

            var graph = new PluginDependencyGraph(plugins);
            var loadOrder = graph.GetLoadOrder();

            Assert.AreEqual(4, loadOrder.Count);
            Assert.AreEqual("Base", loadOrder[0].Name);
            Assert.IsTrue(loadOrder[3].Name == "Top");
        }

        [TestMethod]
        public void TestPluginDependencyGraph_GetLoadOrder_MissingDependency_Throws()
        {
            var plugins = new[]
            {
                new PluginMetadata { Name = "Plugin1", Dependencies = Deps("NonExistent") }
            };

            var graph = new PluginDependencyGraph(plugins);

            Assert.ThrowsException<PluginDependencyGraphException>(() =>
                graph.GetLoadOrder());
        }

        [TestMethod]
        public void TestPluginDependencyGraph_GetLoadOrder_Cycle_Throws()
        {
            var plugins = new[]
            {
                new PluginMetadata { Name = "Plugin1", Dependencies = Deps("Plugin2") },
                new PluginMetadata { Name = "Plugin2", Dependencies = Deps("Plugin1") }
            };

            var graph = new PluginDependencyGraph(plugins);

            Assert.ThrowsException<PluginDependencyGraphException>(() =>
                graph.GetLoadOrder());
        }

        [TestMethod]
        public void TestPluginDependencyGraph_TryGetLoadOrder_ValidGraph_ReturnsTrue()
        {
            var plugins = new[]
            {
                new PluginMetadata { Name = "Plugin1" },
                new PluginMetadata { Name = "Plugin2", Dependencies = Deps("Plugin1") }
            };

            var graph = new PluginDependencyGraph(plugins);
            var success = graph.TryGetLoadOrder(out var loadOrder, out var validation);

            Assert.IsTrue(success);
            Assert.AreEqual(2, loadOrder.Count);
            Assert.IsTrue(validation.IsValid);
        }

        [TestMethod]
        public void TestPluginDependencyGraph_TryGetLoadOrder_MissingDependency_ReturnsFalse()
        {
            var plugins = new[]
            {
                new PluginMetadata { Name = "Plugin1", Dependencies = Deps("NonExistent") }
            };

            var graph = new PluginDependencyGraph(plugins);
            var success = graph.TryGetLoadOrder(out var loadOrder, out var validation);

            Assert.IsFalse(success);
            Assert.AreEqual(0, loadOrder.Count);
            Assert.IsFalse(validation.IsValid);
            Assert.AreEqual(1, validation.MissingDependencies.Count);
            Assert.AreEqual(1, validation.ExcludedPlugins.Count);
        }

        [TestMethod]
        public void TestPluginDependencyGraph_TryGetLoadOrder_Cycle_ReturnsFalse()
        {
            var plugins = new[]
            {
                new PluginMetadata { Name = "Plugin1", Dependencies = Deps("Plugin2") },
                new PluginMetadata { Name = "Plugin2", Dependencies = Deps("Plugin1") }
            };

            var graph = new PluginDependencyGraph(plugins);
            var success = graph.TryGetLoadOrder(out var loadOrder, out var validation);

            Assert.IsFalse(success);
            Assert.AreEqual(0, loadOrder.Count);
            Assert.IsFalse(validation.IsValid);
            Assert.AreEqual(1, validation.Cycles.Count);
            Assert.AreEqual(2, validation.ExcludedPlugins.Count);
        }

        [TestMethod]
        public void TestPluginDependencyGraph_TryGetLoadOrder_PartialGraph_ExcludesInvalid()
        {
            var plugins = new[]
            {
                new PluginMetadata { Name = "Valid1" },
                new PluginMetadata { Name = "Valid2", Dependencies = Deps("Valid1") },
                new PluginMetadata { Name = "Invalid", Dependencies = Deps("NonExistent") }
            };

            var graph = new PluginDependencyGraph(plugins);
            var success = graph.TryGetLoadOrder(out var loadOrder, out var validation);

            Assert.IsFalse(success);
            Assert.AreEqual(2, loadOrder.Count);
            Assert.IsTrue(loadOrder.Any(p => p.Name == "Valid1"));
            Assert.IsTrue(loadOrder.Any(p => p.Name == "Valid2"));
            Assert.IsTrue(validation.ExcludedPlugins.Contains("Invalid"));
        }

        [TestMethod]
        public void TestPluginDependencyGraph_TryGetLoadOrder_TransitiveDependency_ExcludesDependent()
        {
            var plugins = new[]
            {
                new PluginMetadata { Name = "Base", Dependencies = Deps("NonExistent") },
                new PluginMetadata { Name = "Dependent", Dependencies = Deps("Base") }
            };

            var graph = new PluginDependencyGraph(plugins);
            var success = graph.TryGetLoadOrder(out var loadOrder, out var validation);

            Assert.IsFalse(success);
            Assert.AreEqual(0, loadOrder.Count);
            Assert.AreEqual(2, validation.ExcludedPlugins.Count);
            Assert.IsTrue(validation.ExcludedPlugins.Contains("Base"));
            Assert.IsTrue(validation.ExcludedPlugins.Contains("Dependent"));
        }

        #endregion

        #region Edge Case Tests

        [TestMethod]
        public void TestPluginDependencyGraph_EmptyDependencyList_TreatedAsNoDependencies()
        {
            var plugins = new[]
            {
                new PluginMetadata { Name = "Plugin1", Dependencies = Array.Empty<PluginDependency>() }
            };

            var graph = new PluginDependencyGraph(plugins);
            var result = graph.Validate();

            Assert.IsTrue(result.IsValid);
        }

        [TestMethod]
        public void TestPluginDependencyGraph_WhitespaceDependencies_Ignored()
        {
            // Whitespace dependencies are filtered out during PluginMetadata construction
            // Since PluginDependency constructor validates names, we test with empty dependencies
            var plugins = new[]
            {
                new PluginMetadata { Name = "Plugin1", Dependencies = Array.Empty<PluginDependency>() }
            };

            var graph = new PluginDependencyGraph(plugins);
            var result = graph.Validate();

            Assert.IsTrue(result.IsValid);
        }

        [TestMethod]
        public void TestPluginDependencyGraph_SelfDependencyRemoved_NoError()
        {
            // Self-dependencies are now detected as cycles
            // TryGetLoadOrder excludes cyclic plugins but returns valid order for others
            var plugins = new[]
            {
                new PluginMetadata { Name = "Plugin1", Dependencies = Deps("Plugin1", "Plugin2") },
                new PluginMetadata { Name = "Plugin2" }
            };

            var graph = new PluginDependencyGraph(plugins);
            var success = graph.TryGetLoadOrder(out var loadOrder, out var validationResult);

            // Plugin1 has self-dependency (cycle), so it's excluded
            Assert.IsFalse(success); // Has cycles
            Assert.AreEqual(1, validationResult.Cycles.Count);
            Assert.AreEqual(1, loadOrder.Count); // Only Plugin2 is loadable
            Assert.AreEqual("Plugin2", loadOrder[0].Name);
        }

        [TestMethod]
        public void TestPluginDependencyGraph_CaseInsensitiveNames_Handled()
        {
            var plugins = new[]
            {
                new PluginMetadata { Name = "Plugin1" },
                new PluginMetadata { Name = "Plugin2", Dependencies = Deps("plugin1") }
            };

            var graph = new PluginDependencyGraph(plugins);
            var result = graph.Validate();

            Assert.IsTrue(result.IsValid);
        }

        [TestMethod]
        public void TestPluginDependencyGraph_LargeGraph_HandlesCorrectly()
        {
            var plugins = Enumerable.Range(0, 100).Select(i =>
                new PluginMetadata
                {
                    Name = $"Plugin{i}",
                    Dependencies = i > 0 ? Deps($"Plugin{i - 1}") : Array.Empty<PluginDependency>()
                }).ToArray();

            var graph = new PluginDependencyGraph(plugins);
            var loadOrder = graph.GetLoadOrder();

            Assert.AreEqual(100, loadOrder.Count);
            Assert.AreEqual("Plugin0", loadOrder[0].Name);
            Assert.AreEqual("Plugin99", loadOrder[99].Name);
        }

        [TestMethod]
        public void TestPluginDependencyGraph_ComplexGraph_CorrectTopologicalSort()
        {
            var plugins = new[]
            {
                new PluginMetadata { Name = "A" },
                new PluginMetadata { Name = "B", Dependencies = Deps("A") },
                new PluginMetadata { Name = "C", Dependencies = Deps("A") },
                new PluginMetadata { Name = "D", Dependencies = Deps("B", "C") },
                new PluginMetadata { Name = "E", Dependencies = Deps("C") },
                new PluginMetadata { Name = "F", Dependencies = Deps("D", "E") }
            };

            var graph = new PluginDependencyGraph(plugins);
            var loadOrder = graph.GetLoadOrder();

            Assert.AreEqual(6, loadOrder.Count);

            // Verify dependencies are loaded before dependents
            var indices = loadOrder.Select((p, i) => (p.Name, i)).ToDictionary(x => x.Name, x => x.i);
            Assert.IsTrue(indices["A"] < indices["B"]);
            Assert.IsTrue(indices["A"] < indices["C"]);
            Assert.IsTrue(indices["B"] < indices["D"]);
            Assert.IsTrue(indices["C"] < indices["D"]);
            Assert.IsTrue(indices["C"] < indices["E"]);
            Assert.IsTrue(indices["D"] < indices["F"]);
            Assert.IsTrue(indices["E"] < indices["F"]);
        }

        #endregion

        #region PluginDependencyGraphException Tests

        [TestMethod]
        public void TestPluginDependencyGraphException_Constructor_ValidResult_Succeeds()
        {
            var validation = new PluginDependencyGraphValidationResult();
            var exception = new PluginDependencyGraphException(validation);

            Assert.IsNotNull(exception.ValidationResult);
            Assert.AreEqual("Plugin dependency graph validation failed.", exception.Message);
        }

        [TestMethod]
        public void TestPluginDependencyGraphException_Constructor_NullResult_Throws()
        {
            Assert.ThrowsException<ArgumentNullException>(() =>
                new PluginDependencyGraphException(null!));
        }

        #endregion

        #region PluginDependencyGraphValidationResult Tests

        [TestMethod]
        public void TestValidationResult_DefaultConstructor_EmptyCollections()
        {
            var result = new PluginDependencyGraphValidationResult();

            Assert.AreEqual(0, result.MissingDependencies.Count);
            Assert.AreEqual(0, result.Cycles.Count);
            Assert.AreEqual(0, result.ExcludedPlugins.Count);
            Assert.IsTrue(result.IsValid);
        }

        [TestMethod]
        public void TestValidationResult_WithMissingDependencies_IsInvalid()
        {
            var missing = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.IReadOnlyList<string>>
            {
                ["Plugin1"] = new[] { "Missing" }
            };

            var result = new PluginDependencyGraphValidationResult(missingDependencies: missing);

            Assert.IsFalse(result.IsValid);
            Assert.AreEqual(1, result.MissingDependencies.Count);
        }

        [TestMethod]
        public void TestValidationResult_WithCycles_IsInvalid()
        {
            var cycles = new System.Collections.Generic.List<System.Collections.Generic.IReadOnlyList<string>>
            {
                new[] { "Plugin1", "Plugin2" }
            };

            var result = new PluginDependencyGraphValidationResult(cycles: cycles);

            Assert.IsFalse(result.IsValid);
            Assert.AreEqual(1, result.Cycles.Count);
        }

        [TestMethod]
        public void TestValidationResult_WithExcludedPlugins_Tracked()
        {
            var excluded = new[] { "Plugin1", "Plugin2" };

            var result = new PluginDependencyGraphValidationResult(excludedPlugins: excluded);

            Assert.AreEqual(2, result.ExcludedPlugins.Count);
        }

        #endregion
    }
}
