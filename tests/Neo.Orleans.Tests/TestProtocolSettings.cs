// Copyright (C) 2015-2025 The Neo Project.
//
// TestProtocolSettings.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Cryptography.ECC;
using Neo.Wallets;
using System.Collections.Generic;
using System.Linq;

namespace Neo.Orleans.Tests
{
    internal static class TestProtocolSettings
    {
        private static readonly KeyPair[] ValidatorKeys = CreateValidatorKeys(7);

        internal static readonly IReadOnlyDictionary<ECPoint, KeyPair> ValidatorKeyMap =
            ValidatorKeys.ToDictionary(key => key.PublicKey);

        internal static readonly ProtocolSettings SoleNode = ProtocolSettings.Default with
        {
            Network = 0x334F454Eu,
            StandbyCommittee = ValidatorKeys.Select(key => key.PublicKey).ToArray(),
            ValidatorsCount = ValidatorKeys.Length,
            SeedList =
            [
                "seed1.neo.org:10333"
            ],
        };

        private static KeyPair[] CreateValidatorKeys(int count)
        {
            var keys = new KeyPair[count];
            for (int i = 0; i < keys.Length; i++)
            {
                keys[i] = new KeyPair(Enumerable.Repeat((byte)(i + 1), 32).ToArray());
            }
            return keys;
        }
    }
}
