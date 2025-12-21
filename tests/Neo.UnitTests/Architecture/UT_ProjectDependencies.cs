// Copyright (C) 2015-2025 The Neo Project.
//
// UT_ProjectDependencies.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

#nullable enable

namespace Neo.UnitTests.Architecture
{
    [TestClass]
    public class UT_ProjectDependencies
    {
        private enum Layer
        {
            Base = 0,
            Infrastructure = 1,
            Core = 2,
            Service = 3,
            Application = 4,
        }

        private static readonly IReadOnlyDictionary<string, Layer> ProjectLayers = new Dictionary<string, Layer>(StringComparer.Ordinal)
        {
            // Base / primitives
            ["Neo.Extensions"] = Layer.Base,
            ["Neo.IO"] = Layer.Base,
            ["Neo.Core"] = Layer.Base,
            ["Neo.Json"] = Layer.Base,
            ["Neo.SmartContract.Abstractions"] = Layer.Base,

            // Infrastructure
            ["Neo.Cryptography"] = Layer.Infrastructure,
            ["Neo.Storage"] = Layer.Infrastructure,
            ["Neo.Observability"] = Layer.Infrastructure,
            ["Neo.Serialization.MessagePack"] = Layer.Infrastructure,

            // Core
            ["Neo.Protocol"] = Layer.Core,
            ["Neo.Protocol.Payloads"] = Layer.Core,
            ["Neo.Network"] = Layer.Core,
            ["Neo.TxPool"] = Layer.Core,
            ["Neo.Ledger"] = Layer.Core,
            ["Neo.Consensus"] = Layer.Core,
            ["Neo.SmartContract.Core"] = Layer.Infrastructure,
            ["Neo.SmartContract.Manifest"] = Layer.Infrastructure,
            ["Neo.SmartContract"] = Layer.Core,

            // Services
            ["Neo.Services"] = Layer.Service,
            ["Neo.Sign"] = Layer.Service,
            ["Neo.Wallets"] = Layer.Service,
            ["Neo.Node.Core"] = Layer.Service,
            ["Neo.Plugins"] = Layer.Service,
            ["Neo.ApplicationLogs"] = Layer.Service,
            ["Neo.RpcNep17Tracker"] = Layer.Service,
            ["Neo.LevelDBStore"] = Layer.Service,
            ["Neo.RPC"] = Layer.Service,
            ["Neo.Orleans"] = Layer.Service,
            ["Neo.Builders"] = Layer.Service,

            // Applications
            ["Neo.Node"] = Layer.Application,
            ["Neo.Node.AOT"] = Layer.Application,
        };

        [TestMethod]
        [Ignore("Legacy Neo project still exists during refactoring transition")]
        public void TestProjectReferencesRespectLayering()
        {
            var repoRoot = FindRepoRoot();
            var srcDir = Path.Combine(repoRoot, "src");
            Assert.IsFalse(File.Exists(Path.Combine(srcDir, "Neo", "Neo.csproj")), "Legacy 'src/Neo/Neo.csproj' must not exist.");

            var csprojs = Directory.EnumerateFiles(srcDir, "*.csproj", SearchOption.AllDirectories)
                .Select(p => Path.GetFullPath(p))
                .ToArray();

            var projectNameByPath = csprojs.ToDictionary(
                p => p,
                p => Path.GetFileNameWithoutExtension(p),
                StringComparer.OrdinalIgnoreCase);

            if (projectNameByPath.Values.Any(n => string.Equals(n, "Neo", StringComparison.OrdinalIgnoreCase)))
            {
                Assert.Fail("Legacy project 'Neo' must not exist. Remove 'src/Neo/Neo.csproj' and any solution references.");
            }

            var violations = new List<string>();

            foreach (var projectPath in csprojs)
            {
                var projectName = projectNameByPath[projectPath];
                if (!ProjectLayers.TryGetValue(projectName, out var projectLayer))
                    continue;

                var referencedProjects = ReadProjectReferences(projectPath)
                    .Select(referencePath => ResolveReference(projectPath, referencePath))
                    .Where(resolved => resolved is not null)
                    .Select(resolved => resolved!)
                    .Where(projectNameByPath.ContainsKey)
                    .Select(resolved => projectNameByPath[resolved])
                    .ToArray();

                foreach (var referencedName in referencedProjects)
                {
                    if (!ProjectLayers.TryGetValue(referencedName, out var referencedLayer))
                        continue;

                    if ((int)referencedLayer > (int)projectLayer)
                    {
                        violations.Add($"{projectName} ({projectLayer}) must not reference {referencedName} ({referencedLayer}).");
                    }
                }
            }

            if (violations.Count > 0)
            {
                Assert.Fail("Project layering violations:\n" + string.Join("\n", violations.OrderBy(v => v, StringComparer.Ordinal)));
            }
        }

        private static string FindRepoRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null)
            {
                if (File.Exists(Path.Combine(dir.FullName, "neo.sln")))
                    return dir.FullName;
                dir = dir.Parent;
            }

            throw new InvalidOperationException("Unable to locate repo root (neo.sln not found).");
        }

        private static IEnumerable<string> ReadProjectReferences(string csprojPath)
        {
            var doc = XDocument.Load(csprojPath, LoadOptions.PreserveWhitespace);
            var project = doc.Root;
            if (project is null) yield break;

            XNamespace ns = project.Name.Namespace;
            foreach (var el in project.Descendants(ns + "ProjectReference"))
            {
                var include = el.Attribute("Include")?.Value;
                if (string.IsNullOrWhiteSpace(include)) continue;
                yield return include!;
            }
        }

        private static string? ResolveReference(string csprojPath, string include)
        {
            var normalized = include.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);
            var candidate = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(csprojPath)!, normalized));
            return File.Exists(candidate) ? candidate : null;
        }
    }
}
