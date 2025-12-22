// Copyright (C) 2015-2025 The Neo Project.
//
// UT_LedgerContract.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.Extensions;
using Neo.IO;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.Persistence.Providers;
using Neo.SmartContract;
using Neo.SmartContract.Native;
using Neo.UnitTests.Extensions;
using Neo.VM;
using System;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;

namespace Neo.UnitTests.SmartContract.Native
{
    [TestClass]
    public class UT_LedgerContract
    {
        private DataCache _snapshotCache;
        private Block _persistingBlock;

        private const byte Prefix_Block = 5;
        private const byte Prefix_BlockHash = 9;
        private const byte Prefix_Transaction = 11;
        private const byte Prefix_CurrentBlock = 12;

        [TestInitialize]
        public void TestSetup()
        {
            _snapshotCache = TestBlockchain.GetTestSnapshotCache();
            _persistingBlock = new Block
            {
                Header = (Header)RuntimeHelpers.GetUninitializedObject(typeof(Header)),
                Transactions = []
            };
        }

        [TestMethod]
        public void Check_Initialized()
        {
            var snapshot = _snapshotCache.CloneCache();

            // Should be initialized after TestBlockchain setup
            Assert.IsTrue(NativeContract.Ledger.Initialized(snapshot));

            // Test with empty snapshot - use a fresh MemoryStore
            var emptyStore = new MemoryStore();
            var emptySnapshot = new StoreCache(emptyStore.GetSnapshot());
            Assert.IsFalse(NativeContract.Ledger.Initialized(emptySnapshot));
        }

        [TestMethod]
        public void Check_CurrentHash()
        {
            var snapshot = _snapshotCache.CloneCache();
            var currentHash = NativeContract.Ledger.CurrentHash(snapshot);

            Assert.IsNotNull(currentHash);
            Assert.AreEqual(32, currentHash.Size);
        }

        [TestMethod]
        public void Check_CurrentIndex()
        {
            var snapshot = _snapshotCache.CloneCache();
            var currentIndex = NativeContract.Ledger.CurrentIndex(snapshot);

            Assert.IsTrue(currentIndex >= 0);
        }

        [TestMethod]
        public void Check_GetBlockHash()
        {
            var snapshot = _snapshotCache.CloneCache();

            // Get genesis block hash
            var hash = NativeContract.Ledger.GetBlockHash(snapshot, 0);
            Assert.IsNotNull(hash);

            // Non-existent block
            var nonExistentHash = NativeContract.Ledger.GetBlockHash(snapshot, uint.MaxValue);
            Assert.IsNull(nonExistentHash);
        }

        [TestMethod]
        public void Check_ContainsBlock()
        {
            var snapshot = _snapshotCache.CloneCache();

            // Create and add a test block
            var testBlock = CreateTestTrimmedBlock(1);
            TestUtils.BlocksAdd(snapshot, testBlock.Hash, testBlock);

            Assert.IsTrue(NativeContract.Ledger.ContainsBlock(snapshot, testBlock.Hash));
            Assert.IsFalse(NativeContract.Ledger.ContainsBlock(snapshot, UInt256.Zero));
        }

        [TestMethod]
        public void Check_GetTrimmedBlock()
        {
            var snapshot = _snapshotCache.CloneCache();

            // Create and add a test block
            var testBlock = CreateTestTrimmedBlock(1);
            TestUtils.BlocksAdd(snapshot, testBlock.Hash, testBlock);

            var retrievedBlock = NativeContract.Ledger.GetTrimmedBlock(snapshot, testBlock.Hash);
            Assert.IsNotNull(retrievedBlock);
            Assert.AreEqual(testBlock.Hash, retrievedBlock.Hash);
            Assert.AreEqual(testBlock.Index, retrievedBlock.Index);

            // Non-existent block
            var nonExistent = NativeContract.Ledger.GetTrimmedBlock(snapshot, UInt256.Zero);
            Assert.IsNull(nonExistent);
        }

        [TestMethod]
        public void Check_GetBlock_ByHash()
        {
            var snapshot = _snapshotCache.CloneCache();

            // Create test transaction
            var tx = CreateTestTransaction();

            // Create and add a test block with transaction
            var testBlock = CreateTestBlock(1, tx);
            TestUtils.BlocksAdd(snapshot, testBlock.Hash, testBlock);

            var retrievedBlock = NativeContract.Ledger.GetBlock(snapshot, testBlock.Hash);
            Assert.IsNotNull(retrievedBlock);
            Assert.AreEqual(testBlock.Hash, retrievedBlock.Hash);
            Assert.AreEqual(testBlock.Index, retrievedBlock.Index);
            Assert.AreEqual(1, retrievedBlock.Transactions.Length);
            Assert.AreEqual(tx.Hash, retrievedBlock.Transactions[0].Hash);
        }

