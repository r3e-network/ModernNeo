// Copyright (C) 2015-2025 The Neo Project.
//
// TypeForwards.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using System.Runtime.CompilerServices;

// Neo.Core types
[assembly: TypeForwardedTo(typeof(Neo.BigDecimal))]
[assembly: TypeForwardedTo(typeof(Neo.ContainsTransactionType))]
[assembly: TypeForwardedTo(typeof(Neo.Hardfork))]
[assembly: TypeForwardedTo(typeof(Neo.TimeProvider))]
[assembly: TypeForwardedTo(typeof(Neo.UInt160))]
[assembly: TypeForwardedTo(typeof(Neo.UInt256))]

// Neo.Cryptography types
[assembly: TypeForwardedTo(typeof(Neo.Cryptography.Base58))]
[assembly: TypeForwardedTo(typeof(Neo.Cryptography.BloomFilter))]
[assembly: TypeForwardedTo(typeof(Neo.Cryptography.Crypto))]
[assembly: TypeForwardedTo(typeof(Neo.Cryptography.Ed25519))]
[assembly: TypeForwardedTo(typeof(Neo.Cryptography.HashAlgorithm))]
[assembly: TypeForwardedTo(typeof(Neo.Cryptography.HashExtensions))]
[assembly: TypeForwardedTo(typeof(Neo.Cryptography.Hasher))]
[assembly: TypeForwardedTo(typeof(Neo.Cryptography.MerkleTree))]
[assembly: TypeForwardedTo(typeof(Neo.Cryptography.MerkleTreeNode))]
[assembly: TypeForwardedTo(typeof(Neo.Cryptography.Murmur128))]
[assembly: TypeForwardedTo(typeof(Neo.Cryptography.Murmur32))]
[assembly: TypeForwardedTo(typeof(Neo.Cryptography.ECC.ECCurve))]
[assembly: TypeForwardedTo(typeof(Neo.Cryptography.ECC.ECFieldElement))]
[assembly: TypeForwardedTo(typeof(Neo.Cryptography.ECC.ECPoint))]

// Neo.Storage types (Neo.Persistence namespace)
[assembly: TypeForwardedTo(typeof(Neo.Persistence.IStore))]
[assembly: TypeForwardedTo(typeof(Neo.Persistence.IStoreProvider))]
[assembly: TypeForwardedTo(typeof(Neo.Persistence.IStoreSnapshot))]
[assembly: TypeForwardedTo(typeof(Neo.Persistence.IWriteStore<,>))]
[assembly: TypeForwardedTo(typeof(Neo.Persistence.IReadOnlyStore<,>))]
[assembly: TypeForwardedTo(typeof(Neo.Persistence.SeekDirection))]
[assembly: TypeForwardedTo(typeof(Neo.Persistence.TrackState))]
[assembly: TypeForwardedTo(typeof(Neo.Persistence.StoreFactory))]
[assembly: TypeForwardedTo(typeof(Neo.Persistence.Providers.MemoryStore))]
[assembly: TypeForwardedTo(typeof(Neo.Persistence.Providers.MemorySnapshot))]
[assembly: TypeForwardedTo(typeof(Neo.Persistence.Providers.MemoryStoreProvider))]

// Neo.IO types
[assembly: TypeForwardedTo(typeof(Neo.IO.BinaryWriterExtensions))]
[assembly: TypeForwardedTo(typeof(Neo.IO.MemoryReaderExtensions))]

// Neo.Protocol types
[assembly: TypeForwardedTo(typeof(Neo.Network.P2P.Capabilities.NodeCapabilityType))]
[assembly: TypeForwardedTo(typeof(Neo.Network.P2P.MessageFlags))]
[assembly: TypeForwardedTo(typeof(Neo.Network.P2P.Payloads.FilterAddPayload))]
[assembly: TypeForwardedTo(typeof(Neo.Network.P2P.Payloads.FilterLoadPayload))]
[assembly: TypeForwardedTo(typeof(Neo.Network.P2P.Payloads.GetBlocksPayload))]
[assembly: TypeForwardedTo(typeof(Neo.Network.P2P.Payloads.OracleResponseCode))]
[assembly: TypeForwardedTo(typeof(Neo.Network.P2P.Payloads.WitnessRuleAction))]
[assembly: TypeForwardedTo(typeof(Neo.Network.P2P.Payloads.WitnessScope))]
[assembly: TypeForwardedTo(typeof(Neo.Ledger.TransactionRemovalReason))]
[assembly: TypeForwardedTo(typeof(Neo.SmartContract.CallFlags))]
[assembly: TypeForwardedTo(typeof(Neo.SmartContract.ContractParameterType))]
[assembly: TypeForwardedTo(typeof(Neo.SmartContract.FindOptions))]
[assembly: TypeForwardedTo(typeof(Neo.SmartContract.Native.NamedCurveHash))]
[assembly: TypeForwardedTo(typeof(Neo.SmartContract.Native.Role))]
[assembly: TypeForwardedTo(typeof(Neo.Plugins.UnhandledExceptionPolicy))]
[assembly: TypeForwardedTo(typeof(Neo.Plugins.IPluginSettings))]
[assembly: TypeForwardedTo(typeof(Neo.SmartContract.TriggerType))]
[assembly: TypeForwardedTo(typeof(Neo.Ledger.VerifyResult))]

