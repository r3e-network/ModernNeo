// Copyright (C) 2015-2025 The Neo Project.
//
// PluginDependencyGraph.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Neo.Plugins
{
    /// <summary>
    /// Represents a dependency graph of plugins and provides validation and load ordering.
    /// </summary>
    public sealed class PluginDependencyGraph
    {
        private readonly Dictionary<string, PluginMetadata> _plugins;
        private readonly Dictionary<string, HashSet<string>> _dependencies;
        private readonly HashSet<string> _selfDependencies;

        /// <summary>
        /// Initializes a new instance of the <see cref="PluginDependencyGraph"/> class.
        /// </summary>
        /// <param name="plugins">The plugins to include in the graph.</param>
        public PluginDependencyGraph(IEnumerable<PluginMetadata> plugins)
        {
            if (plugins is null) throw new ArgumentNullException(nameof(plugins));

            _plugins = new Dictionary<string, PluginMetadata>(StringComparer.OrdinalIgnoreCase);
            _dependencies = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            _selfDependencies = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var plugin in plugins)
            {
                if (string.IsNullOrWhiteSpace(plugin.Name))
                    throw new ArgumentException("Plugin metadata must have a non-empty Name.", nameof(plugins));

                if (_plugins.ContainsKey(plugin.Name))
                    throw new ArgumentException($"Duplicate plugin name '{plugin.Name}'.", nameof(plugins));

                _plugins.Add(plugin.Name, plugin);

                var deps = plugin.Dependencies?
                    .Where(static d => !string.IsNullOrWhiteSpace(d.Name))
                    .Select(static d => d.Name.Trim())
                    .ToHashSet(StringComparer.OrdinalIgnoreCase)
                    ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                // Track self-dependencies as cycles before removing them
                if (deps.Contains(plugin.Name))
                    _selfDependencies.Add(plugin.Name);

                deps.Remove(plugin.Name);
                _dependencies.Add(plugin.Name, deps);
            }
        }

        /// <summary>
        /// Gets the plugins contained in this graph by name.
        /// </summary>
        public IReadOnlyDictionary<string, PluginMetadata> Plugins => new ReadOnlyDictionary<string, PluginMetadata>(_plugins);

        /// <summary>
        /// Validates the graph for missing dependencies and cyclic dependencies.
        /// </summary>
        /// <returns>The validation result.</returns>
        public PluginDependencyGraphValidationResult Validate()
        {
            var missing = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var (name, deps) in _dependencies)
            {
                var miss = deps.Where(d => !_plugins.ContainsKey(d)).ToArray();
                if (miss.Length > 0)
                    missing[name] = miss;
            }

            var cycles = FindCycles(_plugins.Keys);

            // Add self-dependencies as single-element cycles
            var allCycles = new List<IReadOnlyList<string>>(cycles);
            foreach (var selfDep in _selfDependencies)
            {
                allCycles.Add(new[] { selfDep });
            }

            return new PluginDependencyGraphValidationResult(
                missingDependencies: missing,
                cycles: allCycles);
        }

        /// <summary>
        /// Attempts to compute a load order that respects dependencies.
        /// </summary>
        /// <param name="loadOrder">The computed load order.</param>
        /// <param name="validationResult">The validation result, including excluded plugins.</param>
        /// <returns><see langword="true"/> if a complete load order is produced for all valid plugins; otherwise, <see langword="false"/>.</returns>
        public bool TryGetLoadOrder(
            out IReadOnlyList<PluginMetadata> loadOrder,
            out PluginDependencyGraphValidationResult validationResult)
        {
            var excluded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Exclude self-dependent plugins first
            foreach (var selfDep in _selfDependencies)
                excluded.Add(selfDep);

            var missing = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var (name, deps) in _dependencies)
            {
                var miss = deps.Where(d => !_plugins.ContainsKey(d)).ToArray();
                if (miss.Length == 0) continue;
                missing[name] = miss;
                excluded.Add(name);
            }

            // Prune plugins that depend on excluded plugins, iteratively.
            bool changed;
            do
            {
                changed = false;
                foreach (var (name, deps) in _dependencies)
                {
                    if (excluded.Contains(name)) continue;
                    if (deps.Any(excluded.Contains))
                    {
                        excluded.Add(name);
                        changed = true;
                    }
                }
            } while (changed);

            // Cycle detection only considers the remaining subgraph.
            var remaining = _plugins.Keys.Where(n => !excluded.Contains(n)).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var cycles = FindCycles(remaining);

            foreach (var cycle in cycles)
            {
                foreach (var node in cycle)
                    excluded.Add(node);
            }

            // Prune dependents of cyclic nodes.
            do
            {
                changed = false;
                foreach (var (name, deps) in _dependencies)
                {
                    if (excluded.Contains(name)) continue;
                    if (deps.Any(excluded.Contains))
                    {
                        excluded.Add(name);
                        changed = true;
                    }
                }
            } while (changed);

            // Include self-dependencies in cycles list
            var allCycles = new List<IReadOnlyList<string>>(cycles);
            foreach (var selfDep in _selfDependencies)
            {
                allCycles.Add(new[] { selfDep });
            }

            validationResult = new PluginDependencyGraphValidationResult(
                missingDependencies: missing,
                cycles: allCycles,
                excludedPlugins: excluded);

            loadOrder = TopologicalSort(_plugins.Keys.Where(n => !excluded.Contains(n)));

            // A result can be "successful" even if some plugins were excluded; success is defined as "no cycles/missing in the included set".
            return validationResult.MissingDependencies.Count == 0 && validationResult.Cycles.Count == 0;
        }

        /// <summary>
        /// Computes a load order that respects dependencies.
        /// </summary>
        /// <returns>The topologically sorted plugin metadata list.</returns>
        /// <exception cref="PluginDependencyGraphException">Thrown when missing dependencies or cyclic dependencies are found.</exception>
        public IReadOnlyList<PluginMetadata> GetLoadOrder()
        {
            if (!TryGetLoadOrder(out var loadOrder, out var validation))
                throw new PluginDependencyGraphException(validation);

            return loadOrder;
        }

        private IReadOnlyList<PluginMetadata> TopologicalSort(IEnumerable<string> nodes)
        {
            var nodeSet = nodes.ToHashSet(StringComparer.OrdinalIgnoreCase);
            var inDegree = nodeSet.ToDictionary(static n => n, _ => 0, StringComparer.OrdinalIgnoreCase);

            foreach (var name in nodeSet)
            {
                foreach (var dep in _dependencies[name].Where(nodeSet.Contains))
                    inDegree[name]++;
            }

            var queue = new Queue<string>(inDegree.Where(kv => kv.Value == 0).Select(kv => kv.Key));
            var order = new List<PluginMetadata>(nodeSet.Count);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                order.Add(_plugins[current]);

                foreach (var dependent in nodeSet.Where(n => _dependencies[n].Contains(current)))
                {
                    inDegree[dependent]--;
                    if (inDegree[dependent] == 0)
                        queue.Enqueue(dependent);
                }
            }

            return order;
        }

        private IReadOnlyList<IReadOnlyList<string>> FindCycles(IEnumerable<string> nodes)
        {
            var nodeSet = nodes.ToHashSet(StringComparer.OrdinalIgnoreCase);
            var index = 0;
            var stack = new Stack<string>();
            var onStack = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var indices = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var lowLink = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var sccs = new List<IReadOnlyList<string>>();

            foreach (var v in nodeSet)
            {
                if (!indices.ContainsKey(v))
                    StrongConnect(v);
            }

            return sccs;

            void StrongConnect(string v)
            {
                indices[v] = index;
                lowLink[v] = index;
                index++;

                stack.Push(v);
                onStack.Add(v);

                foreach (var w in _dependencies[v].Where(nodeSet.Contains))
                {
                    if (!indices.ContainsKey(w))
                    {
                        StrongConnect(w);
                        lowLink[v] = Math.Min(lowLink[v], lowLink[w]);
                    }
                    else if (onStack.Contains(w))
                    {
                        lowLink[v] = Math.Min(lowLink[v], indices[w]);
                    }
                }

                if (lowLink[v] != indices[v]) return;

                var component = new List<string>();
                string w2;
                do
                {
                    w2 = stack.Pop();
                    onStack.Remove(w2);
                    component.Add(w2);
                } while (!string.Equals(w2, v, StringComparison.OrdinalIgnoreCase));

                if (component.Count > 1)
                {
                    sccs.Add(component);
                    return;
                }

                // Self-loop counts as a cycle.
                if (_dependencies[v].Contains(v))
                    sccs.Add(component);
            }
        }
    }

    /// <summary>
    /// Represents validation results for a <see cref="PluginDependencyGraph"/>.
    /// </summary>
    public sealed class PluginDependencyGraphValidationResult
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PluginDependencyGraphValidationResult"/> class.
        /// </summary>
        /// <param name="missingDependencies">Missing dependencies by plugin.</param>
        /// <param name="cycles">Detected cycles.</param>
        /// <param name="excludedPlugins">Plugins excluded from load ordering due to missing dependencies or cycles.</param>
        public PluginDependencyGraphValidationResult(
            IReadOnlyDictionary<string, IReadOnlyList<string>>? missingDependencies = null,
            IReadOnlyList<IReadOnlyList<string>>? cycles = null,
            IReadOnlyCollection<string>? excludedPlugins = null)
        {
            MissingDependencies = missingDependencies ?? new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
            Cycles = cycles ?? Array.Empty<IReadOnlyList<string>>();
            ExcludedPlugins = excludedPlugins ?? Array.Empty<string>();
        }

        /// <summary>
        /// Gets a map from plugin name to missing dependency names.
        /// </summary>
        public IReadOnlyDictionary<string, IReadOnlyList<string>> MissingDependencies { get; }

        /// <summary>
        /// Gets the list of detected cyclic dependency groups.
        /// </summary>
        public IReadOnlyList<IReadOnlyList<string>> Cycles { get; }

        /// <summary>
        /// Gets the plugins excluded from the computed load order due to validation failures.
        /// </summary>
        public IReadOnlyCollection<string> ExcludedPlugins { get; }

        /// <summary>
        /// Gets a value indicating whether the dependency graph is valid (no missing dependencies and no cycles).
        /// </summary>
        public bool IsValid => MissingDependencies.Count == 0 && Cycles.Count == 0;
    }

    /// <summary>
    /// Represents an error that occurs when a <see cref="PluginDependencyGraph"/> fails validation.
    /// </summary>
    public sealed class PluginDependencyGraphException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PluginDependencyGraphException"/> class.
        /// </summary>
        /// <param name="validationResult">The validation result.</param>
        public PluginDependencyGraphException(PluginDependencyGraphValidationResult validationResult)
            : base("Plugin dependency graph validation failed.")
        {
            ValidationResult = validationResult ?? throw new ArgumentNullException(nameof(validationResult));
        }

        /// <summary>
        /// Gets the validation result.
        /// </summary>
        public PluginDependencyGraphValidationResult ValidationResult { get; }
    }
}
