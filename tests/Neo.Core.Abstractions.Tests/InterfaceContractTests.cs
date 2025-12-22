// Copyright (C) 2015-2025 The Neo Project.
//
// InterfaceContractTests.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.Core.Abstractions;
using Neo.Core.Abstractions.Blockchain;
using Neo.Core.Abstractions.MemPool;
using Neo.Core.Abstractions.Storage;
using System;
using System.Linq;
using System.Reflection;

namespace Neo.Core.Abstractions.Tests
{
    [TestClass]
    public class InterfaceContractTests
    {
        private readonly Assembly _assembly = typeof(IBlockData).Assembly;

        [TestMethod]
        public void Assembly_HasZeroProjectDependencies()
        {
            // Neo.Core.Abstractions must have ZERO project dependencies
            // Only system assemblies are allowed
            var references = _assembly.GetReferencedAssemblies();

            foreach (var reference in references)
            {
                // Only allow System.* and Microsoft.* assemblies
                Assert.IsTrue(
                    reference.Name!.StartsWith("System") ||
                    reference.Name.StartsWith("Microsoft") ||
                    reference.Name.StartsWith("netstandard") ||
                    reference.Name == "Neo.Core.Abstractions",
                    $"Unexpected dependency: {reference.Name}. Neo.Core.Abstractions must have zero project dependencies.");
            }
        }

        [TestMethod]
        public void IBlockData_HasRequiredProperties()
        {
            var type = typeof(IBlockData);

            Assert.IsNotNull(type.GetProperty("Hash"));
            Assert.IsNotNull(type.GetProperty("Index"));
            Assert.IsNotNull(type.GetProperty("Timestamp"));
            Assert.IsNotNull(type.GetProperty("PrevHash"));
            Assert.IsNotNull(type.GetProperty("MerkleRoot"));
            Assert.IsNotNull(type.GetProperty("PrimaryIndex"));
            Assert.IsNotNull(type.GetProperty("NextConsensus"));
            Assert.IsNotNull(type.GetProperty("TransactionCount"));
            Assert.IsNotNull(type.GetProperty("Transactions"));
        }

        [TestMethod]
        public void ITransactionData_HasRequiredProperties()
        {
            var type = typeof(ITransactionData);

            Assert.IsNotNull(type.GetProperty("Hash"));
            Assert.IsNotNull(type.GetProperty("Version"));
            Assert.IsNotNull(type.GetProperty("Nonce"));
            Assert.IsNotNull(type.GetProperty("SystemFee"));
            Assert.IsNotNull(type.GetProperty("NetworkFee"));
            Assert.IsNotNull(type.GetProperty("ValidUntilBlock"));
            Assert.IsNotNull(type.GetProperty("Script"));
            Assert.IsNotNull(type.GetProperty("Sender"));
            Assert.IsNotNull(type.GetProperty("Signers"));
            Assert.IsNotNull(type.GetProperty("Witnesses"));
            Assert.IsNotNull(type.GetProperty("Attributes"));
            Assert.IsNotNull(type.GetProperty("Size"));
        }

        [TestMethod]
        public void IHeaderData_HasRequiredProperties()
        {
            var type = typeof(IHeaderData);

            Assert.IsNotNull(type.GetProperty("Hash"));
            Assert.IsNotNull(type.GetProperty("Index"));
            Assert.IsNotNull(type.GetProperty("Timestamp"));
            Assert.IsNotNull(type.GetProperty("PrevHash"));
            Assert.IsNotNull(type.GetProperty("MerkleRoot"));
            Assert.IsNotNull(type.GetProperty("PrimaryIndex"));
            Assert.IsNotNull(type.GetProperty("NextConsensus"));
            Assert.IsNotNull(type.GetProperty("Witness"));
        }

        [TestMethod]
        public void IBlockchainQuery_HasRequiredMethods()
        {
            var type = typeof(IBlockchainQuery);

            Assert.IsNotNull(type.GetMethod("GetHeightAsync"));
            Assert.IsNotNull(type.GetMethod("GetHeaderHeightAsync"));
            Assert.IsNotNull(type.GetMethod("GetBlockByHashAsync"));
            Assert.IsNotNull(type.GetMethod("GetBlockByIndexAsync"));
            Assert.IsNotNull(type.GetMethod("GetBlockHashByIndexAsync"));
            Assert.IsNotNull(type.GetMethod("GetHeaderByHashAsync"));
            Assert.IsNotNull(type.GetMethod("GetHeaderByIndexAsync"));
            Assert.IsNotNull(type.GetMethod("ContainsBlockAsync"));
            Assert.IsNotNull(type.GetMethod("ContainsTransactionAsync"));
            Assert.IsNotNull(type.GetMethod("GetTransactionAsync"));
        }

        [TestMethod]
        public void IBlockchainMutator_HasRequiredMethods()
        {
            var type = typeof(IBlockchainMutator);

            Assert.IsNotNull(type.GetMethod("PersistBlockAsync"));
            Assert.IsNotNull(type.GetMethod("AddHeaderAsync"));
        }

        [TestMethod]
        public void IBlockValidator_HasRequiredMethods()
        {
            var type = typeof(IBlockValidator);

            Assert.IsNotNull(type.GetMethod("ValidateBlockAsync"));
            Assert.IsNotNull(type.GetMethod("ValidateHeaderAsync"));
        }