        [TestMethod]
        public void Check_GetBlock_ByIndex()
        {
            var snapshot = _snapshotCache.CloneCache();

            // Create test transaction
            var tx = CreateTestTransaction();

            // Create and add a test block
            var testBlock = CreateTestBlock(1, tx);
            TestUtils.BlocksAdd(snapshot, testBlock.Hash, testBlock);

            var retrievedBlock = NativeContract.Ledger.GetBlock(snapshot, testBlock.Index);
            Assert.IsNotNull(retrievedBlock);
            Assert.AreEqual(testBlock.Hash, retrievedBlock.Hash);
            Assert.AreEqual(testBlock.Index, retrievedBlock.Index);
        }

        [TestMethod]
        public void Check_GetHeader_ByHash()
        {
            var snapshot = _snapshotCache.CloneCache();

            var testBlock = CreateTestTrimmedBlock(1);
            TestUtils.BlocksAdd(snapshot, testBlock.Hash, testBlock);

            var header = NativeContract.Ledger.GetHeader(snapshot, testBlock.Hash);
            Assert.IsNotNull(header);
            Assert.AreEqual(testBlock.Hash, header.Hash);
            Assert.AreEqual(testBlock.Index, header.Index);
        }

        [TestMethod]
        public void Check_GetHeader_ByIndex()
        {
            var snapshot = _snapshotCache.CloneCache();

            var testBlock = CreateTestTrimmedBlock(1);
            TestUtils.BlocksAdd(snapshot, testBlock.Hash, testBlock);

            var header = NativeContract.Ledger.GetHeader(snapshot, testBlock.Index);
            Assert.IsNotNull(header);
            Assert.AreEqual(testBlock.Hash, header.Hash);
        }

        [TestMethod]
        public void Check_ContainsTransaction()
        {
            var snapshot = _snapshotCache.CloneCache();

            var tx = CreateTestTransaction();
            var testBlock = CreateTestBlock(1, tx);
            TestUtils.BlocksAdd(snapshot, testBlock.Hash, testBlock);

            Assert.IsTrue(NativeContract.Ledger.ContainsTransaction(snapshot, tx.Hash));
            Assert.IsFalse(NativeContract.Ledger.ContainsTransaction(snapshot, UInt256.Zero));
        }

        [TestMethod]
        public void Check_GetTransactionState()
        {
            var snapshot = _snapshotCache.CloneCache();

            var tx = CreateTestTransaction();
            var testBlock = CreateTestBlock(1, tx);
            TestUtils.BlocksAdd(snapshot, testBlock.Hash, testBlock);

            var txState = NativeContract.Ledger.GetTransactionState(snapshot, tx.Hash);
            Assert.IsNotNull(txState);
            Assert.AreEqual(testBlock.Index, txState.BlockIndex);
            Assert.IsNotNull(txState.Transaction);
            Assert.AreEqual(tx.Hash, txState.Transaction.Hash);

            // Non-existent transaction
            var nonExistent = NativeContract.Ledger.GetTransactionState(snapshot, UInt256.Zero);
            Assert.IsNull(nonExistent);
        }

        [TestMethod]
        public void Check_GetTransaction()
        {
            var snapshot = _snapshotCache.CloneCache();

            var tx = CreateTestTransaction();
            var testBlock = CreateTestBlock(1, tx);
            TestUtils.BlocksAdd(snapshot, testBlock.Hash, testBlock);

            var retrievedTx = NativeContract.Ledger.GetTransaction(snapshot, tx.Hash);
            Assert.IsNotNull(retrievedTx);
            Assert.AreEqual(tx.Hash, retrievedTx.Hash);
        }

