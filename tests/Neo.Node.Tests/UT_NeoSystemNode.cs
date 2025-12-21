// Copyright (C) 2015-2025 The Neo Project.
//
// UT_NeoSystemNode.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Microsoft.Extensions.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.Network.P2P;
using Neo.Persistence;
using Neo.Persistence.Providers;
using System;
using System.Collections.Generic;
using System.Net;

namespace Neo.Node.Tests
{
    [TestClass]
    public class UT_NeoSystemNode
    {
        [TestMethod]
        public void TestNeoSystemNodeProperties()
        {
            // Verify NeoSystemNode class exists and has expected properties
            var nodeType = typeof(NeoSystemNode);

            Assert.IsNotNull(nodeType);
            Assert.IsNotNull(nodeType.GetProperty("System"));
            Assert.IsNotNull(nodeType.GetProperty("ChannelsConfig"));
            Assert.IsNotNull(nodeType.GetProperty("IsStarted"));
        }

        [TestMethod]
        public void TestNeoSystemNodeImplementsIDisposable()
        {
            var nodeType = typeof(NeoSystemNode);
            Assert.IsTrue(typeof(System.IDisposable).IsAssignableFrom(nodeType));
        }

        [TestMethod]
        public void TestNeoSystemNodeFactoryExists()
        {
            var factoryType = typeof(NeoSystemNodeFactory);
            Assert.IsNotNull(factoryType);

            var createMethod = factoryType.GetMethod("Create");
            Assert.IsNotNull(createMethod);
            Assert.IsTrue(createMethod.IsStatic);
        }

        [TestMethod]
        public void TestChannelsConfigFactoryExists()
        {
            var factoryType = typeof(ChannelsConfigFactory);
            Assert.IsNotNull(factoryType);

            var createMethod = factoryType.GetMethod("Create");
            Assert.IsNotNull(createMethod);
            Assert.IsTrue(createMethod.IsStatic);
        }

        [TestMethod]
        public void TestNeoSystemNodeHasStartMethod()
        {
            var nodeType = typeof(NeoSystemNode);
            var startMethod = nodeType.GetMethod("Start");

            Assert.IsNotNull(startMethod);
            Assert.IsTrue(startMethod.IsPublic);
            Assert.AreEqual(typeof(void), startMethod.ReturnType);
        }

        [TestMethod]
        public void TestNeoSystemNodeHasDisposeMethod()
        {
            var nodeType = typeof(NeoSystemNode);
            var disposeMethod = nodeType.GetMethod("Dispose");

            Assert.IsNotNull(disposeMethod);
            Assert.IsTrue(disposeMethod.IsPublic);
        }

        [TestMethod]
        public void TestNeoSystemNodeConstructorSignature()
        {
            var nodeType = typeof(NeoSystemNode);
            var constructor = nodeType.GetConstructor(new[] { typeof(NeoSystem), typeof(ChannelsConfig) });

            Assert.IsNotNull(constructor);
            Assert.IsTrue(constructor.IsPublic);
        }
    }

    [TestClass]
    public class UT_NeoSystemNodeFactory
    {
        [TestMethod]
        public void TestFactoryCreateMethodSignature()
        {
            var factoryType = typeof(NeoSystemNodeFactory);
            var createMethod = factoryType.GetMethod("Create");

            Assert.IsNotNull(createMethod);
            Assert.IsTrue(createMethod.IsStatic);
            Assert.IsTrue(createMethod.IsPublic);
            Assert.AreEqual(typeof(NeoSystemNode), createMethod.ReturnType);

            var parameters = createMethod.GetParameters();
            Assert.AreEqual(1, parameters.Length);
            Assert.AreEqual(typeof(IConfiguration), parameters[0].ParameterType);
        }

        [TestMethod]
        public void TestFactoryClassIsStatic()
        {
            var factoryType = typeof(NeoSystemNodeFactory);
            Assert.IsTrue(factoryType.IsAbstract && factoryType.IsSealed);
        }

        [TestMethod]
        public void TestFactoryHasNoPublicConstructor()
        {
            var factoryType = typeof(NeoSystemNodeFactory);
            var constructors = factoryType.GetConstructors();
            Assert.AreEqual(0, constructors.Length);
        }
    }
}
