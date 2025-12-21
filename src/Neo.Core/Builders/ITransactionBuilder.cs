// Copyright (C) 2015-2025 The Neo Project.
//
// ITransactionBuilder.cs file belongs to the neo project and is free
// software distributed under the MIT software license.

using Neo.Core.Interfaces;

namespace Neo.Core.Builders;

/// <summary>
/// Dependency-free interface for building transactions.
/// Implementations can use concrete types while consumers depend only on this interface.
/// </summary>
public interface ITransactionBuilder<TTransaction, TSigner, TWitness>
    where TTransaction : ITransactionData
{
    /// <summary>
    /// Sets the transaction version.
    /// </summary>
    ITransactionBuilder<TTransaction, TSigner, TWitness> Version(byte version);

    /// <summary>
    /// Sets the transaction nonce.
    /// </summary>
    ITransactionBuilder<TTransaction, TSigner, TWitness> Nonce(uint nonce);

    /// <summary>
    /// Sets the system fee.
    /// </summary>
    ITransactionBuilder<TTransaction, TSigner, TWitness> SystemFee(long systemFee);

    /// <summary>
    /// Sets the network fee.
    /// </summary>
    ITransactionBuilder<TTransaction, TSigner, TWitness> NetworkFee(long networkFee);

    /// <summary>
    /// Sets the valid until block index.
    /// </summary>
    ITransactionBuilder<TTransaction, TSigner, TWitness> ValidUntil(uint blockIndex);

    /// <summary>
    /// Sets the transaction script.
    /// </summary>
    ITransactionBuilder<TTransaction, TSigner, TWitness> Script(byte[] script);

    /// <summary>
    /// Adds a signer to the transaction.
    /// </summary>
    ITransactionBuilder<TTransaction, TSigner, TWitness> AddSigner(TSigner signer);

    /// <summary>
    /// Adds a witness to the transaction.
    /// </summary>
    ITransactionBuilder<TTransaction, TSigner, TWitness> AddWitness(TWitness witness);

    /// <summary>
    /// Builds the transaction.
    /// </summary>
    TTransaction Build();
}

/// <summary>
/// Dependency-free interface for building witnesses.
/// </summary>
public interface IWitnessBuilder<TWitness>
    where TWitness : IWitness
{
    /// <summary>
    /// Sets the invocation script.
    /// </summary>
    IWitnessBuilder<TWitness> InvocationScript(byte[] script);

    /// <summary>
    /// Sets the verification script.
    /// </summary>
    IWitnessBuilder<TWitness> VerificationScript(byte[] script);

    /// <summary>
    /// Builds the witness.
    /// </summary>
    TWitness Build();
}

/// <summary>
/// Dependency-free interface for building signers.
/// </summary>
public interface ISignerBuilder<TSigner>
    where TSigner : ISignerData
{
    /// <summary>
    /// Sets the account script hash.
    /// </summary>
    ISignerBuilder<TSigner> Account(byte[] scriptHash);

    /// <summary>
    /// Sets the witness scope.
    /// </summary>
    ISignerBuilder<TSigner> Scopes(byte scopes);

    /// <summary>
    /// Adds an allowed contract.
    /// </summary>
    ISignerBuilder<TSigner> AllowContract(byte[] contractHash);

    /// <summary>
    /// Adds an allowed group public key.
    /// </summary>
    ISignerBuilder<TSigner> AllowGroup(byte[] publicKey);

    /// <summary>
    /// Builds the signer.
    /// </summary>
    TSigner Build();
}

/// <summary>
/// Factory interface for creating builders.
/// </summary>
public interface IBuilderFactory<TTransaction, TSigner, TWitness>
    where TTransaction : ITransactionData
    where TSigner : ISignerData
    where TWitness : IWitness
{
    /// <summary>
    /// Creates a new transaction builder.
    /// </summary>
    ITransactionBuilder<TTransaction, TSigner, TWitness> CreateTransactionBuilder();

    /// <summary>
    /// Creates a new witness builder.
    /// </summary>
    IWitnessBuilder<TWitness> CreateWitnessBuilder();

    /// <summary>
    /// Creates a new signer builder.
    /// </summary>
    ISignerBuilder<TSigner> CreateSignerBuilder(byte[] account);
}
