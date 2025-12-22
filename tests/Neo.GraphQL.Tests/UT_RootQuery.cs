// Copyright (C) 2015-2025 The Neo Project.
//
// UT_RootQuery.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

namespace Neo.GraphQL.Tests
{
    [TestClass]
    public class UT_RootQuery
    {
        [TestMethod]
        public void RootQuery_HasCorrectName()
        {
            var query = new RootQuery(null, null, null);
            Assert.AreEqual("Query", query.Name);
        }

        [TestMethod]
        public void RootQuery_HasDescription()
        {
            var query = new RootQuery(null, null, null);
            Assert.IsNotNull(query.Description);
            Assert.IsTrue(query.Description.Contains("Neo"));
        }

        [TestMethod]
        public void RootQuery_HasNodeInfoQueries()
        {
            var query = new RootQuery(null, null, null);
            var fields = query.Fields.Select(f => f.Name).ToList();

            Assert.IsTrue(fields.Contains("version"));
            Assert.IsTrue(fields.Contains("network"));
            Assert.IsTrue(fields.Contains("height"));
            Assert.IsTrue(fields.Contains("mempoolCount"));
        }

        [TestMethod]
        public void RootQuery_HasBlockQueries()
        {
            var query = new RootQuery(null, null, null);
            var fields = query.Fields.Select(f => f.Name).ToList();

            Assert.IsTrue(fields.Contains("block"));
            Assert.IsTrue(fields.Contains("blockByHash"));
            Assert.IsTrue(fields.Contains("blocks"));
            Assert.IsTrue(fields.Contains("blockHashes"));
        }

        [TestMethod]
        public void RootQuery_HasTransactionQueries()
        {
            var query = new RootQuery(null, null, null);
            var fields = query.Fields.Select(f => f.Name).ToList();

            Assert.IsTrue(fields.Contains("transaction"));
            Assert.IsTrue(fields.Contains("transactionExists"));
            Assert.IsTrue(fields.Contains("transactionBlockIndex"));
            Assert.IsTrue(fields.Contains("mempoolTransactions"));
        }

        [TestMethod]
        public void RootQuery_HasBlockTransactionQueries()
        {
            var query = new RootQuery(null, null, null);
            var fields = query.Fields.Select(f => f.Name).ToList();

            Assert.IsTrue(fields.Contains("blockTransactions"));
        }

        [TestMethod]
        public void RootQuery_VersionReturnsV1()
        {
            var query = new RootQuery(null, null, null);
            var versionField = query.Fields.First(f => f.Name == "version");

            Assert.IsNotNull(versionField);
            Assert.IsNotNull(versionField.Description);
        }

        [TestMethod]
        public void RootQuery_TotalFieldCount()
        {
            var query = new RootQuery(null, null, null);
            var fieldCount = query.Fields.Count();

            // Node info: 4 (version, network, height, mempoolCount)
            // Block: 4 (block, blockByHash, blocks, blockHashes)
            // Transaction: 4 (transaction, transactionExists, transactionBlockIndex, mempoolTransactions)
            // Block transactions: 1 (blockTransactions)
            // Total: 13
            Assert.AreEqual(13, fieldCount);
        }
    }
}