        [TestMethod]
        public void Check_GetBlock_ViaContract()
        {
            var snapshot = _snapshotCache.CloneCache();

            var tx = CreateTestTransaction();
            var testBlock = CreateTestBlock(1, tx);
            TestUtils.BlocksAdd(snapshot, testBlock.Hash, testBlock);

            // Test getBlock by hash
            using var script = new ScriptBuilder();
            script.EmitDynamicCall(NativeContract.Ledger.Hash, "getBlock", testBlock.Hash.ToArray());

            var engine = ApplicationEngine.Create(TriggerType.Application, null, snapshot, settings: TestProtocolSettings.Default);
            engine.LoadScript(script.ToArray());

            Assert.AreEqual(VMState.HALT, engine.Execute());
            Assert.AreEqual(1, engine.ResultStack.Count);

            var result = engine.ResultStack.Pop();
            Assert.IsFalse(result.IsNull);
        }

        [TestMethod]
        public void Check_GetBlock_ViaContract_ByIndex()
        {
            var snapshot = _snapshotCache.CloneCache();

            var tx = CreateTestTransaction();
            var testBlock = CreateTestBlock(1, tx);
            TestUtils.BlocksAdd(snapshot, testBlock.Hash, testBlock);

            // Test getBlock by index
            using var script = new ScriptBuilder();
            script.EmitDynamicCall(NativeContract.Ledger.Hash, "getBlock", new BigInteger(testBlock.Index).ToByteArray());

            var engine = ApplicationEngine.Create(TriggerType.Application, null, snapshot, settings: TestProtocolSettings.Default);
            engine.LoadScript(script.ToArray());

            Assert.AreEqual(VMState.HALT, engine.Execute());
            Assert.AreEqual(1, engine.ResultStack.Count);

            var result = engine.ResultStack.Pop();
            Assert.IsFalse(result.IsNull);
        }

        [TestMethod]
        public void Check_GetTransaction_ViaContract()
        {
            var snapshot = _snapshotCache.CloneCache();

            var tx = CreateTestTransaction();
            var testBlock = CreateTestBlock(1, tx);
            TestUtils.BlocksAdd(snapshot, testBlock.Hash, testBlock);

            using var script = new ScriptBuilder();
            script.EmitDynamicCall(NativeContract.Ledger.Hash, "getTransaction", tx.Hash);

            var engine = ApplicationEngine.Create(TriggerType.Application, null, snapshot, settings: TestProtocolSettings.Default);
            engine.LoadScript(script.ToArray());

            Assert.AreEqual(VMState.HALT, engine.Execute());
            Assert.AreEqual(1, engine.ResultStack.Count);

            var result = engine.ResultStack.Pop();
            Assert.IsFalse(result.IsNull);
        }

        [TestMethod]
        public void Check_GetTransactionHeight()
        {
            var snapshot = _snapshotCache.CloneCache();

            var tx = CreateTestTransaction();
            var testBlock = CreateTestBlock(5, tx);
            TestUtils.BlocksAdd(snapshot, testBlock.Hash, testBlock);

            using var script = new ScriptBuilder();
            script.EmitDynamicCall(NativeContract.Ledger.Hash, "getTransactionHeight", tx.Hash);

            var engine = ApplicationEngine.Create(TriggerType.Application, null, snapshot, settings: TestProtocolSettings.Default);
            engine.LoadScript(script.ToArray());

            Assert.AreEqual(VMState.HALT, engine.Execute());
            Assert.AreEqual(1, engine.ResultStack.Count);

            var height = engine.ResultStack.Pop().GetInteger();
            Assert.AreEqual(5, height);
        }

        [TestMethod]
        public void Check_GetTransactionHeight_NonExistent()
        {
            var snapshot = _snapshotCache.CloneCache();

            using var script = new ScriptBuilder();
            script.EmitDynamicCall(NativeContract.Ledger.Hash, "getTransactionHeight", UInt256.Zero);

            var engine = ApplicationEngine.Create(TriggerType.Application, null, snapshot, settings: TestProtocolSettings.Default);
            engine.LoadScript(script.ToArray());

            Assert.AreEqual(VMState.HALT, engine.Execute());
            Assert.AreEqual(1, engine.ResultStack.Count);

            var height = engine.ResultStack.Pop().GetInteger();
            Assert.AreEqual(-1, height);
        }

        [TestMethod]
        public void Check_GetTransactionSigners()
        {
            var snapshot = _snapshotCache.CloneCache();

            var tx = CreateTestTransaction();
            var testBlock = CreateTestBlock(1, tx);
            TestUtils.BlocksAdd(snapshot, testBlock.Hash, testBlock);

            using var script = new ScriptBuilder();
            script.EmitDynamicCall(NativeContract.Ledger.Hash, "getTransactionSigners", tx.Hash);

            var engine = ApplicationEngine.Create(TriggerType.Application, null, snapshot, settings: TestProtocolSettings.Default);
            engine.LoadScript(script.ToArray());

            Assert.AreEqual(VMState.HALT, engine.Execute());
            Assert.AreEqual(1, engine.ResultStack.Count);

            var result = engine.ResultStack.Pop();
            Assert.IsFalse(result.IsNull);
        }

