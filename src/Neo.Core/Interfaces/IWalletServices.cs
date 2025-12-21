// Copyright (C) 2015-2025 The Neo Project.
//
// IWalletServices.cs file belongs to the neo project and is free
// software distributed under the MIT software license.

using System;
using System.Collections.Generic;

namespace Neo.Core.Interfaces;

/// <summary>
/// Service interface for querying account balances.
/// Abstracts NativeContract.GAS/NEP17 balance queries from wallet implementations.
/// </summary>
public interface IBalanceService
{
    /// <summary>
    /// Gets the GAS balance for the specified account.
    /// </summary>
    /// <param name="accountHash">The account script hash.</param>
    /// <returns>The GAS balance in fixed-point format.</returns>
    long GetGasBalance(byte[] accountHash);

    /// <summary>
    /// Gets the NEP-17 token balance for the specified account.
    /// </summary>
    /// <param name="tokenHash">The token contract hash.</param>
    /// <param name="accountHash">The account script hash.</param>
    /// <returns>The token balance.</returns>
    long GetTokenBalance(byte[] tokenHash, byte[] accountHash);

    /// <summary>
    /// Gets balances for multiple accounts.
    /// </summary>
    /// <param name="accountHashes">The account script hashes.</param>
    /// <returns>Dictionary mapping account hash to GAS balance.</returns>
    IReadOnlyDictionary<byte[], long> GetGasBalances(IEnumerable<byte[]> accountHashes);
}

/// <summary>
/// Service interface for calculating transaction fees.
/// Abstracts ApplicationEngine and Policy contract queries.
/// </summary>
public interface IFeeCalculator
{
    /// <summary>
    /// Calculates the network fee for a transaction.
    /// </summary>
    /// <param name="transactionSize">The transaction size in bytes.</param>
    /// <param name="verificationScriptHashes">Script hashes requiring verification.</param>
    /// <returns>The calculated network fee.</returns>
    long CalculateNetworkFee(int transactionSize, IEnumerable<byte[]> verificationScriptHashes);

    /// <summary>
    /// Gets the current fee per byte from policy.
    /// </summary>
    /// <returns>The fee per byte.</returns>
    long GetFeePerByte();

    /// <summary>
    /// Gets the execution fee factor from policy.
    /// </summary>
    /// <returns>The execution fee factor.</returns>
    uint GetExecFeeFactor();

    /// <summary>
    /// Estimates the system fee for a script.
    /// </summary>
    /// <param name="script">The script to estimate.</param>
    /// <param name="signers">The transaction signers.</param>
    /// <returns>The estimated system fee, or -1 if execution fails.</returns>
    long EstimateSystemFee(byte[] script, IEnumerable<ISignerData>? signers = null);
}

/// <summary>
/// Service interface for script execution and building.
/// Abstracts ApplicationEngine.Run and ScriptBuilder operations.
/// </summary>
public interface IScriptExecutor
{
    /// <summary>
    /// Executes a script and returns the result.
    /// </summary>
    /// <param name="script">The script to execute.</param>
    /// <param name="gasLimit">Maximum gas for execution.</param>
    /// <returns>The execution result, or null if failed.</returns>
    IScriptExecutionResult? Execute(byte[] script, long gasLimit = 20_00000000);

    /// <summary>
    /// Executes a script in the context of a transaction.
    /// </summary>
    /// <param name="script">The script to execute.</param>
    /// <param name="transaction">The transaction context.</param>
    /// <param name="gasLimit">Maximum gas for execution.</param>
    /// <returns>The execution result, or null if failed.</returns>
    IScriptExecutionResult? ExecuteWithTransaction(byte[] script, ITransactionData transaction, long gasLimit);

    /// <summary>
    /// Gets the current block index from the ledger.
    /// </summary>
    /// <returns>The current block index.</returns>
    uint GetCurrentBlockIndex();

