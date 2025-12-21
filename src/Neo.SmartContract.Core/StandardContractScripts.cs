// Copyright (C) 2015-2025 The Neo Project.
//
// StandardContractScripts.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Cryptography;
using Neo.Cryptography.ECC;
using Neo.VM;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace Neo.SmartContract
{
    /// <summary>
    /// Helpers for recognizing standard contract scripts (single-sig and multi-sig).
    /// </summary>
    public static class StandardContractScripts
    {
        private static readonly uint SystemCryptoCheckSig = GetSyscallHash("System.Crypto.CheckSig");
        private static readonly uint SystemCryptoCheckMultisig = GetSyscallHash("System.Crypto.CheckMultisig");

        private static uint GetSyscallHash(string name)
        {
            return BinaryPrimitives.ReadUInt32LittleEndian(Encoding.ASCII.GetBytes(name).Sha256());
        }

        /// <summary>
        /// Determines whether the specified script is a standard contract script.
        /// </summary>
        /// <param name="script">The script bytes.</param>
        /// <returns><see langword="true"/> if standard; otherwise <see langword="false"/>.</returns>
        public static bool IsStandardContract(ReadOnlySpan<byte> script)
        {
            return IsSignatureContract(script) || IsMultiSigContract(script);
        }

        /// <summary>
        /// Determines whether the specified script is a signature contract script.
        /// </summary>
        /// <param name="script">The script bytes.</param>
        /// <returns><see langword="true"/> if signature contract; otherwise <see langword="false"/>.</returns>
        public static bool IsSignatureContract(ReadOnlySpan<byte> script)
        {
            if (script.Length != 40) return false;
            if (script[0] != (byte)OpCode.PUSHDATA1
                || script[1] != 33
                || script[35] != (byte)OpCode.SYSCALL
                || BinaryPrimitives.ReadUInt32LittleEndian(script[36..]) != SystemCryptoCheckSig)
                return false;
            return true;
        }

        /// <summary>
        /// Determines whether the specified script is a multi-signature contract script.
        /// </summary>
        /// <param name="script">The script bytes.</param>
        /// <returns><see langword="true"/> if multi-sig; otherwise <see langword="false"/>.</returns>
        public static bool IsMultiSigContract(ReadOnlySpan<byte> script)
        {
            return IsMultiSigContract(script, out _, out _, null);
        }

        /// <summary>
        /// Determines whether the specified script is a multi-signature contract script.
        /// </summary>
        /// <param name="script">The script bytes.</param>
        /// <param name="m">The number of required signatures.</param>
        /// <param name="n">The number of public keys.</param>
        /// <returns><see langword="true"/> if multi-sig; otherwise <see langword="false"/>.</returns>
        public static bool IsMultiSigContract(ReadOnlySpan<byte> script, out int m, out int n)
        {
            return IsMultiSigContract(script, out m, out n, null);
        }

        /// <summary>
        /// Determines whether the specified script is a multi-signature contract script.
        /// </summary>
        /// <param name="script">The script bytes.</param>
        /// <param name="m">The number of required signatures.</param>
        /// <param name="points">The decoded public keys.</param>
        /// <returns><see langword="true"/> if multi-sig; otherwise <see langword="false"/>.</returns>
        public static bool IsMultiSigContract(ReadOnlySpan<byte> script, out int m, [NotNullWhen(true)] out ECPoint[]? points)
        {
            List<ECPoint> list = new();
            if (IsMultiSigContract(script, out m, out _, list))
            {
                points = list.ToArray();
                return true;
            }

            points = null;
            return false;
        }

        private static bool IsMultiSigContract(ReadOnlySpan<byte> script, out int m, out int n, List<ECPoint>? points)
        {
            m = 0;
            n = 0;
            int i = 0;
            if (script.Length < 42) return false;
            switch (script[i])
            {
                case (byte)OpCode.PUSHINT8:
                    m = script[++i];
                    ++i;
                    break;
                case (byte)OpCode.PUSHINT16:
                    m = BinaryPrimitives.ReadUInt16LittleEndian(script[++i..]);
                    i += 2;
                    break;
                case byte b when b >= (byte)OpCode.PUSH1 && b <= (byte)OpCode.PUSH16:
                    m = b - (byte)OpCode.PUSH0;
                    ++i;
                    break;
                default:
                    return false;
            }
            if (m < 1 || m > 1024) return false;
            while (script[i] == (byte)OpCode.PUSHDATA1)
            {
                if (script.Length <= i + 35) return false;
                if (script[++i] != 33) return false;
                try
                {
                    points?.Add(ECPoint.DecodePoint(script.Slice(i + 1, 33), ECCurve.Secp256r1));
                }
                catch (Exception)
                {
                    return false;
                }
                i += 34;
                ++n;
            }
            if (n < m || n > 1024) return false;
            switch (script[i])
            {
                case (byte)OpCode.PUSHINT8:
                    if (script.Length <= i + 1 || n != script[++i]) return false;
                    ++i;
                    break;
                case (byte)OpCode.PUSHINT16:
                    if (script.Length < i + 3 || n != BinaryPrimitives.ReadUInt16LittleEndian(script[++i..])) return false;
                    i += 2;
                    break;
                case byte b when b >= (byte)OpCode.PUSH1 && b <= (byte)OpCode.PUSH16:
                    if (n != b - (byte)OpCode.PUSH0) return false;
                    ++i;
                    break;
                default:
                    return false;
            }
            if (script.Length != i + 5) return false;
            if (script[i++] != (byte)OpCode.SYSCALL) return false;
            if (BinaryPrimitives.ReadUInt32LittleEndian(script[i..]) != SystemCryptoCheckMultisig)
                return false;
            return true;
        }
    }
}

