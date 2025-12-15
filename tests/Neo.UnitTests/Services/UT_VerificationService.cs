// Copyright (C) 2015-2025 The Neo Project.
//
// UT_VerificationService.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.Core.Interfaces;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.Services;
using System;

namespace Neo.UnitTests.Services
{
    [TestClass]
    public class UT_VerificationService
    {
        private TestVerifiable _verifiable;
        private VerificationService _service;

        [TestInitialize]
        public void Setup()
        {
            _verifiable = new TestVerifiable();
            var snapshot = TestBlockchain.GetTestSnapshotCache();
            _service = new VerificationService(snapshot);
        }

        [TestMethod]
        public void TestConstructor_NullSnapshot_ThrowsArgumentNullException()
        {
            Assert.ThrowsExactly<ArgumentNullException>(() => new VerificationService(null!));
        }

        [TestMethod]
        public void TestGetScriptHashesForVerifying_WithNonIVerifiable_ThrowsNotSupportedException()
        {
            var nonVerifiable = new NonVerifiableObject();
            Assert.ThrowsExactly<NotSupportedException>(() => _service.GetScriptHashesForVerifying(nonVerifiable));
        }

        [TestMethod]
        public void TestVerify_WithNonIVerifiable_ThrowsNotSupportedException()
        {
            var nonVerifiable = new NonVerifiableObject();
            Assert.ThrowsExactly<NotSupportedException>(() => _service.Verify(nonVerifiable));
        }

        [TestMethod]
        public void TestVerify_WithContext_WithNonIVerifiable_ThrowsNotSupportedException()
        {
            var nonVerifiable = new NonVerifiableObject();
            Assert.ThrowsExactly<NotSupportedException>(() => _service.Verify(nonVerifiable, new object()));
        }

        /// <summary>
        /// Test class that implements IVerifiableBase but not IVerifiable.
        /// </summary>
        private class NonVerifiableObject : IVerifiableBase
        {
            public UInt256 Hash => UInt256.Zero;
            public IWitness[] Witnesses => [];
            public int Size => 0;
            public void DeserializeUnsigned(ref Neo.IO.MemoryReader reader) { }
            public void SerializeUnsigned(System.IO.BinaryWriter writer) { }
            public void Deserialize(ref Neo.IO.MemoryReader reader) { }
            public void Serialize(System.IO.BinaryWriter writer) { }
        }
    }
}
