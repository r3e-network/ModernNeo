// Copyright (C) 2015-2025 The Neo Project.
//
// UT_NeoSchema.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Microsoft.Extensions.DependencyInjection;

namespace Neo.GraphQL.Tests
{
    [TestClass]
    public class UT_NeoSchema
    {
        [TestMethod]
        public void NeoSchema_CanBeCreated()
        {
            var services = new ServiceCollection();
            var provider = services.BuildServiceProvider();

            var schema = new NeoSchema(provider);

            Assert.IsNotNull(schema);
            Assert.IsNotNull(schema.Query);
        }

        [TestMethod]
        public void NeoSchema_QueryIsRootQuery()
        {
            var services = new ServiceCollection();
            var provider = services.BuildServiceProvider();

            var schema = new NeoSchema(provider);

            Assert.IsInstanceOfType(schema.Query, typeof(RootQuery));
        }

        [TestMethod]
        public void NeoSchema_QueryHasCorrectName()
        {
            var services = new ServiceCollection();
            var provider = services.BuildServiceProvider();

            var schema = new NeoSchema(provider);

            Assert.AreEqual("Query", schema.Query.Name);
        }
    }
}