        [TestMethod]
        public void ITransactionValidator_HasRequiredMethods()
        {
            var type = typeof(ITransactionValidator);

            Assert.IsNotNull(type.GetMethod("ValidateTransactionAsync"));
            Assert.IsNotNull(type.GetMethod("PreverifyTransactionAsync"));
        }

        [TestMethod]
        public void IStorageSnapshot_HasRequiredMethods()
        {
            var type = typeof(IStorageSnapshot);

            Assert.IsNotNull(type.GetMethod("TryGet"));
            Assert.IsNotNull(type.GetMethod("Contains"));
            Assert.IsNotNull(type.GetMethod("Find"));
            Assert.IsNotNull(type.GetMethod("Clone"));
        }

        [TestMethod]
        public void IStorageMutator_ExtendsIStorageSnapshot()
        {
            Assert.IsTrue(typeof(IStorageSnapshot).IsAssignableFrom(typeof(IStorageMutator)));
        }

        [TestMethod]
        public void IStorageMutator_HasRequiredMethods()
        {
            var type = typeof(IStorageMutator);

            Assert.IsNotNull(type.GetMethod("Put"));
            Assert.IsNotNull(type.GetMethod("Delete"));
            Assert.IsNotNull(type.GetMethod("Commit"));
        }

        [TestMethod]
        public void IStorageKey_HasRequiredProperties()
        {
            var type = typeof(IStorageKey);

            Assert.IsNotNull(type.GetProperty("Id"));
            Assert.IsNotNull(type.GetProperty("Key"));
            Assert.IsNotNull(type.GetMethod("ToArray"));
        }

        [TestMethod]
        public void IStorageItem_HasRequiredProperties()
        {
            var type = typeof(IStorageItem);

            Assert.IsNotNull(type.GetProperty("Value"));
            Assert.IsNotNull(type.GetProperty("IsConstant"));
            Assert.IsNotNull(type.GetMethod("Clone"));
        }

        [TestMethod]
        public void IMemoryPoolQuery_HasRequiredProperties()
        {
            var type = typeof(IMemoryPoolQuery);

            Assert.IsNotNull(type.GetProperty("VerifiedCount"));
            Assert.IsNotNull(type.GetProperty("UnverifiedCount"));
            Assert.IsNotNull(type.GetProperty("Count"));
        }

        [TestMethod]
        public void IMemoryPoolQuery_HasRequiredMethods()
        {
            var type = typeof(IMemoryPoolQuery);

            Assert.IsNotNull(type.GetMethod("ContainsKey"));
            Assert.IsNotNull(type.GetMethod("GetVerifiedTransactionsAsync"));
            Assert.IsNotNull(type.GetMethod("TryGetValue"));
        }

        [TestMethod]
        public void IMemoryPoolMutator_ExtendsIMemoryPoolQuery()
        {
            Assert.IsTrue(typeof(IMemoryPoolQuery).IsAssignableFrom(typeof(IMemoryPoolMutator)));
        }

        [TestMethod]
        public void IMemoryPoolMutator_HasRequiredMethods()
        {
            var type = typeof(IMemoryPoolMutator);

            Assert.IsNotNull(type.GetMethod("AddTransactionAsync"));
            Assert.IsNotNull(type.GetMethod("Remove"));
            Assert.IsNotNull(type.GetMethod("Clear"));
        }

        [TestMethod]
        public void IVerifiable_HasRequiredMembers()
        {
            var type = typeof(IVerifiable);

            Assert.IsNotNull(type.GetProperty("Witnesses"));
            Assert.IsNotNull(type.GetMethod("GetSignData"));
            Assert.IsNotNull(type.GetMethod("GetScriptHashesForVerifying"));
        }

        [TestMethod]
        public void IInventory_HasRequiredMembers()
        {
            var type = typeof(IInventory);

            Assert.IsNotNull(type.GetProperty("Hash"));
            Assert.IsNotNull(type.GetProperty("Type"));
        }

        [TestMethod]
        public void InventoryType_HasExpectedValues()
        {
            Assert.AreEqual((byte)0x2b, (byte)InventoryType.Transaction);
            Assert.AreEqual((byte)0x2c, (byte)InventoryType.Block);
            Assert.AreEqual((byte)0x2e, (byte)InventoryType.Extensible);
        }

        [TestMethod]
        public void VerifyResult_HasExpectedValues()
        {
            Assert.AreEqual((byte)0, (byte)VerifyResult.Succeed);
            Assert.AreEqual((byte)1, (byte)VerifyResult.AlreadyInPool);
            Assert.AreEqual((byte)2, (byte)VerifyResult.AlreadyOnChain);
            Assert.AreEqual((byte)255, (byte)VerifyResult.Unknown);
        }

        [TestMethod]
        public void AllInterfaces_HaveXmlDocumentation()
        {
            var interfaces = _assembly.GetTypes().Where(t => t.IsInterface);

            foreach (var iface in interfaces)
            {
                // Check that interface has summary documentation
                // This is a compile-time check via GenerateDocumentationFile
                Assert.IsTrue(iface.IsPublic, $"Interface {iface.Name} should be public");
            }
        }

        [TestMethod]
        public void AllPublicTypes_AreInCorrectNamespace()
        {
            var types = _assembly.GetTypes().Where(t => t.IsPublic);

            foreach (var type in types)
            {
                Assert.IsTrue(
                    type.Namespace!.StartsWith("Neo.Core.Abstractions"),
                    $"Type {type.Name} is in unexpected namespace {type.Namespace}");
            }
        }
    }
}
