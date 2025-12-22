// Copyright (C) 2015-2025 The Neo Project.
//
// UT_ProtocolEnums.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.Ledger;
using Neo.Network.P2P;
using Neo.Network.P2P.Payloads;
using Neo.SmartContract;
using System;
using System.Linq;

namespace Neo.UnitTests.Protocol
{
    [TestClass]
    public class UT_ProtocolEnums
    {
        #region MessageCommand Tests

        [TestMethod]
        public void MessageCommand_HandshakingValues()
        {
            Assert.AreEqual(0x00, (byte)MessageCommand.Version);
            Assert.AreEqual(0x01, (byte)MessageCommand.Verack);
        }

        [TestMethod]
        public void MessageCommand_ConnectivityValues()
        {
            Assert.AreEqual(0x10, (byte)MessageCommand.GetAddr);
            Assert.AreEqual(0x11, (byte)MessageCommand.Addr);
            Assert.AreEqual(0x18, (byte)MessageCommand.Ping);
            Assert.AreEqual(0x19, (byte)MessageCommand.Pong);
        }

        [TestMethod]
        public void MessageCommand_SynchronizationValues()
        {
            Assert.AreEqual(0x20, (byte)MessageCommand.GetHeaders);
            Assert.AreEqual(0x21, (byte)MessageCommand.Headers);
            Assert.AreEqual(0x24, (byte)MessageCommand.GetBlocks);
            Assert.AreEqual(0x25, (byte)MessageCommand.Mempool);
            Assert.AreEqual(0x27, (byte)MessageCommand.Inv);
            Assert.AreEqual(0x28, (byte)MessageCommand.GetData);
            Assert.AreEqual(0x29, (byte)MessageCommand.GetBlockByIndex);
            Assert.AreEqual(0x2a, (byte)MessageCommand.NotFound);
            Assert.AreEqual(0x2b, (byte)MessageCommand.Transaction);
            Assert.AreEqual(0x2c, (byte)MessageCommand.Block);
            Assert.AreEqual(0x2e, (byte)MessageCommand.Extensible);
            Assert.AreEqual(0x2f, (byte)MessageCommand.Reject);
        }

        [TestMethod]
        public void MessageCommand_SPVProtocolValues()
        {
            Assert.AreEqual(0x30, (byte)MessageCommand.FilterLoad);
            Assert.AreEqual(0x31, (byte)MessageCommand.FilterAdd);
            Assert.AreEqual(0x32, (byte)MessageCommand.FilterClear);
            Assert.AreEqual(0x38, (byte)MessageCommand.MerkleBlock);
        }

        [TestMethod]
        public void MessageCommand_OtherValues()
        {
            Assert.AreEqual(0x40, (byte)MessageCommand.Alert);
        }

        [TestMethod]
        public void MessageCommand_AllValuesUnique()
        {
            var values = Enum.GetValues<MessageCommand>().Cast<byte>().ToList();
            var uniqueValues = values.Distinct().ToList();
            Assert.AreEqual(values.Count, uniqueValues.Count, "MessageCommand values should be unique");
        }

        [TestMethod]
        public void MessageCommand_IsByteEnum()
        {
            Assert.AreEqual(typeof(byte), Enum.GetUnderlyingType(typeof(MessageCommand)));
        }

        #endregion

        #region InventoryType Tests

        [TestMethod]
        public void InventoryType_Values()
        {
            Assert.AreEqual((byte)MessageCommand.Transaction, (byte)InventoryType.TX);
            Assert.AreEqual((byte)MessageCommand.Block, (byte)InventoryType.Block);
            Assert.AreEqual((byte)MessageCommand.Extensible, (byte)InventoryType.Extensible);
        }

        [TestMethod]
        public void InventoryType_MatchesMessageCommand()
        {
            // InventoryType values should match corresponding MessageCommand values
            Assert.AreEqual(0x2b, (byte)InventoryType.TX);
            Assert.AreEqual(0x2c, (byte)InventoryType.Block);
            Assert.AreEqual(0x2e, (byte)InventoryType.Extensible);
        }

        [TestMethod]
        public void InventoryType_IsByteEnum()
        {
            Assert.AreEqual(typeof(byte), Enum.GetUnderlyingType(typeof(InventoryType)));
        }

        [TestMethod]
        public void InventoryType_AllValuesUnique()
        {
            var values = Enum.GetValues<InventoryType>().Cast<byte>().ToList();
            var uniqueValues = values.Distinct().ToList();
            Assert.AreEqual(values.Count, uniqueValues.Count, "InventoryType values should be unique");
        }

        #endregion

        #region VerifyResult Tests

        [TestMethod]
        public void VerifyResult_SuccessValues()
        {
            Assert.AreEqual(0, (byte)VerifyResult.Succeed);
            Assert.AreEqual(1, (byte)VerifyResult.AlreadyExists);
            Assert.AreEqual(2, (byte)VerifyResult.AlreadyInPool);
        }

