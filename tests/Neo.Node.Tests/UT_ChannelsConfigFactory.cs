// Copyright (C) 2015-2025 The Neo Project.
//
// UT_ChannelsConfigFactory.cs file belongs to the neo project and is free
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
using System.Collections.Generic;
using System.Net;

namespace Neo.Node.Tests
{
    [TestClass]
    public class UT_ChannelsConfigFactory
    {
        [TestMethod]
        public void TestCreateWithDefaultValues()
        {
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>())
                .Build();

            var result = ChannelsConfigFactory.Create(config);

            Assert.IsNotNull(result);
            Assert.IsNull(result.Tcp);
            Assert.AreEqual(ChannelsConfig.DefaultEnableCompression, result.EnableCompression);
            Assert.AreEqual(ChannelsConfig.DefaultMinDesiredConnections, result.MinDesiredConnections);
            Assert.AreEqual(ChannelsConfig.DefaultMaxConnections, result.MaxConnections);
        }

        [TestMethod]
        public void TestCreateWithPort()
        {
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ApplicationConfiguration:P2P:Port"] = "10333"
                })
                .Build();

            var result = ChannelsConfigFactory.Create(config);

            Assert.IsNotNull(result);
            Assert.IsNotNull(result.Tcp);
            Assert.AreEqual(10333, result.Tcp.Port);
            Assert.AreEqual(IPAddress.Any, result.Tcp.Address);
        }

        [TestMethod]
        public void TestCreateWithBindAddress()
        {
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ApplicationConfiguration:P2P:Port"] = "10333",
                    ["ApplicationConfiguration:P2P:BindAddress"] = "127.0.0.1"
                })
                .Build();

            var result = ChannelsConfigFactory.Create(config);

            Assert.IsNotNull(result);
            Assert.IsNotNull(result.Tcp);
            Assert.AreEqual(10333, result.Tcp.Port);
            Assert.AreEqual(IPAddress.Loopback, result.Tcp.Address);
        }

        [TestMethod]
        public void TestCreateWithListenAddress()
        {
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ApplicationConfiguration:P2P:Port"] = "10333",
                    ["ApplicationConfiguration:P2P:ListenAddress"] = "192.168.1.1"
                })
                .Build();

            var result = ChannelsConfigFactory.Create(config);

            Assert.IsNotNull(result);
            Assert.IsNotNull(result.Tcp);
            Assert.AreEqual(IPAddress.Parse("192.168.1.1"), result.Tcp.Address);
        }

        [TestMethod]
        public void TestCreateWithCustomConnectionSettings()
        {
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ApplicationConfiguration:P2P:MinDesiredConnections"] = "20",
                    ["ApplicationConfiguration:P2P:MaxConnections"] = "100",
                    ["ApplicationConfiguration:P2P:MaxConnectionsPerAddress"] = "5",
                    ["ApplicationConfiguration:P2P:EnableCompression"] = "false"
                })
                .Build();

            var result = ChannelsConfigFactory.Create(config);

            Assert.IsNotNull(result);
            Assert.AreEqual(20, result.MinDesiredConnections);
            Assert.AreEqual(100, result.MaxConnections);
            Assert.AreEqual(5, result.MaxConnectionsPerAddress);
            Assert.IsFalse(result.EnableCompression);
        }

        [TestMethod]
        public void TestCreateWithInvalidBindAddressFallsBackToAny()
        {
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ApplicationConfiguration:P2P:Port"] = "10333",
                    ["ApplicationConfiguration:P2P:BindAddress"] = "invalid-address"
                })
                .Build();

            var result = ChannelsConfigFactory.Create(config);

            Assert.IsNotNull(result);
            Assert.IsNotNull(result.Tcp);
            Assert.AreEqual(IPAddress.Any, result.Tcp.Address);
        }

        [TestMethod]
        public void TestCreateWithZeroPortReturnsNullTcp()
        {
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ApplicationConfiguration:P2P:Port"] = "0"
                })
                .Build();

            var result = ChannelsConfigFactory.Create(config);

            Assert.IsNotNull(result);
            Assert.IsNull(result.Tcp);
        }
    }
}
