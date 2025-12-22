// Copyright (C) 2015-2025 The Neo Project.
//
// UT_GraphQLTypes.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.GraphQL.Types;

namespace Neo.GraphQL.Tests
{
    [TestClass]
    public class UT_GraphQLTypes
    {
        [TestMethod]
        public void BlockType_HasCorrectName()
        {
            var blockType = new BlockType();
            Assert.AreEqual("Block", blockType.Name);
        }

        [TestMethod]
        public void BlockType_HasDescription()
        {
            var blockType = new BlockType();
            Assert.IsNotNull(blockType.Description);
            Assert.IsTrue(blockType.Description.Contains("block"));
        }

        [TestMethod]
        public void BlockType_HasRequiredFields()
        {
            var blockType = new BlockType();
            var fields = blockType.Fields.Select(f => f.Name).ToList();

            Assert.IsTrue(fields.Contains("hash"));
            Assert.IsTrue(fields.Contains("index"));
            Assert.IsTrue(fields.Contains("version"));
            Assert.IsTrue(fields.Contains("prevHash"));
            Assert.IsTrue(fields.Contains("merkleRoot"));
            Assert.IsTrue(fields.Contains("timestamp"));
            Assert.IsTrue(fields.Contains("nonce"));
            Assert.IsTrue(fields.Contains("primaryIndex"));
            Assert.IsTrue(fields.Contains("nextConsensus"));
            Assert.IsTrue(fields.Contains("size"));
            Assert.IsTrue(fields.Contains("transactionCount"));
            Assert.IsTrue(fields.Contains("transactions"));
            Assert.IsTrue(fields.Contains("transactionHashes"));
        }

        [TestMethod]
        public void TransactionType_HasCorrectName()
        {
            var txType = new TransactionType();
            Assert.AreEqual("Transaction", txType.Name);
        }

        [TestMethod]
        public void TransactionType_HasDescription()
        {
            var txType = new TransactionType();
            Assert.IsNotNull(txType.Description);
            Assert.IsTrue(txType.Description.Contains("transaction"));
        }

        [TestMethod]
        public void TransactionType_HasRequiredFields()
        {
            var txType = new TransactionType();
            var fields = txType.Fields.Select(f => f.Name).ToList();

            Assert.IsTrue(fields.Contains("hash"));
            Assert.IsTrue(fields.Contains("version"));
            Assert.IsTrue(fields.Contains("nonce"));
            Assert.IsTrue(fields.Contains("sender"));
            Assert.IsTrue(fields.Contains("systemFee"));
            Assert.IsTrue(fields.Contains("networkFee"));
            Assert.IsTrue(fields.Contains("validUntilBlock"));
            Assert.IsTrue(fields.Contains("size"));
            Assert.IsTrue(fields.Contains("feePerByte"));
            Assert.IsTrue(fields.Contains("script"));
            Assert.IsTrue(fields.Contains("signersCount"));
            Assert.IsTrue(fields.Contains("attributesCount"));
            Assert.IsTrue(fields.Contains("signers"));
            Assert.IsTrue(fields.Contains("witnesses"));
        }

        [TestMethod]
        public void SignerType_HasCorrectName()
        {
            var signerType = new SignerType();
            Assert.AreEqual("Signer", signerType.Name);
        }

        [TestMethod]
        public void SignerType_HasRequiredFields()
        {
            var signerType = new SignerType();
            var fields = signerType.Fields.Select(f => f.Name).ToList();

            Assert.IsTrue(fields.Contains("account"));
            Assert.IsTrue(fields.Contains("scopes"));
            Assert.IsTrue(fields.Contains("allowedContracts"));
            Assert.IsTrue(fields.Contains("allowedGroups"));
        }

        [TestMethod]
        public void WitnessType_HasCorrectName()
        {
            var witnessType = new WitnessType();
            Assert.AreEqual("Witness", witnessType.Name);
        }

        [TestMethod]
        public void WitnessType_HasRequiredFields()
        {
            var witnessType = new WitnessType();
            var fields = witnessType.Fields.Select(f => f.Name).ToList();

            Assert.IsTrue(fields.Contains("invocationScript"));
            Assert.IsTrue(fields.Contains("verificationScript"));
            Assert.IsTrue(fields.Contains("scriptHash"));
        }
    }
}
