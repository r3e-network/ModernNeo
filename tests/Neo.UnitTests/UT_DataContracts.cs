// Copyright (C) 2015-2025 The Neo Project.
//
// UT_DataContracts.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.Core.Interfaces;
using Neo.Cryptography;
using Neo.Cryptography.ECC;
using Neo.IO;
using Neo.Network.P2P.Payloads;
using Neo.Network.P2P.Payloads.Conditions;
using System;
using System.IO;

namespace Neo.UnitTests
{
    [TestClass]
    public class UT_DataContracts
    {
        [TestMethod]
        public void Transaction_Implements_ITransactionData()
        {
            var sender = UInt160.Parse("0x0102030405060708090a0b0c0d0e0f1011121314");
            var conflictsHash = UInt256.Parse("0x0102030405060708090a0b0c0d0e0f101112131415161718191a1b1c1d1e1f20");

            var tx = new Transaction
            {
                Version = 0,
                Nonce = 123,
                SystemFee = 456,
                NetworkFee = 10_000,
                ValidUntilBlock = 789,
                Signers = [new Signer { Account = sender, Scopes = WitnessScope.CalledByEntry }],
                Attributes = [new Conflicts { Hash = conflictsHash }],
                Script = new byte[] { 0x01, 0x02, 0x03 },
                Witnesses = [Witness.Empty],
            };

            ITransactionData data = tx;

            Assert.AreEqual(tx.Hash, data.Hash);
            Assert.AreEqual(tx.Version, data.Version);
            Assert.AreEqual(tx.Nonce, data.Nonce);
            Assert.AreEqual(tx.SystemFee, data.SystemFee);
            Assert.AreEqual(tx.NetworkFee, data.NetworkFee);
            Assert.AreEqual(tx.ValidUntilBlock, data.ValidUntilBlock);
            Assert.AreEqual(tx.Sender, data.Sender);
            Assert.AreEqual(tx.Signers.Length, data.SignersCount);
            Assert.AreEqual(tx.Attributes.Length, data.AttributesCount);
            Assert.AreEqual(tx.NetworkFee / tx.Size, data.FeePerByte);
            CollectionAssert.AreEqual(tx.Script.Span.ToArray(), data.Script.Span.ToArray());
        }

        [TestMethod]
        public void Header_Implements_IHeaderData()
        {
            var header = new Header
            {
                Version = 0,
                PrevHash = UInt256.Parse("0x0000000000000000000000000000000000000000000000000000000000000001"),
                MerkleRoot = UInt256.Parse("0x0000000000000000000000000000000000000000000000000000000000000002"),
                Timestamp = 123,
                Nonce = 456,
                Index = 789,
                PrimaryIndex = 1,
                NextConsensus = UInt160.Parse("0x0000000000000000000000000000000000000001"),
                Witness = Witness.Empty,
            };

            IHeaderData data = header;

            Assert.AreEqual(header.Hash, data.Hash);
            Assert.AreEqual(header.Version, data.Version);
            Assert.AreEqual(header.PrevHash, data.PrevHash);
            Assert.AreEqual(header.MerkleRoot, data.MerkleRoot);
            Assert.AreEqual(header.Timestamp, data.Timestamp);
            Assert.AreEqual(header.Nonce, data.Nonce);
            Assert.AreEqual(header.Index, data.Index);
            Assert.AreEqual(header.PrimaryIndex, data.PrimaryIndex);
            Assert.AreEqual(header.NextConsensus, data.NextConsensus);
        }

        [TestMethod]
        public void Block_Implements_IBlockData()
        {
            var tx1 = TestUtils.CreateRandomHashTransaction();
            var tx2 = TestUtils.CreateRandomHashTransaction();

            var merkleRoot = MerkleTree.ComputeRoot([tx1.Hash, tx2.Hash]);
            var header = new Header
            {
                Version = 0,
                PrevHash = UInt256.Zero,
                MerkleRoot = merkleRoot,
                Timestamp = 123,
                Nonce = 456,
                Index = 789,
                PrimaryIndex = 0,
                NextConsensus = UInt160.Zero,
                Witness = Witness.Empty,
            };

            var block = new Block
            {
                Header = header,
                Transactions = [tx1, tx2],
            };

            IBlockData data = block;

            Assert.AreEqual(block.Hash, data.Hash);
            Assert.AreEqual(header.Version, data.Version);
            Assert.AreEqual(header.PrevHash, data.PrevHash);
            Assert.AreEqual(header.MerkleRoot, data.MerkleRoot);
            Assert.AreEqual(header.Timestamp, data.Timestamp);
            Assert.AreEqual(header.Nonce, data.Nonce);
            Assert.AreEqual(header.Index, data.Index);
            Assert.AreEqual(header.PrimaryIndex, data.PrimaryIndex);
            Assert.AreEqual(header.NextConsensus, data.NextConsensus);
            Assert.AreEqual(2, data.TransactionsCount);
        }

