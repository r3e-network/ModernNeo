// Copyright (C) 2015-2025 The Neo Project.
//
// UT_NeoNetworkBoundaries.cs file belongs to the neo project and is free
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

namespace Neo.UnitTests.Architecture
{
    [TestClass]
    public class UT_NeoNetworkBoundaries
    {
        [TestMethod]
        public void NeoNetwork_MustNotReference_SmartContractOrNativeContracts()
        {
            var repoRoot = FindRepoRoot();
            var networkDir = Path.Combine(repoRoot, "src", "Neo.Network");
            Assert.IsTrue(Directory.Exists(networkDir), "src/Neo.Network must exist.");

            var csFiles = Directory.EnumerateFiles(networkDir, "*.cs", SearchOption.AllDirectories)
                .Where(p => !IsBuildOutputPath(p))
                .ToArray();

            static bool IsBuildOutputPath(string path)
            {
                var normalized = path.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);
                return normalized.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
                    || normalized.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase);
            }

            var forbiddenTokens = new[] { "Neo.SmartContract", "NativeContract" };
            var violations = new List<string>();

            foreach (var file in csFiles)
            {
                var content = File.ReadAllText(file);
                foreach (var token in forbiddenTokens)
                {
                    if (content.Contains(token, StringComparison.Ordinal))
                    {
                        violations.Add($"{Path.GetRelativePath(repoRoot, file)} contains '{token}'.");
                    }
                }
            }

            if (violations.Count > 0)
            {
                Assert.Fail("Neo.Network boundary violations:\n" + string.Join("\n", violations.OrderBy(v => v, StringComparer.Ordinal)));
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
    }
}

