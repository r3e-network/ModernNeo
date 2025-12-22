// Copyright (C) 2015-2025 The Neo Project.
//
// UT_HttpRpcServer.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

#nullable enable

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.Json;
using Neo.RPC;
using Neo.RPC.Methods;

namespace Neo.UnitTests.RPC
{
    [TestClass]
    public class UT_HttpRpcServer
    {
        [TestMethod]
        public void TestConstructor_DefaultOptions()
        {
            using var server = new HttpRpcServer();
            Assert.IsFalse(server.IsRunning);
            Assert.AreEqual("http://localhost:10332/", server.Endpoint);
        }

        [TestMethod]
        public void TestConstructor_CustomOptions()
        {
            var options = new HttpRpcServerOptions
            {
                ListenAddress = "http://localhost:8080/"
            };
            using var server = new HttpRpcServer(options);
            Assert.AreEqual("http://localhost:8080/", server.Endpoint);
        }

        [TestMethod]
        public void TestRegisterMethod()
        {
            using var server = new HttpRpcServer();
            var method = BlockchainMethods.GetBlockCount(() => 100u);

            server.RegisterMethod(method);

            var methods = server.GetRegisteredMethods().ToList();
            Assert.IsTrue(methods.Contains("getblockcount"));
        }

        [TestMethod]
        public void TestRegisterMethod_NullThrows()
        {
            using var server = new HttpRpcServer();
            Assert.ThrowsExactly<ArgumentNullException>(() => server.RegisterMethod(null!));
        }

        [TestMethod]
        public void TestUnregisterMethod()
        {
            using var server = new HttpRpcServer();
            var method = BlockchainMethods.GetBlockCount(() => 100u);
            server.RegisterMethod(method);

            var result = server.UnregisterMethod("getblockcount");

            Assert.IsTrue(result);
            Assert.IsFalse(server.GetRegisteredMethods().Contains("getblockcount"));
        }

        [TestMethod]
        public void TestUnregisterMethod_NonExisting()
        {
            using var server = new HttpRpcServer();
            var result = server.UnregisterMethod("nonexistent");
            Assert.IsFalse(result);
        }

        [TestMethod]
        public void TestDispose()
        {
            var server = new HttpRpcServer();
            server.Dispose();
            server.Dispose(); // Should not throw on double dispose
        }
    }

    [TestClass]
    public class UT_HttpRpcServerOptions
    {
        [TestMethod]
        public void TestDefaultValues()
        {
            var options = new HttpRpcServerOptions();

            Assert.AreEqual("http://localhost:10332/", options.ListenAddress);
            Assert.IsTrue(options.EnableCors);
            Assert.AreEqual("*", options.CorsOrigin);
            Assert.AreEqual(1024 * 1024, options.MaxRequestSize);
            Assert.AreEqual(TimeSpan.FromSeconds(30), options.RequestTimeout);
        }

        [TestMethod]
        public void TestCustomValues()
        {
            var options = new HttpRpcServerOptions
            {
                ListenAddress = "http://0.0.0.0:8080/",
                EnableCors = false,
                CorsOrigin = "https://example.com",
                MaxRequestSize = 2048,
                RequestTimeout = TimeSpan.FromSeconds(60)
            };

            Assert.AreEqual("http://0.0.0.0:8080/", options.ListenAddress);
            Assert.IsFalse(options.EnableCors);
            Assert.AreEqual("https://example.com", options.CorsOrigin);
            Assert.AreEqual(2048, options.MaxRequestSize);
            Assert.AreEqual(TimeSpan.FromSeconds(60), options.RequestTimeout);
        }
    }

    [TestClass]
    public class UT_BlockchainMethods
    {
        [TestMethod]
        public async Task TestGetBlockCount()
        {
            var method = BlockchainMethods.GetBlockCount(() => 12345u);

            var result = await method.ProcessAsync(null);

            Assert.IsNotNull(result);
            Assert.AreEqual(12345, result.GetInt32());
        }

        [TestMethod]
        public async Task TestGetBestBlockHash()
        {
            var method = BlockchainMethods.GetBestBlockHash(() => "0x1234567890abcdef");

            var result = await method.ProcessAsync(null);

            Assert.IsNotNull(result);
            Assert.AreEqual("0x1234567890abcdef", result.AsString());
        }