        [TestMethod]
        public void Signer_Implements_ISignerData()
        {
            var signer = new Signer
            {
                Account = UInt160.Parse("0x0102030405060708090a0b0c0d0e0f1011121314"),
                Scopes = WitnessScope.CustomContracts | WitnessScope.CustomGroups | WitnessScope.WitnessRules,
                AllowedContracts = [UInt160.Zero],
                AllowedGroups = [ECCurve.Secp256r1.G],
                Rules =
                [
                    new WitnessRule
                    {
                        Action = WitnessRuleAction.Allow,
                        Condition = new CalledByEntryCondition()
                    }
                ]
            };

            ISignerData data = signer;

            Assert.AreEqual(signer.Account, data.Account);
            Assert.AreEqual((byte)signer.Scopes, data.ScopesValue);
            Assert.AreEqual(1, data.AllowedContractsCount);
            Assert.AreEqual(1, data.AllowedGroupsCount);
            Assert.AreEqual(1, data.RulesCount);
        }

        [TestMethod]
        public void WitnessRule_Implements_IWitnessRuleData()
        {
            var rule = new WitnessRule
            {
                Action = WitnessRuleAction.Deny,
                Condition = new CalledByEntryCondition()
            };

            IWitnessRuleData data = rule;

            Assert.AreEqual((byte)WitnessRuleAction.Deny, data.ActionValue);
        }

        [TestMethod]
        public void WitnessCondition_Implements_IWitnessConditionData()
        {
            var condition = new CalledByEntryCondition();
            IWitnessConditionData data = condition;

            Assert.AreEqual((byte)WitnessConditionType.CalledByEntry, data.TypeValue);
        }

        [TestMethod]
        public void TransactionAttribute_Implements_ITransactionAttributeData()
        {
            var conflicts = new Conflicts
            {
                Hash = UInt256.Parse("0x0102030405060708090a0b0c0d0e0f101112131415161718191a1b1c1d1e1f20")
            };
            ITransactionAttributeData conflictsData = conflicts;
            Assert.AreEqual((byte)TransactionAttributeType.Conflicts, conflictsData.TypeValue);
            Assert.IsTrue(conflictsData.AllowMultiple);

            ITransactionAttributeData highPriorityData = new HighPriorityAttribute();
            Assert.AreEqual((byte)TransactionAttributeType.HighPriority, highPriorityData.TypeValue);
            Assert.IsFalse(highPriorityData.AllowMultiple);
        }

        [TestMethod]
        public void Block_SerializeUnsigned_RoundTrip()
        {
            var header = new Header
            {
                Version = 0,
                PrevHash = UInt256.Parse("0x0000000000000000000000000000000000000000000000000000000000000003"),
                MerkleRoot = UInt256.Parse("0x0000000000000000000000000000000000000000000000000000000000000004"),
                Timestamp = 123,
                Nonce = 456,
                Index = 789,
                PrimaryIndex = 2,
                NextConsensus = UInt160.Parse("0x0000000000000000000000000000000000000002"),
                Witness = Witness.Empty,
            };

            var block = new Block
            {
                Header = header,
                Transactions = [],
            };

            byte[] unsignedBytes;
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                ((IVerifiableBase)block).SerializeUnsigned(writer);
                unsignedBytes = stream.ToArray();
            }

            var roundTrip = new Block
            {
                Header = new Header
                {
                    PrevHash = UInt256.Zero,
                    MerkleRoot = UInt256.Zero,
                    NextConsensus = UInt160.Zero,
                    Witness = Witness.Empty,
                },
                Transactions = [],
            };

            var reader = new MemoryReader(unsignedBytes);
            ((IVerifiableBase)roundTrip).DeserializeUnsigned(ref reader);

            IBlockData data = roundTrip;
            Assert.AreEqual(header.Version, data.Version);
            Assert.AreEqual(header.PrevHash, data.PrevHash);
            Assert.AreEqual(header.MerkleRoot, data.MerkleRoot);
            Assert.AreEqual(header.Timestamp, data.Timestamp);
            Assert.AreEqual(header.Nonce, data.Nonce);
            Assert.AreEqual(header.Index, data.Index);
            Assert.AreEqual(header.PrimaryIndex, data.PrimaryIndex);
            Assert.AreEqual(header.NextConsensus, data.NextConsensus);
        }
    }
}

