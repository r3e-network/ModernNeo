// Copyright (C) 2015-2025 The Neo Project.
//
// HashExtensions.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Extensions;
using Org.BouncyCastle.Crypto.Digests;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Parameters;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace Neo.Cryptography
{
    /// <summary>
    /// Hash and encryption extension methods for cryptography.
    /// </summary>
    public static class HashExtensions
    {
        private static readonly bool s_isOSX = RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

        private const int AesNonceSizeBytes = 12;
        private const int AesTagSizeBytes = 16;

        /// <summary>
        /// Computes the hash value for the specified byte array using the ripemd160 algorithm.
        /// </summary>
        /// <param name="value">The input to compute the hash code for.</param>
        /// <returns>The computed hash code.</returns>
        public static byte[] RIPEMD160(this byte[] value)
        {
            var digest = new RipeMD160Digest();
            var buffer = new byte[digest.GetDigestSize()];
            digest.BlockUpdate(value, 0, value.Length);
            digest.DoFinal(buffer, 0);
            return buffer;
        }

        /// <summary>
        /// Computes the hash value for the specified byte array using the ripemd160 algorithm.
        /// </summary>
        /// <param name="value">The input to compute the hash code for.</param>
        /// <returns>The computed hash code.</returns>
        public static byte[] RIPEMD160(this ReadOnlySpan<byte> value)
        {
            var digest = new RipeMD160Digest();
            var buffer = new byte[digest.GetDigestSize()];
            digest.BlockUpdate(value);
            digest.DoFinal(buffer, 0);
            return buffer;
        }

        /// <summary>
        /// Computes the hash value for the specified byte array using the murmur algorithm.
        /// </summary>
        /// <param name="value">The input to compute the hash code for.</param>
        /// <param name="seed">The seed used by the murmur algorithm.</param>
        /// <returns>The computed hash code.</returns>
        public static uint Murmur32(this byte[] value, uint seed)
        {
            return Cryptography.Murmur32.HashToUInt32(value, seed);
        }

        /// <summary>
        /// Computes the hash value for the specified byte array using the murmur algorithm.
        /// </summary>
        /// <param name="value">The input to compute the hash code for.</param>
        /// <param name="seed">The seed used by the murmur algorithm.</param>
        /// <returns>The computed hash code.</returns>
        public static uint Murmur32(this ReadOnlySpan<byte> value, uint seed)
        {
            return Cryptography.Murmur32.HashToUInt32(value, seed);
        }

        /// <summary>
        /// Computes the 128-bit hash value for the specified byte array using the murmur algorithm.
        /// </summary>
        /// <param name="value">The input to compute the hash code for.</param>
        /// <param name="seed">The seed used by the murmur algorithm.</param>
        /// <returns>The computed hash code.</returns>
        public static byte[] Murmur128(this byte[] value, uint seed) => value.AsReadOnlySpan().Murmur128(seed);

        /// <summary>
        /// Computes the 128-bit hash value for the specified byte array using the murmur algorithm.
        /// </summary>
        /// <param name="value">The input to compute the hash code for.</param>
        /// <param name="seed">The seed used by the murmur algorithm.</param>
        /// <returns>The computed hash code.</returns>
        public static byte[] Murmur128(this ReadOnlySpan<byte> value, uint seed)
        {
            return new Murmur128(seed).ComputeHash(value);
        }

        /// <summary>
        /// Computes the hash value for the specified byte array using the sha256 algorithm.
        /// </summary>
        /// <param name="value">The input to compute the hash code for.</param>
        /// <returns>The computed hash code.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte[] Sha256(this byte[] value)
        {
            return SHA256.HashData(value);
        }

        /// <summary>
        /// Computes the hash value for the specified byte array using the sha512 algorithm.
        /// </summary>
        /// <param name="value">The input to compute the hash code for.</param>
        /// <returns>The computed hash code.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte[] Sha512(this byte[] value)
        {
            return SHA512.HashData(value);
        }

        /// <summary>
        /// Computes the hash value for the specified byte array using the sha3-512 algorithm.
        /// </summary>
        /// <param name="source">The input to compute the hash code for.</param>
        /// <returns>The computed hash code.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte[] Sha3_512(this byte[] source) => Sha3_512(source.AsSpan());

        /// <summary>
        /// Computes the hash value for the specified byte array using the sha3-256 algorithm.
        /// </summary>
        /// <param name="source">The input to compute the hash code for.</param>
        /// <returns>The computed hash code.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte[] Sha3_256(this byte[] source) => Sha3_256(source.AsSpan());

        /// <summary>
        /// Computes the hash value for the specified byte array using the blake2b-512 algorithm.
        /// </summary>
        /// <param name="source">The input to compute the hash code for.</param>
        /// <param name="salt">The salt to use for the hash, and must be 16 bytes.</param>
        /// <returns>The computed hash code.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte[] Blake2b_512(this byte[] source, byte[]? salt = null) => Blake2b_512(source.AsSpan(), salt);

        /// <summary>
        /// Computes the hash value for the specified byte array using the blake2b-256 algorithm.
        /// </summary>
        /// <param name="source">The input to compute the hash code for.</param>
        /// <param name="salt">The salt to use for the hash, and must be 16 bytes.</param>
        /// <returns>The computed hash code.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte[] Blake2b_256(this byte[] source, byte[]? salt = null) => Blake2b_256(source.AsSpan(), salt);

        /// <summary>
        /// Computes the hash value for the specified region of the specified byte array using the sha256 algorithm.
        /// </summary>
        /// <param name="value">The input to compute the hash code for.</param>
        /// <param name="offset">The offset into the byte array from which to begin using data.</param>
        /// <param name="count">The number of bytes in the array to use as data.</param>
        /// <returns>The computed hash code.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte[] Sha256(this byte[] value, int offset, int count)
        {
            return SHA256.HashData(value.AsSpan(offset, count));
        }

        /// <summary>
        /// Computes the hash value for the specified region of the specified byte array using the sha512 algorithm.
        /// </summary>
        /// <param name="value">The input to compute the hash code for.</param>
        /// <param name="offset">The offset into the byte array from which to begin using data.</param>
        /// <param name="count">The number of bytes in the array to use as data.</param>
        /// <returns>The computed hash code.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte[] Sha512(this byte[] value, int offset, int count)
        {
            return SHA512.HashData(value.AsSpan(offset, count));
        }

        /// <summary>
        /// Computes the hash value for the specified byte array using the sha256 algorithm.
        /// </summary>
        /// <param name="value">The input to compute the hash code for.</param>
        /// <returns>The computed hash code.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte[] Sha256(this ReadOnlySpan<byte> value)
        {
            var buffer = new byte[32];
            SHA256.HashData(value, buffer);
            return buffer;
        }

        /// <summary>
        /// Computes the hash value for the specified byte array using the sha512 algorithm.
        /// </summary>
        /// <param name="value">The input to compute the hash code for.</param>
        /// <returns>The computed hash code.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte[] Sha512(this ReadOnlySpan<byte> value)
        {
            var buffer = new byte[64];
            SHA512.HashData(value, buffer);
            return buffer;
        }

        /// <summary>
        /// Computes the hash value for the specified byte array using the sha3-512 algorithm.
        /// </summary>
        /// <param name="source">The input to compute the hash code for.</param>
        /// <returns>The computed hash code.</returns>
        public static byte[] Sha3_512(this ReadOnlySpan<byte> source)
        {
            var sha3 = new Sha3Digest(512);
            sha3.BlockUpdate(source);

            var result = new byte[sha3.GetDigestSize()];
            sha3.DoFinal(result, 0);
            return result;
        }

        /// <summary>
        /// Computes the hash value for the specified byte array using the sha3-256 algorithm.
        /// </summary>
        /// <param name="source">The input to compute the hash code for.</param>
        /// <returns>The computed hash code.</returns>
        public static byte[] Sha3_256(this ReadOnlySpan<byte> source)
        {
            var sha3 = new Sha3Digest(256);
            sha3.BlockUpdate(source);

            var result = new byte[sha3.GetDigestSize()];
            sha3.DoFinal(result, 0);
            return result;
        }

        /// <summary>
        /// Computes the hash value for the specified byte array using the blake2b-512 algorithm.
        /// </summary>
        /// <param name="source">The input to compute the hash code for.</param>
        /// <param name="salt">The salt to use for the hash and must be null or 16 bytes.</param>
        /// <returns>The computed hash code.</returns>
        /// <exception cref="ArgumentException">Thrown when the salt is not null or 16 bytes.</exception>
        public static byte[] Blake2b_512(this ReadOnlySpan<byte> source, byte[]? salt = null)
        {
            if (salt is not null && salt.Length != 16)
                throw new ArgumentException("The salt must be null or 16 bytes.", nameof(salt));

            var blake2b = new Blake2bDigest(null, 64, salt, null);
            blake2b.BlockUpdate(source);

            var result = new byte[blake2b.GetDigestSize()];
            blake2b.DoFinal(result, 0);
            return result;
        }

        /// <summary>
        /// Computes the hash value for the specified byte array using the blake2b-256 algorithm.
        /// </summary>
        /// <param name="source">The input to compute the hash code for.</param>
        /// <param name="salt">The salt to use for the hash and must be null or 16 bytes.</param>
        /// <returns>The computed hash code.</returns>
        /// <exception cref="ArgumentException">Thrown when the salt is not null or 16 bytes.</exception>
        public static byte[] Blake2b_256(this ReadOnlySpan<byte> source, byte[]? salt = null)
        {
            if (salt is not null && salt.Length != 16)
                throw new ArgumentException("The salt must be null or 16 bytes.", nameof(salt));

            var blake2b = new Blake2bDigest(null, 32, salt, null);
            blake2b.BlockUpdate(source);

            var result = new byte[blake2b.GetDigestSize()];
            blake2b.DoFinal(result, 0);
            return result;
        }

        /// <summary>
        /// Computes the hash value for the specified byte array using the sha256 algorithm.
        /// </summary>
        /// <param name="value">The input to compute the hash code for.</param>
        /// <returns>The computed hash code.</returns>
        public static byte[] Sha256(this Span<byte> value)
        {
            return Sha256((ReadOnlySpan<byte>)value);
        }

        /// <summary>
        /// Computes the hash value for the specified byte array using the sha512 algorithm.
        /// </summary>
        /// <param name="value">The input to compute the hash code for.</param>
        /// <returns>The computed hash code.</returns>
        public static byte[] Sha512(this Span<byte> value)
        {
            return Sha512((ReadOnlySpan<byte>)value);
        }

        /// <summary>
        /// Computes the hash value for the specified byte array using the sha3-512 algorithm.
        /// </summary>
        /// <param name="source">The input to compute the hash code for.</param>
        /// <returns>The computed hash code.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte[] Sha3_512(this Span<byte> source) => Sha3_512((ReadOnlySpan<byte>)source);

        /// <summary>
        /// Computes the hash value for the specified byte array using the sha3-256 algorithm.
        /// </summary>
        /// <param name="source">The input to compute the hash code for.</param>
        /// <returns>The computed hash code.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte[] Sha3_256(this Span<byte> source) => Sha3_256((ReadOnlySpan<byte>)source);

        /// <summary>
        /// Computes the hash value for the specified byte array using the blake2b-512 algorithm.
        /// </summary>
        /// <param name="source">The input to compute the hash code for.</param>
        /// <param name="salt">The salt to use for the hash, and must be 16 bytes.</param>
        /// <returns>The computed hash code.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte[] Blake2b_512(this Span<byte> source, byte[]? salt = null)
            => Blake2b_512((ReadOnlySpan<byte>)source, salt);

        /// <summary>
        /// Computes the hash value for the specified byte array using the blake2b-256 algorithm.
        /// </summary>
        /// <param name="source">The input to compute the hash code for.</param>
        /// <param name="salt">The salt to use for the hash, and must be 16 bytes.</param>
        /// <returns>The computed hash code.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte[] Blake2b_256(this Span<byte> source, byte[]? salt = null)
            => Blake2b_256((ReadOnlySpan<byte>)source, salt);

        /// <summary>
        /// Computes the hash value for the specified byte array using the keccak256 algorithm.
        /// </summary>
        /// <param name="value">The input to compute the hash code for.</param>
        /// <returns>The computed hash code.</returns>
        public static byte[] Keccak256(this byte[] value) => value.AsSpan().Keccak256();

        /// <summary>
        /// Computes the hash value for the specified byte array using the keccak256 algorithm.
        /// </summary>
        /// <param name="value">The input to compute the hash code for.</param>
        /// <returns>The computed hash code.</returns>
        public static byte[] Keccak256(this ReadOnlySpan<byte> value)
        {
            var keccak = new KeccakDigest(256);
            keccak.BlockUpdate(value);
            var result = new byte[keccak.GetDigestSize()];
            keccak.DoFinal(result, 0);
            return result;
        }

        /// <summary>
        /// Computes the hash value for the specified byte array using the keccak256 algorithm.
        /// </summary>
        /// <param name="value">The input to compute the hash code for.</param>
        /// <returns>The computed hash code.</returns>
        public static byte[] Keccak256(this Span<byte> value) => ((ReadOnlySpan<byte>)value).Keccak256();

        /// <summary>
        /// Encrypts data using AES-256-GCM.
        /// </summary>
        public static byte[] AES256Encrypt(this byte[] plainData, byte[] key, byte[] nonce, byte[]? associatedData = null)
        {
            if (nonce.Length != AesNonceSizeBytes)
                throw new ArgumentOutOfRangeException(nameof(nonce), $"`nonce` must be {AesNonceSizeBytes} bytes");

            var tag = new byte[AesTagSizeBytes];
            var cipherBytes = new byte[plainData.Length];
            if (!s_isOSX)
            {
                using var cipher = new AesGcm(key, AesTagSizeBytes);
                cipher.Encrypt(nonce, plainData, cipherBytes, tag, associatedData);
            }
            else
            {
                var cipher = new GcmBlockCipher(new AesEngine());
                var parameters = new AeadParameters(
                    new KeyParameter(key),
                    AesTagSizeBytes * 8,
                    nonce,
                    associatedData);
                cipher.Init(true, parameters);
                cipherBytes = new byte[cipher.GetOutputSize(plainData.Length)];
                var length = cipher.ProcessBytes(plainData, 0, plainData.Length, cipherBytes, 0);
                cipher.DoFinal(cipherBytes, length);
            }
            return [.. nonce, .. cipherBytes, .. tag];
        }

        /// <summary>
        /// Decrypts data using AES-256-GCM.
        /// </summary>
        public static byte[] AES256Decrypt(this byte[] encryptedData, byte[] key, byte[]? associatedData = null)
        {
            if (encryptedData.Length < AesNonceSizeBytes + AesTagSizeBytes)
                throw new ArgumentException($"The encryptedData.Length must be greater than {AesNonceSizeBytes} + {AesTagSizeBytes}");

            ReadOnlySpan<byte> encrypted = encryptedData;
            var nonce = encrypted[..AesNonceSizeBytes];
            var cipherBytes = encrypted[AesNonceSizeBytes..^AesTagSizeBytes];
            var tag = encrypted[^AesTagSizeBytes..];
            var decryptedData = new byte[cipherBytes.Length];
            if (!s_isOSX)
            {
                using var cipher = new AesGcm(key, AesTagSizeBytes);
                cipher.Decrypt(nonce, cipherBytes, tag, decryptedData, associatedData);
            }
            else
            {
                var cipher = new GcmBlockCipher(new AesEngine());
                var parameters = new AeadParameters(
                    new KeyParameter(key),
                    AesTagSizeBytes * 8,
                    nonce.ToArray(),
                    associatedData);
                cipher.Init(false, parameters);
                decryptedData = new byte[cipher.GetOutputSize(cipherBytes.Length)];
                var length = cipher.ProcessBytes(cipherBytes.ToArray(), 0, cipherBytes.Length, decryptedData, 0);
                cipher.DoFinal(decryptedData, length);
            }
            return decryptedData;
        }
    }
}
