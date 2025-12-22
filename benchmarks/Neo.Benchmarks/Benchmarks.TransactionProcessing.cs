// Copyright (C) 2015-2025 The Neo Project.
//
// Benchmarks.TransactionProcessing.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

#nullable enable

using BenchmarkDotNet.Attributes;
using Neo.Cryptography;
using Neo.Extensions;
using Neo.IO;
using Neo.Ledger;
using Neo.Network.P2P;
using Neo.Network.P2P.Payloads;
using Neo.SmartContract;
using Neo.VM;

namespace Neo.Benchmarks
{
    /// <summary>
    /// Transaction serialization benchmarks.
    /// </summary>
    [MemoryDiagnoser]
    [RankColumn]
    public class TransactionSerializationBenchmarks
    {
        private Transaction _transaction = null!;
        private byte[] _serializedTransaction = null!;

        [GlobalSetup]
        public void Setup()
        {
            _transaction = new Transaction
            {
                Version = 0,
                Nonce = 123456,
                SystemFee = 1000000,
                NetworkFee = 100000,
                ValidUntilBlock = 1000,
                Signers = new[]
                {
                    new Signer
                    {
                        Account = UInt160.Parse("0xabcdef0123456789abcdef0123456789abcdef01"),
                        Scopes = WitnessScope.CalledByEntry
                    }
                },
                Attributes = Array.Empty<TransactionAttribute>(),
                Script = new byte[] { (byte)OpCode.PUSH1, (byte)OpCode.RET },
                Witnesses = new[]
                {
                    new Witness
                    {
                        InvocationScript = new byte[64],
                        VerificationScript = new byte[32]
                    }
                }
            };

            _serializedTransaction = _transaction.ToArray();
        }

        [Benchmark(Baseline = true)]
        public byte[] Serialize_Transaction() => _transaction.ToArray();

        [Benchmark]
        public Transaction Deserialize_Transaction() => _serializedTransaction.AsSerializable<Transaction>();

        [Benchmark]
        public int GetTransactionSize() => _transaction.Size;
    }

    /// <summary>
    /// Transaction hash computation benchmarks.
    /// </summary>
    [MemoryDiagnoser]
    [RankColumn]
    public class TransactionHashBenchmarks
    {
        private Transaction _transaction = null!;

        [GlobalSetup]
        public void Setup()
        {
            _transaction = new Transaction
            {
                Version = 0,
                Nonce = 123456,
                SystemFee = 1000000,
                NetworkFee = 100000,
                ValidUntilBlock = 1000,
                Signers = new[]
                {
                    new Signer
                    {
                        Account = UInt160.Parse("0xabcdef0123456789abcdef0123456789abcdef01"),
                        Scopes = WitnessScope.CalledByEntry
                    }
                },
                Attributes = Array.Empty<TransactionAttribute>(),
                Script = new byte[] { (byte)OpCode.PUSH1, (byte)OpCode.RET },
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

        [Benchmark(Baseline = true)]
        public UInt256 ComputeTransactionHash() => _transaction.Hash;

        [Benchmark]
        public byte[] GetSignData() => _transaction.GetSignData(ProtocolSettings.Default.Network);
    }

    /// <summary>
    /// Transaction state-independent verification benchmarks.
    /// </summary>
    [MemoryDiagnoser]
    [RankColumn]
    public class TransactionVerificationBenchmarks
    {
        private Transaction _transaction = null!;
        private ProtocolSettings _settings = null!;

        [GlobalSetup]
        public void Setup()
        {
            _settings = ProtocolSettings.Default;

            _transaction = new Transaction
            {
                Version = 0,
                Nonce = 123456,
                SystemFee = 1000000,
                NetworkFee = 100000,
                ValidUntilBlock = 1000,
                Signers = new[]
                {
                    new Signer
                    {
                        Account = UInt160.Parse("0xabcdef0123456789abcdef0123456789abcdef01"),
                        Scopes = WitnessScope.CalledByEntry
                    }
                },
                Attributes = Array.Empty<TransactionAttribute>(),
                Script = new byte[] { (byte)OpCode.PUSH1, (byte)OpCode.RET },
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

        [Benchmark(Baseline = true)]
        public VerifyResult VerifyStateIndependent() => _transaction.VerifyStateIndependent(_settings);

        [Benchmark]
        public int GetTransactionSize() => _transaction.Size;
    }

    /// <summary>
    /// Batch transaction processing benchmarks.
    /// </summary>
    [MemoryDiagnoser]
    [RankColumn]
    public class BatchTransactionBenchmarks
    {
        private Transaction[] _transactions = null!;
        private const int BatchSize = 100;

        [GlobalSetup]
        public void Setup()
        {
            _transactions = new Transaction[BatchSize];
            for (int i = 0; i < BatchSize; i++)
            {
                _transactions[i] = new Transaction
                {
                    Version = 0,
                    Nonce = (uint)i,
                    SystemFee = 1000000,
                    NetworkFee = 100000,
                    ValidUntilBlock = 1000,
                    Signers = new[]
                    {
                        new Signer
                        {
                            Account = UInt160.Parse("0xabcdef0123456789abcdef0123456789abcdef01"),
                            Scopes = WitnessScope.CalledByEntry
                        }
                    },
                    Attributes = Array.Empty<TransactionAttribute>(),
                    Script = new byte[] { (byte)OpCode.PUSH1, (byte)OpCode.RET },
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
        }

        [Benchmark(Baseline = true)]
        public byte[][] Batch_Serialize()
        {
            var results = new byte[BatchSize][];
            for (int i = 0; i < BatchSize; i++)
            {
                results[i] = _transactions[i].ToArray();
            }
            return results;
        }

        [Benchmark]
        public UInt256[] Batch_ComputeHashes()
        {
            var results = new UInt256[BatchSize];
            for (int i = 0; i < BatchSize; i++)
            {
                results[i] = _transactions[i].Hash;
            }
            return results;
        }

        [Benchmark]
        public VerifyResult[] Batch_VerifyStateIndependent()
        {
            var results = new VerifyResult[BatchSize];
            var settings = ProtocolSettings.Default;
            for (int i = 0; i < BatchSize; i++)
            {
                results[i] = _transactions[i].VerifyStateIndependent(settings);
            }
            return results;
        }
    }

    /// <summary>
    /// Signer and witness benchmarks.
    /// </summary>
    [MemoryDiagnoser]
    [RankColumn]
    public class SignerWitnessBenchmarks
    {
        private Signer _signer = null!;
        private Witness _witness = null!;
        private byte[] _serializedSigner = null!;
        private byte[] _serializedWitness = null!;

        [GlobalSetup]
        public void Setup()
        {
            _signer = new Signer
            {
                Account = UInt160.Parse("0xabcdef0123456789abcdef0123456789abcdef01"),
                Scopes = WitnessScope.CalledByEntry
            };

            _witness = new Witness
            {
                InvocationScript = new byte[64],
                VerificationScript = new byte[32]
            };

            _serializedSigner = _signer.ToArray();
            _serializedWitness = _witness.ToArray();
        }

        [Benchmark(Baseline = true)]
        public byte[] Serialize_Signer() => _signer.ToArray();

        [Benchmark]
        public Signer Deserialize_Signer() => _serializedSigner.AsSerializable<Signer>();

        [Benchmark]
        public byte[] Serialize_Witness() => _witness.ToArray();

        [Benchmark]
        public Witness Deserialize_Witness() => _serializedWitness.AsSerializable<Witness>();
    }
}