        [TestMethod]
        public void Check_GetTransactionVMState()
        {
            var snapshot = _snapshotCache.CloneCache();

            var tx = CreateTestTransaction();
            var testBlock = CreateTestBlock(1, tx);
            TestUtils.BlocksAdd(snapshot, testBlock.Hash, testBlock);

            using var script = new ScriptBuilder();
            script.EmitDynamicCall(NativeContract.Ledger.Hash, "getTransactionVMState", tx.Hash);

            var engine = ApplicationEngine.Create(TriggerType.Application, null, snapshot, settings: TestProtocolSettings.Default);
            engine.LoadScript(script.ToArray());

            Assert.AreEqual(VMState.HALT, engine.Execute());
            Assert.AreEqual(1, engine.ResultStack.Count);

            var vmState = (VMState)(byte)engine.ResultStack.Pop().GetInteger();
            Assert.AreEqual(VMState.NONE, vmState);
        }

        [TestMethod]
        public void Check_GetTransactionFromBlock()
        {
            var snapshot = _snapshotCache.CloneCache();

            var tx = CreateTestTransaction();
            var testBlock = CreateTestBlock(1, tx);
            TestUtils.BlocksAdd(snapshot, testBlock.Hash, testBlock);

            using var script = new ScriptBuilder();
            script.EmitDynamicCall(NativeContract.Ledger.Hash, "getTransactionFromBlock", testBlock.Hash.ToArray(), 0);

            var engine = ApplicationEngine.Create(TriggerType.Application, null, snapshot, settings: TestProtocolSettings.Default);
            engine.LoadScript(script.ToArray());

            Assert.AreEqual(VMState.HALT, engine.Execute());
            Assert.AreEqual(1, engine.ResultStack.Count);

            var result = engine.ResultStack.Pop();
            Assert.IsFalse(result.IsNull);
        }

        [TestMethod]
        public void Check_GetTransactionFromBlock_InvalidIndex()
        {
            var snapshot = _snapshotCache.CloneCache();

            var tx = CreateTestTransaction();
            var testBlock = CreateTestBlock(1, tx);
            TestUtils.BlocksAdd(snapshot, testBlock.Hash, testBlock);

            using var script = new ScriptBuilder();
            script.EmitDynamicCall(NativeContract.Ledger.Hash, "getTransactionFromBlock", testBlock.Hash.ToArray(), 10);

            var engine = ApplicationEngine.Create(TriggerType.Application, null, snapshot, settings: TestProtocolSettings.Default);
            engine.LoadScript(script.ToArray());

            Assert.AreEqual(VMState.FAULT, engine.Execute());
        }

        [TestMethod]
        public void Check_ContainsConflictHash_NoConflict()
        {
            var snapshot = _snapshotCache.CloneCache();

            var conflictHash = TestUtils.RandomUInt256();
            var signers = new[] { TestUtils.RandomUInt160() };

            var result = NativeContract.Ledger.ContainsConflictHash(snapshot, conflictHash, signers, TestProtocolSettings.Default.MaxTraceableBlocks);
            Assert.IsFalse(result);
        }

        [TestMethod]
        public void Check_ContainsConflictHash_WithConflict()
        {
            var snapshot = _snapshotCache.CloneCache();

            var conflictHash = TestUtils.RandomUInt256();
            var signer = TestUtils.RandomUInt160();
            var signers = new[] { signer };

            // Add conflict record
            var conflictKey = NativeContract.Ledger.CreateStorageKey(Prefix_Transaction, conflictHash);
            snapshot.Add(conflictKey, new StorageItem(new TransactionState { BlockIndex = 1 }));

            var signerKey = StorageKey.Create(NativeContract.Ledger.Id, Prefix_Transaction, conflictHash, signer);
            snapshot.Add(signerKey, new StorageItem(new TransactionState { BlockIndex = 1 }));

            // Update current block to make it traceable
            var currentBlockKey = NativeContract.Ledger.CreateStorageKey(Prefix_CurrentBlock);
            var currentState = snapshot.GetAndChange(currentBlockKey).GetInteroperable<HashIndexState>();
            currentState.Index = 100;

            var result = NativeContract.Ledger.ContainsConflictHash(snapshot, conflictHash, signers, TestProtocolSettings.Default.MaxTraceableBlocks);
            Assert.IsTrue(result);
        }

