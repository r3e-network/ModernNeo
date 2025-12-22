// Copyright (C) 2015-2025 The Neo Project.
//
// WalletServicesImpl.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Core.Interfaces;
using Neo.Extensions;
using Neo.Persistence;
using Neo.SmartContract.Native;
using Neo.VM;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Neo.SmartContract
{
    /// <summary>
    /// Implementation of <see cref="IWalletServices"/> that bridges to SmartContract internals.
    /// </summary>
    public class WalletServicesImpl : IWalletServices
    {
        private readonly DataCache _snapshot;
        private readonly ProtocolSettings _settings;

        public IBalanceService Balance { get; }
        public IFeeCalculator Fees { get; }
        public IScriptExecutor Scripts { get; }
        public IContractService Contracts { get; }

        public WalletServicesImpl(DataCache snapshot, ProtocolSettings settings)
        {
            _snapshot = snapshot;
            _settings = settings;
            Balance = new BalanceServiceImpl(snapshot, settings);
            Fees = new FeeCalculatorImpl(snapshot, settings);
            Scripts = new ScriptExecutorImpl(snapshot, settings);
            Contracts = new ContractServiceImpl(snapshot, settings);
        }
    }

    /// <summary>
    /// Implementation of <see cref="IBalanceService"/> using NativeContract.GAS.
    /// </summary>
    internal class BalanceServiceImpl : IBalanceService
    {
        private readonly DataCache _snapshot;
        private readonly ProtocolSettings _settings;

        public BalanceServiceImpl(DataCache snapshot, ProtocolSettings settings)
        {
            _snapshot = snapshot;
            _settings = settings;
        }

        public long GetGasBalance(byte[] accountHash)
        {
            var hash = new UInt160(accountHash);
            return (long)NativeContract.GAS.BalanceOf(_snapshot, hash);
        }

        public long GetTokenBalance(byte[] tokenHash, byte[] accountHash)
        {
            var token = new UInt160(tokenHash);
            var account = new UInt160(accountHash);

            using var sb = new ScriptBuilder();
            sb.EmitDynamicCall(token, "balanceOf", account);

            using var engine = ApplicationEngine.Run(sb.ToArray(), _snapshot, settings: _settings, gas: 0_20000000L);
            if (engine.State != VMState.HALT || engine.ResultStack.Count == 0)
                return 0;

            return (long)engine.ResultStack.Pop().GetInteger();
        }

        public IReadOnlyDictionary<byte[], long> GetGasBalances(IEnumerable<byte[]> accountHashes)
        {
            var result = new Dictionary<byte[], long>(new ByteArrayComparer());
            foreach (var hash in accountHashes)
            {
                result[hash] = GetGasBalance(hash);
            }
            return result;
        }

        private class ByteArrayComparer : IEqualityComparer<byte[]>
        {
            public bool Equals(byte[]? x, byte[]? y)
            {
                if (x is null || y is null) return x == y;
                return x.SequenceEqual(y);
            }

            public int GetHashCode(byte[] obj)
            {
                if (obj is null || obj.Length == 0) return 0;
                int hash = 17;
                foreach (byte b in obj)
                    hash = hash * 31 + b;
                return hash;
            }
        }
    }

    /// <summary>
    /// Implementation of <see cref="IFeeCalculator"/> using Policy contract.
    /// </summary>
    internal class FeeCalculatorImpl : IFeeCalculator
    {
        private readonly DataCache _snapshot;
        private readonly ProtocolSettings _settings;

        public FeeCalculatorImpl(DataCache snapshot, ProtocolSettings settings)
        {
            _snapshot = snapshot;
            _settings = settings;
        }

        public long CalculateNetworkFee(int transactionSize, IEnumerable<byte[]> verificationScriptHashes)
        {
            long fee = transactionSize * GetFeePerByte();
            // Additional verification costs would be calculated here
            return fee;
        }

        public long GetFeePerByte()
        {
            return NativeContract.Policy.GetFeePerByte(_snapshot);
        }

        public uint GetExecFeeFactor()
        {
            var currentIndex = NativeContract.Ledger.CurrentIndex(_snapshot);
            return (uint)NativeContract.Policy.GetExecFeeFactor(_settings, _snapshot, currentIndex + 1);
        }

        public long EstimateSystemFee(byte[] script, IEnumerable<ISignerData>? signers = null)
        {
            using var engine = ApplicationEngine.Run(script, _snapshot, settings: _settings, gas: ApplicationEngine.TestModeGas);
            if (engine.State != VMState.HALT)
                return -1;
            return engine.FeeConsumed;
        }
    }

    /// <summary>
    /// Implementation of <see cref="IScriptExecutor"/> using ApplicationEngine.
    /// </summary>
    internal class ScriptExecutorImpl : IScriptExecutor
    {
        private readonly DataCache _snapshot;
        private readonly ProtocolSettings _settings;

        public ScriptExecutorImpl(DataCache snapshot, ProtocolSettings settings)
        {
            _snapshot = snapshot;
            _settings = settings;
        }

        public IScriptExecutionResult? Execute(byte[] script, long gasLimit = 20_00000000)
        {
            using var engine = ApplicationEngine.Run(script, _snapshot, settings: _settings, gas: gasLimit);
            return new ScriptExecutionResultImpl(engine);
        }

        public IScriptExecutionResult? ExecuteWithTransaction(byte[] script, ITransactionData transaction, long gasLimit)
        {
            // Transaction context execution requires actual Transaction object
            // This simplified implementation runs without transaction context
            return Execute(script, gasLimit);
        }

        public uint GetCurrentBlockIndex()
        {
            return NativeContract.Ledger.CurrentIndex(_snapshot);
        }

        public uint GetMaxValidUntilBlockIncrement()
        {
            return _snapshot.GetMaxValidUntilBlockIncrement(_settings);
        }
    }

    /// <summary>
    /// Implementation of <see cref="IScriptExecutionResult"/>.
    /// </summary>
    internal class ScriptExecutionResultImpl : IScriptExecutionResult
    {
        public bool Success { get; }
        public long GasConsumed { get; }
        public string? ExceptionMessage { get; }
        public IReadOnlyList<byte[]> ResultStack { get; }

        public ScriptExecutionResultImpl(ApplicationEngine engine)
        {
            Success = engine.State == VMState.HALT;
            GasConsumed = engine.FeeConsumed;
            ExceptionMessage = engine.FaultException?.Message;
            ResultStack = engine.ResultStack
                .Select(item => item.GetSpan().ToArray())
                .ToList();
        }
    }

    /// <summary>
    /// Implementation of <see cref="IContractService"/> using Contract class.
    /// </summary>
    internal class ContractServiceImpl : IContractService
    {
        private readonly DataCache _snapshot;
        private readonly ProtocolSettings _settings;

        public ContractServiceImpl(DataCache snapshot, ProtocolSettings settings)
        {
            _snapshot = snapshot;
            _settings = settings;
        }

        public byte[] CreateSignatureRedeemScript(byte[] publicKey)
        {
            var pubKey = Cryptography.ECC.ECPoint.DecodePoint(publicKey, Cryptography.ECC.ECCurve.Secp256r1);
            return Contract.CreateSignatureRedeemScript(pubKey);
        }

        public byte[] CreateMultiSigRedeemScript(int m, IEnumerable<byte[]> publicKeys)
        {
            var keys = publicKeys
                .Select(pk => Cryptography.ECC.ECPoint.DecodePoint(pk, Cryptography.ECC.ECCurve.Secp256r1))
                .ToArray();
            return Contract.CreateMultiSigRedeemScript(m, keys);
        }

        public bool IsMultiSigContract(byte[] script, out int m, out byte[][]? publicKeys)
        {
            var result = Neo.SmartContract.Helper.IsMultiSigContract(script, out m, out Cryptography.ECC.ECPoint[]? points);
            publicKeys = points?.Select(p => p.EncodePoint(true)).ToArray();
            return result;
        }

        public bool IsSignatureContract(byte[] script)
        {
            return Neo.SmartContract.Helper.IsSignatureContract(script);
        }

        public byte[] GetScriptHash(byte[] script)
        {
            return script.ToScriptHash().GetSpan().ToArray();
        }

        public IContractInfo? GetContract(byte[] scriptHash)
        {
            var hash = new UInt160(scriptHash);
            var contract = NativeContract.ContractManagement.GetContract(_snapshot, hash);
            if (contract is null)
                return null;
            return new ContractInfoImpl(contract);
        }

        public byte[] GetGasContractHash()
        {
            return NativeContract.GAS.Hash.GetSpan().ToArray();
        }

        public byte[] GetNeoContractHash()
        {
            return NativeContract.NEO.Hash.GetSpan().ToArray();
        }
    }

    /// <summary>
    /// Implementation of <see cref="IContractInfo"/>.
    /// </summary>
    internal class ContractInfoImpl : IContractInfo
    {
        public byte[] Hash { get; }
        public byte[] Script { get; }
        public string Name { get; }

        public ContractInfoImpl(ContractState contract)
        {
            Hash = contract.Hash.GetSpan().ToArray();
            Script = contract.Nef.Script.ToArray();
            Name = contract.Manifest.Name;
        }
    }
}
