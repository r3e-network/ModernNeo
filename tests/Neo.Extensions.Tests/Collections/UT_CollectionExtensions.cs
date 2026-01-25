// Copyright (C) 2015-2025 The Neo Project.
//
// UT_CollectionExtensions.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Neo.Extensions.Tests.Collections
{
    [TestClass]
    public class UT_CollectionExtensions
    {
        [TestMethod]
        public void TestRemoveWhere()
        {
            var dict = new Dictionary<int, string>
            {
                [1] = "a",
                [2] = "b",
                [3] = "c"
            };

            dict.RemoveWhere(p => dict[p] == "b");

            Assert.AreEqual(2, dict.Count);
            Assert.IsFalse(dict.ContainsKey(2));
            Assert.AreEqual("a", dict[1]);
            Assert.AreEqual("c", dict[3]);
        }

        [TestMethod]
        public void TestRemoveWhereHashSet()
        {
            var set = new HashSet<int> { 1, 2, 3, 4, 5 };
            set.RemoveWhere(p => p % 2 == 0);

            Assert.AreEqual(3, set.Count);
            Assert.IsTrue(set.Contains(1));
            Assert.IsTrue(set.Contains(3));
            Assert.IsTrue(set.Contains(5));
        }

        [TestMethod]
        public void TestSample()
        {
            var list = new List<int> { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
            var sampled = list.Sample(5);
            Assert.AreEqual(5, sampled.Count);
            foreach (var item in sampled) Assert.Contains(item, list);

            sampled = list.Sample(10);
            Assert.AreEqual(10, sampled.Count);

            sampled = list.Sample(0);
            Assert.AreEqual(0, sampled.Count);

            sampled = list.Sample(100);
            Assert.AreEqual(10, sampled.Count);
        }
    }
}