        [TestMethod]
        public void Check_ContainsConflictHash_NotTraceable()
        {
            var snapshot = _snapshotCache.CloneCache();

            var conflictHash = TestUtils.RandomUInt256();
            var signer = TestUtils.RandomUInt160();
            var signers = new[] { signer };

            // Add conflict record at old block
            var conflictKey = NativeContract.Ledger.CreateStorageKey(Prefix_Transaction, conflictHash);
            snapshot.Add(conflictKey, new StorageItem(new TransactionState { BlockIndex = 1 }));

            var signerKey = StorageKey.Create(NativeContract.Ledger.Id, Prefix_Transaction, conflictHash, signer);
            snapshot.Add(signerKey, new StorageItem(new TransactionState { BlockIndex = 1 }));

            // Update current block to make it not traceable
            var currentBlockKey = NativeContract.Ledger.CreateStorageKey(Prefix_CurrentBlock);
            var currentState = snapshot.GetAndChange(currentBlockKey).GetInteroperable<HashIndexState>();
            currentState.Index = TestProtocolSettings.Default.MaxTraceableBlocks + 100;

            var result = NativeContract.Ledger.ContainsConflictHash(snapshot, conflictHash, signers, TestProtocolSettings.Default.MaxTraceableBlocks);
            Assert.IsFalse(result);
        }

        [TestMethod]
        public void Check_Traceable_Block()
        {
            var snapshot = _snapshotCache.CloneCache();

            var tx = CreateTestTransaction();
            var testBlock = CreateTestBlock(1, tx);
            TestUtils.BlocksAdd(snapshot, testBlock.Hash, testBlock);

            // Update current block
            var currentBlockKey = NativeContract.Ledger.CreateStorageKey(Prefix_CurrentBlock);
            var currentState = snapshot.GetAndChange(currentBlockKey).GetInteroperable<HashIndexState>();
            currentState.Index = TestProtocolSettings.Default.MaxTraceableBlocks + 10;

            // Try to get block via contract - should return null because not traceable
            using var script = new ScriptBuilder();
            script.EmitDynamicCall(NativeContract.Ledger.Hash, "getBlock", testBlock.Hash.ToArray());

            var engine = ApplicationEngine.Create(TriggerType.Application, null, snapshot, settings: TestProtocolSettings.Default);
            engine.LoadScript(script.ToArray());

            Assert.AreEqual(VMState.HALT, engine.Execute());
            Assert.AreEqual(1, engine.ResultStack.Count);

            var result = engine.ResultStack.Pop();
            Assert.IsTrue(result.IsNull);
        }

        [TestMethod]
        public void Check_OnPersist_StoresBlockAndTransactions()
        {
            var snapshot = _snapshotCache.CloneCache();

            var tx1 = CreateTestTransaction();
            var tx2 = CreateTestTransaction();

            var block = new Block
            {
                Header = new Header
                {
                    Index = 100,
                    PrevHash = UInt256.Zero,
                    MerkleRoot = UInt256.Zero,
                    Timestamp = 12345,
                    NextConsensus = UInt160.Zero,
                    Witness = Witness.Empty
                },
                Transactions = new[] { tx1, tx2 }
            };

            // Simulate OnPersist
            var engine = ApplicationEngine.Create(TriggerType.System, null, snapshot, block, settings: TestProtocolSettings.Default);
            NativeContract.Ledger.OnPersistAsync(engine).GetAwaiter().GetResult();

            // Verify block hash is stored
            var blockHashKey = NativeContract.Ledger.CreateStorageKey(Prefix_BlockHash, block.Index);
            Assert.IsTrue(snapshot.Contains(blockHashKey));

            // Verify trimmed block is stored
            var blockKey = NativeContract.Ledger.CreateStorageKey(Prefix_Block, block.Hash);
            Assert.IsTrue(snapshot.Contains(blockKey));

            // Verify transactions are stored
            var tx1Key = NativeContract.Ledger.CreateStorageKey(Prefix_Transaction, tx1.Hash);
            Assert.IsTrue(snapshot.Contains(tx1Key));

            var tx2Key = NativeContract.Ledger.CreateStorageKey(Prefix_Transaction, tx2.Hash);
            Assert.IsTrue(snapshot.Contains(tx2Key));
        }

