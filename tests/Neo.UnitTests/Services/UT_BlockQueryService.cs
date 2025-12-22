// Copyright (C) 2015-2025 The Neo Project.
//
// UT_BlockQueryService.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.Network.P2P.Payloads;
using Neo.Services.Blocks;
using System;
using System.Linq;

namespace Neo.UnitTests.Services
{
    [TestClass]
    public class UT_BlockQueryService
    {
        private NeoSystem _system;
        private BlockQueryService _service;

        [TestInitialize]
        public void Setup()
        {
            _system = TestBlockchain.GetSystem();
            _service = new BlockQueryService(_system);
        }

        [TestMethod]
        public void Constructor_NullProvider_DoesNotThrow()
        {
            // Note: Current implementation does not validate null parameter
            // This test documents the actual behavior
            var service = new BlockQueryService(null!);
            Assert.IsNotNull(service);
        }

        [TestMethod]
        public void GetBlockByIndex_ValidIndex_ReturnsBlock()
        {
            // Genesis block is always at index 0
            var block = _service.GetBlockByIndex(0);

            Assert.IsNotNull(block);
            Assert.AreEqual(0u, block.Index);
            Assert.AreEqual(_system.GenesisBlock.Hash, block.Hash);
        }

        [TestMethod]
        public void GetBlockByIndex_InvalidIndex_ReturnsNull()
        {
            // Test with a very high index that doesn't exist
            var block = _service.GetBlockByIndex(uint.MaxValue);

            Assert.IsNull(block);
        }

        [TestMethod]
        public void GetBlockByHash_ValidHash_ReturnsBlock()
        {
            // GetBlockByHash expects hex string in little-endian format (not reversed)
            // We need to get the hash bytes in little-endian and convert to hex
            var genesisHashBytes = new byte[32];
            _system.GenesisBlock.Hash.GetSpan().CopyTo(genesisHashBytes);
            var genesisHashHex = Convert.ToHexString(genesisHashBytes);

            var block = _service.GetBlockByHash(genesisHashHex);

            Assert.IsNotNull(block);
            Assert.AreEqual(_system.GenesisBlock.Hash, block.Hash);
            Assert.AreEqual(0u, block.Index);
        }

        [TestMethod]
        public void GetBlockByHash_ValidHashWith0xPrefix_ReturnsBlock()
        {
            // GetBlockByHash expects hex string in little-endian format (not reversed)
            var genesisHashBytes = new byte[32];
            _system.GenesisBlock.Hash.GetSpan().CopyTo(genesisHashBytes);
            var genesisHashHex = "0x" + Convert.ToHexString(genesisHashBytes);

            var block = _service.GetBlockByHash(genesisHashHex);

            Assert.IsNotNull(block);
            Assert.AreEqual(_system.GenesisBlock.Hash, block.Hash);
        }

        [TestMethod]
        public void GetBlockByHash_InvalidHash_ReturnsNull()
        {
            // Create a hash that doesn't exist in the blockchain
            var invalidHash = new byte[32];
            for (int i = 0; i < 32; i++)
                invalidHash[i] = 0xFF;

            var block = _service.GetBlockByHash(Convert.ToHexString(invalidHash));

            Assert.IsNull(block);
        }

        [TestMethod]
        public void GetBlockByHash_InvalidHexString_ReturnsNull()
        {
            // Test with invalid hex characters
            var block = _service.GetBlockByHash("ZZZZZZZZ");

            Assert.IsNull(block);
        }

        [TestMethod]
        public void GetBlockByHash_NullString_ReturnsNull()
        {
            var block = _service.GetBlockByHash(null!);

            Assert.IsNull(block);
        }

        [TestMethod]
        public void GetBlockByHash_EmptyString_ReturnsNull()
        {
            var block = _service.GetBlockByHash("");

            Assert.IsNull(block);
        }

        [TestMethod]
        public void GetBlockByHash_WhitespaceString_ReturnsNull()
        {
            var block = _service.GetBlockByHash("   ");

            Assert.IsNull(block);
        }

        [TestMethod]
        public void GetBlockByHash_WrongLength_ReturnsNull()
        {
            // Test with a hex string that's not 32 bytes (64 hex chars)
            var block = _service.GetBlockByHash("0123456789ABCDEF");

            Assert.IsNull(block);
        }

        [TestMethod]
        public void GetBlocks_ValidRange_ReturnsBlocks()
        {
            // Get genesis block
            var blocks = _service.GetBlocks(0, 1).ToList();

            Assert.AreEqual(1, blocks.Count);
            Assert.AreEqual(0u, blocks[0].Index);
            Assert.AreEqual(_system.GenesisBlock.Hash, blocks[0].Hash);
        }

        [TestMethod]
        public void GetBlocks_ZeroCount_ReturnsEmpty()
        {
            var blocks = _service.GetBlocks(0, 0).ToList();

            Assert.AreEqual(0, blocks.Count);
        }

        [TestMethod]
        public void GetBlocks_NegativeCount_ReturnsEmpty()
        {
            var blocks = _service.GetBlocks(0, -1).ToList();

            Assert.AreEqual(0, blocks.Count);
        }

        [TestMethod]
        public void GetBlocks_InvalidRange_ReturnsOnlyExistingBlocks()
        {
            // Request blocks that don't exist
            var blocks = _service.GetBlocks(uint.MaxValue - 10, 5).ToList();

            // Should return empty since those blocks don't exist
            Assert.AreEqual(0, blocks.Count);
        }

        [TestMethod]
        public void GetBlocks_PartialRange_ReturnsOnlyExistingBlocks()
        {
            // Request from genesis block with a large count
            // Only genesis block exists in test environment
            var blocks = _service.GetBlocks(0, 100).ToList();

            // Should return at least the genesis block
            Assert.IsTrue(blocks.Count >= 1);
            Assert.AreEqual(0u, blocks[0].Index);
        }

        [TestMethod]
        public void GetBlocks_MultipleBlocks_ReturnsInOrder()
        {
            // Get blocks starting from genesis
            var blocks = _service.GetBlocks(0, 10).ToList();

            // Verify blocks are in ascending order
            for (int i = 0; i < blocks.Count - 1; i++)
            {
                Assert.IsTrue(blocks[i].Index < blocks[i + 1].Index);
            }
        }
    }
}
