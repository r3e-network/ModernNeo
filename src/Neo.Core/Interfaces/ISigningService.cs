// Copyright (C) 2015-2025 The Neo Project.
//
// ISigningService.cs file belongs to the neo project and is free
// software distributed under the MIT software license.

using System;
using System.Collections.Generic;

namespace Neo.Core.Interfaces;

/// <summary>
/// Dependency-free interface for signing operations.
/// This abstraction allows signing without depending on concrete payload types.
/// </summary>
public interface ISigningService
{
    /// <summary>
    /// Signs the specified data with the private key corresponding to the given public key.
    /// </summary>
    /// <param name="data">The data to sign.</param>
    /// <param name="publicKey">The public key (33 bytes compressed EC point).</param>
    /// <returns>The signature (64 bytes for ECDSA).</returns>
    ReadOnlyMemory<byte> Sign(ReadOnlySpan<byte> data, ReadOnlySpan<byte> publicKey);

    /// <summary>
    /// Signs a block hash with the private key corresponding to the given public key.
    /// </summary>
    /// <param name="blockHash">The block hash (32 bytes).</param>
    /// <param name="publicKey">The public key (33 bytes compressed EC point).</param>
    /// <param name="network">The network magic number.</param>
    /// <returns>The signature (64 bytes for ECDSA).</returns>
    ReadOnlyMemory<byte> SignBlockHash(ReadOnlySpan<byte> blockHash, ReadOnlySpan<byte> publicKey, uint network);

    /// <summary>
    /// Checks if the service can sign with the specified public key.
    /// </summary>
    /// <param name="publicKey">The public key (33 bytes compressed EC point).</param>
    /// <returns>True if the service has the corresponding private key and can sign.</returns>
    bool CanSign(ReadOnlySpan<byte> publicKey);

    /// <summary>
    /// Gets all public keys that this service can sign with.
    /// </summary>
    /// <returns>Collection of public keys (33 bytes each).</returns>
    IEnumerable<byte[]> GetSignablePublicKeys();
}

/// <summary>
/// Dependency-free interface for signature verification.
/// </summary>
public interface ISignatureVerifier
{
    /// <summary>
    /// Verifies a signature against the given data and public key.
    /// </summary>
    /// <param name="data">The original data that was signed.</param>
    /// <param name="signature">The signature to verify (64 bytes for ECDSA).</param>
    /// <param name="publicKey">The public key (33 bytes compressed EC point).</param>
    /// <returns>True if the signature is valid.</returns>
    bool Verify(ReadOnlySpan<byte> data, ReadOnlySpan<byte> signature, ReadOnlySpan<byte> publicKey);

    /// <summary>
    /// Verifies multiple signatures against the given data and public keys.
    /// </summary>
    /// <param name="data">The original data that was signed.</param>
    /// <param name="signatures">The signatures to verify.</param>
    /// <param name="publicKeys">The public keys corresponding to the signatures.</param>
    /// <returns>True if all signatures are valid.</returns>
    bool VerifyMultiple(ReadOnlySpan<byte> data, IReadOnlyList<byte[]> signatures, IReadOnlyList<byte[]> publicKeys);
}
