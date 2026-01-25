// Copyright (C) 2015-2025 The Neo Project.
//
// PerformanceRegressionTests.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.Cryptography;
using Neo.Extensions;
using Neo.IO;
using Neo.Network.P2P.Payloads;
using System.Diagnostics;

namespace Neo.PerformanceTests
{
    /// <summary>
    /// Performance regression tests that compare current performance against baselines.
    /// </summary>
    [TestClass]
    public class PerformanceRegressionTests
    {
        private const string BaselineDirectory = "baselines";
        private const string BaselineFileName = "performance-baseline.json";
        private const int WarmupIterations = 100;
        private const int MeasureIterations = 1000;

        private static readonly string BaselinePath = Path.Combine(
            AppContext.BaseDirectory, BaselineDirectory, BaselineFileName);

        private PerformanceBaseline? _baseline;
        private readonly Dictionary<string, BenchmarkResult> _currentResults = new();
        private readonly PerformanceComparer _comparer = new() { RegressionThreshold = 0.10 };

        [TestInitialize]
        public void Setup()
        {
            _baseline = PerformanceBaseline.Load(BaselinePath);
            _currentResults.Clear();
        }

        [TestCleanup]
        public void Cleanup()
        {
            // If no baseline exists, create one from current results
            if (_baseline == null && _currentResults.Count > 0)
            {
                var newBaseline = new PerformanceBaseline
                {
                    Machine = MachineInfo.Current,
                    Benchmarks = _currentResults
                };
                newBaseline.Save(BaselinePath);
                Console.WriteLine($"Created new baseline at: {BaselinePath}");
            }
        }

        [TestMethod]
        [TestCategory("Performance")]
        public void Serialization_Transaction_NoRegression()
        {
            var tx = CreateTestTransaction();
            var bytes = tx.ToArray();

            // Warmup
            for (int i = 0; i < WarmupIterations; i++)
            {
                _ = tx.ToArray();
                _ = bytes.AsSerializable<Transaction>();
            }

            // Measure serialization
            var serializeResult = MeasureOperation("Transaction.Serialize", () => tx.ToArray());
            _currentResults["Transaction.Serialize"] = serializeResult;

            // Measure deserialization
            var deserializeResult = MeasureOperation("Transaction.Deserialize", () => bytes.AsSerializable<Transaction>());
            _currentResults["Transaction.Deserialize"] = deserializeResult;

            // Check for regressions
            if (_baseline != null)
            {
                var results = _comparer.Compare(_baseline, _currentResults);
                AssertNoRegressions(results);
            }
        }

        [TestMethod]
        [TestCategory("Performance")]
        public void Serialization_Block_NoRegression()
        {
            var block = CreateTestBlock(10);
            var bytes = block.ToArray();

            // Warmup
            for (int i = 0; i < WarmupIterations; i++)
            {
                _ = block.ToArray();
                _ = bytes.AsSerializable<Block>();
            }

            // Measure serialization
            var serializeResult = MeasureOperation("Block.Serialize", () => block.ToArray());
            _currentResults["Block.Serialize"] = serializeResult;

            // Measure deserialization
            var deserializeResult = MeasureOperation("Block.Deserialize", () => bytes.AsSerializable<Block>());
            _currentResults["Block.Deserialize"] = deserializeResult;

            // Check for regressions
            if (_baseline != null)
            {
                var results = _comparer.Compare(_baseline, _currentResults);
                AssertNoRegressions(results);
            }
        }

        [TestMethod]
        [TestCategory("Performance")]
        public void Hashing_Sha256_NoRegression()
        {
            var data = new byte[256];
            Random.Shared.NextBytes(data);

            // Warmup
            for (int i = 0; i < WarmupIterations; i++)
            {
                _ = data.Sha256();
            }

            // Measure
            var result = MeasureOperation("Sha256.256bytes", () => data.Sha256());
            _currentResults["Sha256.256bytes"] = result;

            // Check for regressions
            if (_baseline != null)
            {
                var results = _comparer.Compare(_baseline, _currentResults);
                AssertNoRegressions(results);
            }
        }

        [TestMethod]
        [TestCategory("Performance")]
        public void Hashing_Hash256_NoRegression()
        {
            var data = new byte[256];
            Random.Shared.NextBytes(data);

            // Warmup
            for (int i = 0; i < WarmupIterations; i++)
            {
                _ = Crypto.Hash256(data.AsSpan());
            }

            // Measure
            var result = MeasureOperation("Hash256.256bytes", () => Crypto.Hash256(data.AsSpan()));
            _currentResults["Hash256.256bytes"] = result;

            // Check for regressions
            if (_baseline != null)
            {
                var results = _comparer.Compare(_baseline, _currentResults);
                AssertNoRegressions(results);
            }
        }