    /// <summary>
    /// Gets the maximum valid until block increment.
    /// </summary>
    /// <returns>The max valid until block increment.</returns>
    uint GetMaxValidUntilBlockIncrement();
}

/// <summary>
/// Result of script execution.
/// </summary>
public interface IScriptExecutionResult
{
    /// <summary>
    /// Gets whether the execution was successful.
    /// </summary>
    bool Success { get; }

    /// <summary>
    /// Gets the gas consumed by execution.
    /// </summary>
    long GasConsumed { get; }

    /// <summary>
    /// Gets the exception message if execution failed.
    /// </summary>
    string? ExceptionMessage { get; }

    /// <summary>
    /// Gets the result stack items as byte arrays.
    /// </summary>
    IReadOnlyList<byte[]> ResultStack { get; }
}

/// <summary>
/// Service interface for contract-related operations.
/// Abstracts Contract class methods and ContractManagement queries.
/// </summary>
public interface IContractService
{
    /// <summary>
    /// Creates a signature redeem script for the given public key.
    /// </summary>
    /// <param name="publicKey">The public key bytes.</param>
    /// <returns>The signature redeem script.</returns>
    byte[] CreateSignatureRedeemScript(byte[] publicKey);

    /// <summary>
    /// Creates a multi-signature redeem script.
    /// </summary>
    /// <param name="m">The minimum number of signatures required.</param>
    /// <param name="publicKeys">The public keys.</param>
    /// <returns>The multi-sig redeem script.</returns>
    byte[] CreateMultiSigRedeemScript(int m, IEnumerable<byte[]> publicKeys);

    /// <summary>
    /// Checks if a script is a multi-signature contract.
    /// </summary>
    /// <param name="script">The script to check.</param>
    /// <param name="m">Output: minimum signatures required.</param>
    /// <param name="publicKeys">Output: the public keys.</param>
    /// <returns>True if multi-sig contract.</returns>
    bool IsMultiSigContract(byte[] script, out int m, out byte[][]? publicKeys);

    /// <summary>
    /// Checks if a script is a signature contract.
    /// </summary>
    /// <param name="script">The script to check.</param>
    /// <returns>True if signature contract.</returns>
    bool IsSignatureContract(byte[] script);

    /// <summary>
    /// Gets the script hash for a script.
    /// </summary>
    /// <param name="script">The script.</param>
    /// <returns>The script hash as byte array.</returns>
    byte[] GetScriptHash(byte[] script);

    /// <summary>
    /// Gets contract information by script hash.
    /// </summary>
    /// <param name="scriptHash">The contract script hash.</param>
    /// <returns>Contract info, or null if not found.</returns>
    IContractInfo? GetContract(byte[] scriptHash);

    /// <summary>
    /// Gets the GAS native contract script hash.
    /// </summary>
    /// <returns>The GAS contract hash.</returns>
    byte[] GetGasContractHash();

    /// <summary>
    /// Gets the NEO native contract script hash.
    /// </summary>
    /// <returns>The NEO contract hash.</returns>
    byte[] GetNeoContractHash();
}

/// <summary>
/// Basic contract information.
/// </summary>
public interface IContractInfo
{
    /// <summary>
    /// Gets the contract script hash.
    /// </summary>
    byte[] Hash { get; }

    /// <summary>
    /// Gets the contract script.
    /// </summary>
    byte[] Script { get; }

    /// <summary>
    /// Gets the contract name.
    /// </summary>
    string Name { get; }
}

/// <summary>
/// Combined wallet services facade for dependency injection.
/// </summary>
public interface IWalletServices
{
    /// <summary>
    /// Gets the balance service.
    /// </summary>
    IBalanceService Balance { get; }

    /// <summary>
    /// Gets the fee calculator.
    /// </summary>
    IFeeCalculator Fees { get; }

    /// <summary>
    /// Gets the script executor.
    /// </summary>
    IScriptExecutor Scripts { get; }

    /// <summary>
    /// Gets the contract service.
    /// </summary>
    IContractService Contracts { get; }
}