        [TestMethod]
        public void VerifyResult_FailureValues()
        {
            Assert.AreEqual(3, (byte)VerifyResult.OutOfMemory);
            Assert.AreEqual(4, (byte)VerifyResult.UnableToVerify);
            Assert.AreEqual(5, (byte)VerifyResult.Invalid);
            Assert.AreEqual(6, (byte)VerifyResult.InvalidScript);
            Assert.AreEqual(7, (byte)VerifyResult.InvalidAttribute);
            Assert.AreEqual(8, (byte)VerifyResult.InvalidSignature);
            Assert.AreEqual(9, (byte)VerifyResult.OverSize);
            Assert.AreEqual(10, (byte)VerifyResult.Expired);
            Assert.AreEqual(11, (byte)VerifyResult.InsufficientFunds);
            Assert.AreEqual(12, (byte)VerifyResult.PolicyFail);
            Assert.AreEqual(13, (byte)VerifyResult.HasConflicts);
            Assert.AreEqual(14, (byte)VerifyResult.Unknown);
        }

        [TestMethod]
        public void VerifyResult_IsByteEnum()
        {
            Assert.AreEqual(typeof(byte), Enum.GetUnderlyingType(typeof(VerifyResult)));
        }

        [TestMethod]
        public void VerifyResult_AllValuesUnique()
        {
            var values = Enum.GetValues<VerifyResult>().Cast<byte>().ToList();
            var uniqueValues = values.Distinct().ToList();
            Assert.AreEqual(values.Count, uniqueValues.Count, "VerifyResult values should be unique");
        }

        [TestMethod]
        public void VerifyResult_HasExpectedCount()
        {
            var count = Enum.GetValues<VerifyResult>().Length;
            Assert.AreEqual(15, count, "VerifyResult should have 15 values");
        }

        #endregion

        #region WitnessScope Tests

        [TestMethod]
        public void WitnessScope_Values()
        {
            Assert.AreEqual(0x00, (byte)WitnessScope.None);
            Assert.AreEqual(0x01, (byte)WitnessScope.CalledByEntry);
            Assert.AreEqual(0x10, (byte)WitnessScope.CustomContracts);
            Assert.AreEqual(0x20, (byte)WitnessScope.CustomGroups);
            Assert.AreEqual(0x40, (byte)WitnessScope.WitnessRules);
            Assert.AreEqual(0x80, (byte)WitnessScope.Global);
        }

        [TestMethod]
        public void WitnessScope_IsFlagsEnum()
        {
            Assert.IsTrue(typeof(WitnessScope).GetCustomAttributes(typeof(FlagsAttribute), false).Any());
        }

        [TestMethod]
        public void WitnessScope_CanCombineFlags()
        {
            var combined = WitnessScope.CalledByEntry | WitnessScope.CustomContracts;
            Assert.AreEqual(0x11, (byte)combined);

            var hasCalledByEntry = (combined & WitnessScope.CalledByEntry) != 0;
            var hasCustomContracts = (combined & WitnessScope.CustomContracts) != 0;
            Assert.IsTrue(hasCalledByEntry);
            Assert.IsTrue(hasCustomContracts);
        }

        [TestMethod]
        public void WitnessScope_GlobalIsExclusive()
        {
            // Global (0x80) should be the highest bit, making it exclusive
            Assert.AreEqual(0x80, (byte)WitnessScope.Global);
        }

        #endregion

        #region TriggerType Tests

        [TestMethod]
        public void TriggerType_Values()
        {
            Assert.AreEqual(0x01, (byte)TriggerType.OnPersist);
            Assert.AreEqual(0x02, (byte)TriggerType.PostPersist);
            Assert.AreEqual(0x20, (byte)TriggerType.Verification);
            Assert.AreEqual(0x40, (byte)TriggerType.Application);
        }

        [TestMethod]
        public void TriggerType_SystemCombination()
        {
            Assert.AreEqual(TriggerType.OnPersist | TriggerType.PostPersist, TriggerType.System);
            Assert.AreEqual(0x03, (byte)TriggerType.System);
        }

        [TestMethod]
        public void TriggerType_AllCombination()
        {
            var expected = TriggerType.OnPersist | TriggerType.PostPersist | TriggerType.Verification | TriggerType.Application;
            Assert.AreEqual(expected, TriggerType.All);
            Assert.AreEqual(0x63, (byte)TriggerType.All);
        }

        [TestMethod]
        public void TriggerType_IsFlagsEnum()
        {
            Assert.IsTrue(typeof(TriggerType).GetCustomAttributes(typeof(FlagsAttribute), false).Any());
        }

        [TestMethod]
        public void TriggerType_CanCheckFlags()
        {
            var trigger = TriggerType.System;
            Assert.IsTrue((trigger & TriggerType.OnPersist) != 0);
            Assert.IsTrue((trigger & TriggerType.PostPersist) != 0);
            Assert.IsFalse((trigger & TriggerType.Verification) != 0);
            Assert.IsFalse((trigger & TriggerType.Application) != 0);
        }

        #endregion
    }
}