        [TestMethod]
        public void Check_OnPersist_WithConflicts()
        {
            var snapshot = _snapshotCache.CloneCache();

            var conflictHash = TestUtils.RandomUInt256();
            var signer = TestUtils.RandomUInt160();

            var tx = new Transaction
            {
                Script = new byte[] { 0x01 },
                Attributes = new TransactionAttribute[] { new Conflicts { Hash = conflictHash } },
                Signers = new[] { new Signer { Account = signer, Scopes = WitnessScope.None } },
                NetworkFee = 0,
                SystemFee = 0,
                Nonce = 0,
                ValidUntilBlock = 100,
                Version = 0,
                Witnesses = new[] { Witness.Empty }
            };

            var block = new Block
            {
                Header = new Header
                {
                    Index = 10,
                    PrevHash = UInt256.Zero,
                    MerkleRoot = UInt256.Zero,
                    Timestamp = 12345,
                    NextConsensus = UInt160.Zero,
                    Witness = Witness.Empty
                },
                Transactions = new[] { tx }
            };

            // Simulate OnPersist
            var engine = ApplicationEngine.Create(TriggerType.System, null, snapshot, block, settings: TestProtocolSettings.Default);
            NativeContract.Ledger.OnPersistAsync(engine).GetAwaiter().GetResult();

            // Verify conflict record is stored
            var conflictKey = NativeContract.Ledger.CreateStorageKey(Prefix_Transaction, conflictHash);
            Assert.IsTrue(snapshot.Contains(conflictKey));

            var conflictState = snapshot[conflictKey].GetInteroperable<TransactionState>();
            Assert.IsNull(conflictState.Transaction);
            Assert.AreEqual(block.Index, conflictState.BlockIndex);

            // Verify signer-specific conflict record
            var signerKey = StorageKey.Create(NativeContract.Ledger.Id, Prefix_Transaction, conflictHash, signer);
            Assert.IsTrue(snapshot.Contains(signerKey));
        }

        [TestMethod]
        public void Check_PostPersist_UpdatesCurrentBlock()
        {
            var snapshot = _snapshotCache.CloneCache();

            var block = new Block
            {
                Header = new Header
                {
                    Index = 999,
                    PrevHash = UInt256.Zero,
                    MerkleRoot = UInt256.Zero,
                    Timestamp = 12345,
                    NextConsensus = UInt160.Zero,
                    Witness = Witness.Empty
                },
                Transactions = []
            };

            // Simulate PostPersist
            var engine = ApplicationEngine.Create(TriggerType.System, null, snapshot, block, settings: TestProtocolSettings.Default);
            NativeContract.Ledger.PostPersistAsync(engine).GetAwaiter().GetResult();

            // Verify current block is updated
            var currentHash = NativeContract.Ledger.CurrentHash(snapshot);
            var currentIndex = NativeContract.Ledger.CurrentIndex(snapshot);

            Assert.AreEqual(block.Hash, currentHash);
            Assert.AreEqual(block.Index, currentIndex);
        }

        // Helper methods

        private TrimmedBlock CreateTestTrimmedBlock(uint index)
        {
            return new TrimmedBlock
            {
                Header = new Header
                {
                    Index = index,
                    Timestamp = 12345,
                    Witness = Witness.Empty,
                    PrevHash = UInt256.Zero,
                    MerkleRoot = UInt256.Zero,
                    PrimaryIndex = 0,
                    NextConsensus = UInt160.Zero
                },
                Hashes = []
            };
        }

        private Block CreateTestBlock(uint index, Transaction tx)
        {
            return new Block
            {
                Header = new Header
                {
                    Index = index,
                    Timestamp = 12345,
                    Witness = Witness.Empty,
                    PrevHash = UInt256.Zero,
                    MerkleRoot = UInt256.Zero,
                    PrimaryIndex = 0,
                    NextConsensus = UInt160.Zero
                },
                Transactions = new[] { tx }
            };
        }

        private Transaction CreateTestTransaction()
        {
            return new Transaction
            {
                Script = new byte[] { 0x01 },
                Attributes = [],
                Signers = new[] { new Signer { Account = UInt160.Zero, Scopes = WitnessScope.None } },
                NetworkFee = 0,
                SystemFee = 0,
                Nonce = (uint)TestUtils.TestRandom.Next(),
                ValidUntilBlock = 100,
                Version = 0,
                Witnesses = new[] { Witness.Empty }
            };
        }
    }
}