        [TestMethod]
        public async Task TestGetConnectionCount()
        {
            var method = BlockchainMethods.GetConnectionCount(() => 42);

            var result = await method.ProcessAsync(null);

            Assert.IsNotNull(result);
            Assert.AreEqual(42, result.GetInt32());
        }

        [TestMethod]
        public async Task TestGetVersion()
        {
            var method = BlockchainMethods.GetVersion("/Neo:3.0/", 860833102u, 1);

            var result = await method.ProcessAsync(null);

            Assert.IsNotNull(result);
            var obj = result as JObject;
            Assert.IsNotNull(obj);
            Assert.AreEqual("/Neo:3.0/", obj["useragent"]?.AsString());
        }

        [TestMethod]
        public async Task TestGetBlock_MissingParams()
        {
            var method = BlockchainMethods.GetBlock((_, _) => null);

            await Assert.ThrowsExactlyAsync<RpcException>(async () =>
                await method.ProcessAsync(null));
        }

        [TestMethod]
        public async Task TestGetBlock_NotFound()
        {
            var method = BlockchainMethods.GetBlock((_, _) => null);
            var parameters = new JArray { "0x1234" };

            await Assert.ThrowsExactlyAsync<RpcException>(async () =>
                await method.ProcessAsync(parameters));
        }

        [TestMethod]
        public async Task TestGetBlockHash_MissingParams()
        {
            var method = BlockchainMethods.GetBlockHash(_ => null);

            await Assert.ThrowsExactlyAsync<RpcException>(async () =>
                await method.ProcessAsync(null));
        }

        [TestMethod]
        public async Task TestGetRawTransaction_MissingParams()
        {
            var method = BlockchainMethods.GetRawTransaction((_, _) => null);

            await Assert.ThrowsExactlyAsync<RpcException>(async () =>
                await method.ProcessAsync(null));
        }

        [TestMethod]
        public async Task TestSendRawTransaction_MissingParams()
        {
            var method = BlockchainMethods.SendRawTransaction(_ =>
                Task.FromResult<(bool, string, string?)>((true, "hash", null)));

            await Assert.ThrowsExactlyAsync<RpcException>(async () =>
                await method.ProcessAsync(null));
        }

        [TestMethod]
        public async Task TestGetStorage_MissingParams()
        {
            var method = BlockchainMethods.GetStorage((_, _) => null);

            await Assert.ThrowsExactlyAsync<RpcException>(async () =>
                await method.ProcessAsync(null));
        }

        [TestMethod]
        public async Task TestGetContractState_MissingParams()
        {
            var method = BlockchainMethods.GetContractState(_ => null);

            await Assert.ThrowsExactlyAsync<RpcException>(async () =>
                await method.ProcessAsync(null));
        }
    }

    [TestClass]
    public class UT_DelegateRpcMethod
    {
        [TestMethod]
        public void TestName()
        {
            var method = new DelegateRpcMethod("testmethod", _ => Task.FromResult<JToken?>(null));
            Assert.AreEqual("testmethod", method.Name);
        }

        [TestMethod]
        public void TestDescription()
        {
            var method = new DelegateRpcMethod("testmethod", _ => Task.FromResult<JToken?>(null))
            {
                Description = "Test description"
            };
            Assert.AreEqual("Test description", method.Description);
        }

        [TestMethod]
        public async Task TestProcessAsync()
        {
            var method = new DelegateRpcMethod("testmethod", parameters =>
            {
                var count = parameters?.Count ?? 0;
                return Task.FromResult<JToken?>(count);
            });

            var result = await method.ProcessAsync(new JArray { 1, 2, 3 });

            Assert.AreEqual(3, result?.GetInt32());
        }
    }

    [TestClass]
    public class UT_RpcException
    {
        [TestMethod]
        public void TestConstructor()
        {
            var ex = new RpcException(-32600, "Invalid Request");

            Assert.AreEqual(-32600, ex.Code);
            Assert.AreEqual("Invalid Request", ex.Message);
            Assert.IsNull(ex.Data);
        }

        [TestMethod]
        public void TestConstructorWithData()
        {
            var data = new JObject { ["detail"] = "extra info" };
            var ex = new RpcException(-32600, "Invalid Request", data);

            Assert.AreEqual(-32600, ex.Code);
            Assert.AreEqual("Invalid Request", ex.Message);
            Assert.IsNotNull(ex.Data);
        }
    }
}