        [TestMethod]
        [TestCategory("Performance")]
        public void Hashing_Hash160_NoRegression()
        {
            var data = new byte[256];
            Random.Shared.NextBytes(data);

            // Warmup
            for (int i = 0; i < WarmupIterations; i++)
            {
                _ = Crypto.Hash160(data.AsSpan());
            }

            // Measure
            var result = MeasureOperation("Hash160.256bytes", () => Crypto.Hash160(data.AsSpan()));
            _currentResults["Hash160.256bytes"] = result;

            // Check for regressions
            if (_baseline != null)
            {
                var results = _comparer.Compare(_baseline, _currentResults);
                AssertNoRegressions(results);
            }
        }

        private static BenchmarkResult MeasureOperation<T>(string name, Func<T> operation)
        {
            var measurements = new List<double>();
            var sw = new Stopwatch();

            for (int i = 0; i < MeasureIterations; i++)
            {
                sw.Restart();
                _ = operation();
                sw.Stop();
                measurements.Add(sw.Elapsed.TotalNanoseconds);
            }

            var mean = measurements.Average();
            var stdDev = Math.Sqrt(measurements.Select(x => Math.Pow(x - mean, 2)).Average());

            return new BenchmarkResult
            {
                MeanNs = mean,
                StdDevNs = stdDev,
                Iterations = MeasureIterations
            };
        }

        private static void AssertNoRegressions(IReadOnlyList<RegressionResult> results)
        {
            var regressions = results.Where(r => r.IsRegression).ToList();

            if (regressions.Count > 0)
            {
                var message = string.Join(Environment.NewLine,
                    regressions.Select(r => $"  {r.BenchmarkName}: {r.Message}"));

                Assert.Fail($"Performance regressions detected:{Environment.NewLine}{message}");
            }

            // Log all results for visibility
            foreach (var result in results)
            {
                Console.WriteLine($"{result.BenchmarkName}: {result.Message}");
            }
        }

        private static Transaction CreateTestTransaction()
        {
            return new Transaction
            {
                Version = 0,
                Nonce = 12345678,
                SystemFee = 1000000,
                NetworkFee = 100000,
                ValidUntilBlock = 1000000,
                Signers = new[]
                {
                    new Signer
                    {
                        Account = UInt160.Zero,
                        Scopes = WitnessScope.CalledByEntry
                    }
                },
                Attributes = Array.Empty<TransactionAttribute>(),
                Script = new byte[100],
                Witnesses = new[]
                {
                    new Witness
                    {
                        InvocationScript = new byte[64],
                        VerificationScript = new byte[32]
                    }
                }
            };
        }

        private static Block CreateTestBlock(int txCount)
        {
            var transactions = new Transaction[txCount];
            for (int i = 0; i < txCount; i++)
            {
                var accountBytes = new byte[20];
                accountBytes[0] = (byte)i;

                transactions[i] = new Transaction
                {
                    Version = 0,
                    Nonce = (uint)(12345678 + i),
                    SystemFee = 1000000,
                    NetworkFee = 100000,
                    ValidUntilBlock = 1000000,
                    Signers = new[]
                    {
                        new Signer
                        {
                            Account = new UInt160(accountBytes),
                            Scopes = WitnessScope.CalledByEntry
                        }
                    },
                    Attributes = Array.Empty<TransactionAttribute>(),
                    Script = new byte[100],
                    Witnesses = new[]
                    {
                        new Witness
                        {
                            InvocationScript = new byte[64],
                            VerificationScript = new byte[32]
                        }
                    }
                };
            }

            // Compute correct Merkle root from transaction hashes
            var merkleRoot = MerkleTree.ComputeRoot(transactions.Select(t => t.Hash).ToArray());

            return new Block
            {
                Header = new Header
                {
                    Version = 0,
                    PrevHash = UInt256.Zero,
                    MerkleRoot = merkleRoot,
                    Timestamp = 1700000000000,
                    Nonce = 12345,
                    Index = 100,
                    PrimaryIndex = 0,
                    NextConsensus = UInt160.Zero,
                    Witness = new Witness
                    {
                        InvocationScript = new byte[64],
                        VerificationScript = new byte[32]
                    }
                },
                Transactions = transactions
            };
        }
    }
}
