// Copyright (C) 2015-2025 The Neo Project.
//
// UT_NeoNodeCoreIsolation.cs file belongs to the neo project and is free
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
    public class UT_NeoNodeCoreIsolation
    {
        [TestMethod]
        public void NeoNodeCore_ShouldOnlyBeReferencedByApplications()
        {
            var repoRoot = FindRepoRoot();
            var srcDir = Path.Combine(repoRoot, "src");

            var nodeCoreCsproj = Path.GetFullPath(Path.Combine(srcDir, "Neo.Node.Core", "Neo.Node.Core.csproj"));
            Assert.IsTrue(File.Exists(nodeCoreCsproj), "src/Neo.Node.Core/Neo.Node.Core.csproj must exist.");

            var csprojs = Directory.EnumerateFiles(srcDir, "*.csproj", SearchOption.AllDirectories)
                .Select(p => Path.GetFullPath(p))
                .ToArray();

            var projectNameByPath = csprojs.ToDictionary(
                p => p,
                p => Path.GetFileNameWithoutExtension(p),
                StringComparer.OrdinalIgnoreCase);

            var violations = new List<string>();

            foreach (var projectPath in csprojs)
            {
                if (string.Equals(projectPath, nodeCoreCsproj, StringComparison.OrdinalIgnoreCase))
                    continue;

                var projectName = projectNameByPath[projectPath];
                var referencedProjects = ReadProjectReferences(projectPath)
                    .Select(referencePath => ResolveReference(projectPath, referencePath))
                    .Where(resolved => resolved is not null)
                    .Select(resolved => resolved!)
                    .ToArray();

                if (!referencedProjects.Any(p => string.Equals(p, nodeCoreCsproj, StringComparison.OrdinalIgnoreCase)))
                    continue;

                if (!IsAllowedReferencer(projectName))
                {
                    violations.Add($"{projectName} must not reference Neo.Node.Core.");
                }
            }

            if (violations.Count > 0)
            {
                Assert.Fail("Neo.Node.Core isolation violations:\n" + string.Join("\n", violations.OrderBy(v => v, StringComparer.Ordinal)));
            }
        }

        private static bool IsAllowedReferencer(string projectName)
        {
            return string.Equals(projectName, "Neo.Node", StringComparison.OrdinalIgnoreCase)
                || string.Equals(projectName, "Neo.Node.AOT", StringComparison.OrdinalIgnoreCase)
                || string.Equals(projectName, "Neo.ApplicationLogs", StringComparison.OrdinalIgnoreCase)
                || string.Equals(projectName, "Neo.RpcNep17Tracker", StringComparison.OrdinalIgnoreCase)
                || string.Equals(projectName, "Neo.LevelDBStore", StringComparison.OrdinalIgnoreCase);
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
