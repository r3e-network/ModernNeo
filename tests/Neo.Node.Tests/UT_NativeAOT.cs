// Copyright (C) 2015-2025 The Neo Project.
//
// UT_NativeAOT.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace Neo.Node.Tests
{
    [TestClass]
    public class UT_NativeAOT
    {
        private static string GetAotBinaryPath()
        {
            // Try platform-specific path first, then linux-x64
            var basePath = Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory,
                "..", "..", "..", "..", "..",
                "src", "Neo.Node.AOT", "bin", "Release", "net10.0"));

            var platformPath = Path.Combine(basePath, RuntimeInformation.RuntimeIdentifier, "publish", "Neo.Node.AOT");
            if (File.Exists(platformPath))
                return platformPath;

            var linuxPath = Path.Combine(basePath, "linux-x64", "publish", "Neo.Node.AOT");
            if (File.Exists(linuxPath))
                return linuxPath;

            return platformPath; // Return expected path for error messages
        }

        [TestMethod]
        [TestCategory("NativeAOT")]
        public void NativeAOT_BinaryExists()
        {
            var binaryPath = GetAotBinaryPath();

            if (!File.Exists(binaryPath))
            {
                Assert.Inconclusive($"NativeAOT binary not found at {binaryPath}. Run 'dotnet publish src/Neo.Node.AOT -c Release -r linux-x64' first.");
                return;
            }

            var fileInfo = new FileInfo(binaryPath);
            Assert.IsTrue(fileInfo.Length > 0, "NativeAOT binary is empty");

            // Binary should be less than 10MB for a minimal test app
            Assert.IsTrue(fileInfo.Length < 10 * 1024 * 1024,
                $"NativeAOT binary too large: {fileInfo.Length / 1024 / 1024}MB");
        }

        [TestMethod]
        [TestCategory("NativeAOT")]
        public void NativeAOT_BinarySizeOptimal()
        {
            var binaryPath = GetAotBinaryPath();

            if (!File.Exists(binaryPath))
            {
                Assert.Inconclusive("NativeAOT binary not found.");
                return;
            }

            var fileInfo = new FileInfo(binaryPath);
            var sizeMB = fileInfo.Length / (1024.0 * 1024.0);

            // Target: < 5MB for acceptance criteria
            Console.WriteLine($"NativeAOT binary size: {sizeMB:F2} MB");

            Assert.IsTrue(sizeMB < 5.0,
                $"NativeAOT binary exceeds 5MB target: {sizeMB:F2}MB");

            if (sizeMB > 3.0)
            {
                Console.WriteLine($"WARNING: Binary size ({sizeMB:F2}MB) exceeds 3MB. Consider optimization.");
            }
        }

        [TestMethod]
        [TestCategory("NativeAOT")]
        [TestCategory("Integration")]
        public void NativeAOT_ExecutionSucceeds()
        {
            var binaryPath = GetAotBinaryPath();

            if (!File.Exists(binaryPath))
            {
                Assert.Inconclusive("NativeAOT binary not found.");
                return;
            }

            // Skip on Windows if binary is Linux
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && binaryPath.Contains("linux"))
            {
                Assert.Inconclusive("Cannot run Linux binary on Windows.");
                return;
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = binaryPath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            Assert.IsNotNull(process, "Failed to start NativeAOT process");

            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();

            process.WaitForExit(30000); // 30 second timeout

            Console.WriteLine("=== NativeAOT Output ===");
            Console.WriteLine(output);

            if (!string.IsNullOrEmpty(error))
            {
                Console.WriteLine("=== NativeAOT Errors ===");
                Console.WriteLine(error);
            }

            Assert.AreEqual(0, process.ExitCode,
                $"NativeAOT process failed with exit code {process.ExitCode}");
            Assert.IsTrue(output.Contains("[SUCCESS]"),
                "NativeAOT tests did not report success");
        }

        [TestMethod]
        [TestCategory("NativeAOT")]
        [TestCategory("Integration")]
        public void NativeAOT_StartupTimeAcceptable()
        {
            var binaryPath = GetAotBinaryPath();

            if (!File.Exists(binaryPath))
            {
                Assert.Inconclusive("NativeAOT binary not found.");
                return;
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && binaryPath.Contains("linux"))
            {
                Assert.Inconclusive("Cannot run Linux binary on Windows.");
                return;
            }

            var stopwatch = Stopwatch.StartNew();

            var startInfo = new ProcessStartInfo
            {
                FileName = binaryPath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            Assert.IsNotNull(process);

            process.WaitForExit(30000);
            stopwatch.Stop();

            var totalMs = stopwatch.ElapsedMilliseconds;
            Console.WriteLine($"NativeAOT total execution time: {totalMs}ms");

            // Target: < 5000ms for full test suite execution
            Assert.IsTrue(totalMs < 5000,
                $"NativeAOT execution took too long: {totalMs}ms");

            if (totalMs < 100)
            {
                Console.WriteLine("EXCELLENT: Execution completed in under 100ms");
            }
            else if (totalMs < 500)
            {
                Console.WriteLine("GOOD: Execution completed in under 500ms");
            }
        }

        [TestMethod]
        [TestCategory("NativeAOT")]
        [TestCategory("Integration")]
        public void NativeAOT_AllTestGroupsPass()
        {
            var binaryPath = GetAotBinaryPath();

            if (!File.Exists(binaryPath))
            {
                Assert.Inconclusive("NativeAOT binary not found.");
                return;
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && binaryPath.Contains("linux"))
            {
                Assert.Inconclusive("Cannot run Linux binary on Windows.");
                return;
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = binaryPath,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            Assert.IsNotNull(process);

            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(30000);

            // Verify all test groups are present
            var requiredGroups = new[]
            {
                "--- Core Types ---",
                "--- Cryptography ---",
                "--- JSON ---",
                "--- Storage ---",
                "--- Protocol Types ---",
                "--- IO Extensions ---",
                "--- Edge Cases ---"
            };

            foreach (var group in requiredGroups)
            {
                Assert.IsTrue(output.Contains(group),
                    $"Missing test group: {group}");
            }

            // Verify no failures
            Assert.IsTrue(output.Contains("Tests Failed: 0"),
                "Some NativeAOT tests failed");

            // Extract test count
            var passedMatch = Regex.Match(output, @"Tests Passed: (\d+)");

            if (passedMatch.Success)
            {
                var passedCount = int.Parse(passedMatch.Groups[1].Value);
                Console.WriteLine($"NativeAOT tests passed: {passedCount}");
                Assert.IsTrue(passedCount >= 20,
                    $"Expected at least 20 tests, got {passedCount}");
            }
        }

        [TestMethod]
        [TestCategory("NativeAOT")]
        public void NativeAOT_ProjectConfigurationValid()
        {
            var csprojPath = Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory,
                "..", "..", "..", "..", "..",
                "src", "Neo.Node.AOT", "Neo.Node.AOT.csproj"));

            if (!File.Exists(csprojPath))
            {
                Assert.Inconclusive($"Neo.Node.AOT.csproj not found at {csprojPath}.");
                return;
            }

            var content = File.ReadAllText(csprojPath);

            // Verify essential NativeAOT settings
            Assert.IsTrue(content.Contains("<PublishAot>true</PublishAot>"),
                "Missing PublishAot setting");

            Assert.IsTrue(content.Contains("<PublishTrimmed>true</PublishTrimmed>"),
                "Missing PublishTrimmed setting");

            Assert.IsTrue(content.Contains("<EnableAotAnalyzer>true</EnableAotAnalyzer>"),
                "Missing EnableAotAnalyzer setting");

            Console.WriteLine("NativeAOT project configuration is valid");
        }

        [TestMethod]
        [TestCategory("NativeAOT")]
        public void NativeAOT_NoRuntimeConfigTemplateFiles()
        {
            var srcPath = Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory,
                "..", "..", "..", "..", "..",
                "src", "Neo.Node.AOT"));

            if (!Directory.Exists(srcPath))
            {
                Assert.Inconclusive($"Neo.Node.AOT directory not found at {srcPath}.");
                return;
            }

            var templateFiles = Directory.GetFiles(srcPath, "runtimeconfig.template.json",
                SearchOption.TopDirectoryOnly);

            Assert.AreEqual(0, templateFiles.Length,
                $"Found runtimeconfig.template.json files that should be removed: {string.Join(", ", templateFiles)}");
        }
    }
}
