// Copyright (C) 2015-2025 The Neo Project.
//
// UT_NativeContractRegistry.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.Core.Interfaces;
using Neo.SmartContract;
using Neo.SmartContract.Native;
using System.Linq;

namespace Neo.UnitTests.SmartContract
{
    [TestClass]
    public class UT_NativeContractRegistry
    {
        private INativeContractRegistry _registry = null!;

        [TestInitialize]
        public void Setup()
        {
            _registry = NativeContractRegistry.Instance;
        }

        [TestMethod]
        public void Test_Singleton_Instance()
        {
            var instance1 = NativeContractRegistry.Instance;
            var instance2 = NativeContractRegistry.Instance;
            Assert.AreSame(instance1, instance2);
        }

        [TestMethod]
        public void Test_Contracts_ReturnsAllNativeContracts()
        {
            var contracts = _registry.Contracts;
            Assert.IsNotNull(contracts);
            Assert.IsTrue(contracts.Count > 0);

            // Verify known contracts exist
            var contractNames = contracts.Select(c => c.Name).ToList();
            Assert.IsTrue(contractNames.Contains("ContractManagement"));
            Assert.IsTrue(contractNames.Contains("NeoToken"));
            Assert.IsTrue(contractNames.Contains("GasToken"));
            Assert.IsTrue(contractNames.Contains("PolicyContract"));
            Assert.IsTrue(contractNames.Contains("LedgerContract"));
        }

        [TestMethod]
        public void Test_GetContract_ByHash_ValidHash()
        {
            // Get a known contract hash
            var neoHash = NativeContract.NEO.Hash.GetSpan().ToArray();
            var contract = _registry.GetContract(neoHash);

            Assert.IsNotNull(contract);
            Assert.AreEqual("NeoToken", contract.Name);
            Assert.AreEqual(NativeContract.NEO.Id, contract.Id);
        }

        [TestMethod]
        public void Test_GetContract_ByHash_InvalidHash()
        {
            var invalidHash = new byte[20];
            var contract = _registry.GetContract(invalidHash);
            Assert.IsNull(contract);
        }

        [TestMethod]
        public void Test_GetContractById_ValidId()
        {
            var contract = _registry.GetContractById(NativeContract.NEO.Id);
            Assert.IsNotNull(contract);
            Assert.AreEqual("NeoToken", contract.Name);
        }

        [TestMethod]
        public void Test_GetContractById_InvalidId()
        {
            var contract = _registry.GetContractById(-999);
            Assert.IsNull(contract);
        }

        [TestMethod]
        public void Test_IsNative_ValidNativeHash()
        {
            var neoHash = NativeContract.NEO.Hash.GetSpan().ToArray();
            Assert.IsTrue(_registry.IsNative(neoHash));

            var gasHash = NativeContract.GAS.Hash.GetSpan().ToArray();
            Assert.IsTrue(_registry.IsNative(gasHash));
        }

        [TestMethod]
        public void Test_IsNative_InvalidHash()
        {
            var invalidHash = new byte[20];
            Assert.IsFalse(_registry.IsNative(invalidHash));
        }

        [TestMethod]
        public void Test_INativeContractData_Properties()
        {
            var contracts = _registry.Contracts;

            foreach (var contract in contracts)
            {
                // All contracts should have valid properties
                Assert.IsNotNull(contract.Name);
                Assert.IsFalse(string.IsNullOrEmpty(contract.Name));
                Assert.IsNotNull(contract.Hash);
                Assert.AreNotEqual(0, contract.Hash.GetHashCode());
            }
        }

        [TestMethod]
        public void Test_NativeContract_ImplementsINativeContractData()
        {
            // Verify that registry returns INativeContractData for NEO
            var neoHash = NativeContract.NEO.Hash.GetSpan().ToArray();
            var neoData = _registry.GetContract(neoHash);

            Assert.IsNotNull(neoData);
            Assert.AreEqual("NeoToken", neoData.Name);
            Assert.AreEqual(NativeContract.NEO.Id, neoData.Id);
            Assert.AreEqual(NativeContract.NEO.Hash, neoData.Hash);
        }

        [TestMethod]
        public void Test_ActiveIn_Property()
        {
            // Test ActiveIn property conversion via registry
            var neoHash = NativeContract.NEO.Hash.GetSpan().ToArray();
            var neoData = _registry.GetContract(neoHash);

            Assert.IsNotNull(neoData);
            // NEO token is active from genesis, so ActiveIn should be null
            Assert.IsNull(neoData.ActiveIn);
        }

        [TestMethod]
        public void Test_GetCurrentIndex_WithSnapshot()
        {
            using var system = TestBlockchain.GetSystem();
            var snapshot = system.StoreView;

            var index = _registry.GetCurrentIndex(snapshot);
            // Genesis block index is 0
            Assert.AreEqual(0u, index);
        }

        [TestMethod]
        public void Test_GetFeePerByte_WithSnapshot()
        {
            using var system = TestBlockchain.GetSystem();
            var snapshot = system.StoreView;

            var fee = _registry.GetFeePerByte(snapshot);
            // Fee should be positive
            Assert.IsTrue(fee >= 0);
        }

        [TestMethod]
        public void Test_GetStoragePrice_WithSnapshot()
        {
            using var system = TestBlockchain.GetSystem();
            var snapshot = system.StoreView;

            var price = _registry.GetStoragePrice(snapshot);
            // Storage price should be positive
            Assert.IsTrue(price > 0);
        }

        [TestMethod]
        public void Test_ContainsTransaction_NonExistent()
        {
            using var system = TestBlockchain.GetSystem();
            var snapshot = system.GetSnapshotCache();

            var nonExistentHash = new byte[32];
            var exists = _registry.ContainsTransaction(snapshot, nonExistentHash);
            Assert.IsFalse(exists);
        }

        [TestMethod]
        public void Test_GetBlock_ByIndex_Genesis()
        {
            using var system = TestBlockchain.GetSystem();
            var snapshot = system.GetSnapshotCache();

            var block = _registry.GetBlock(snapshot, 0);
            Assert.IsNotNull(block);
        }

        [TestMethod]
        public void Test_GetBlock_ByHash_Invalid()
        {
            using var system = TestBlockchain.GetSystem();
            var snapshot = system.GetSnapshotCache();

            var invalidHash = new byte[32];
            var block = _registry.GetBlock(snapshot, invalidHash);
            Assert.IsNull(block);
        }
    }
}
