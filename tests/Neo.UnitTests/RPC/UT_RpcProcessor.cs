// Copyright (C) 2015-2025 The Neo Project.
//
// UT_RpcProcessor.cs file belongs to the neo project and is free
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
using System.Threading.Tasks;

namespace Neo.UnitTests.RPC
{
    [TestClass]
    public class UT_RpcProcessor
    {
        [TestMethod]
        [TestCategory("RPC")]
        public async Task ParseError_InvalidJson()
        {
            var p = new RpcProcessor();
            var json = await p.ProcessAsync("not json");
            var parsed = JToken.Parse(json);
            Assert.IsNotNull(parsed);
            Assert.IsInstanceOfType(parsed, typeof(JObject));
            var token = (JObject)parsed!;
            var errorTok = token["error"];
            Assert.IsNotNull(errorTok);
            Assert.IsInstanceOfType(errorTok, typeof(JObject));
            var error = (JObject)errorTok!;
            var codeTok = error["code"];
            Assert.IsNotNull(codeTok);
            Assert.AreEqual(-32700, codeTok.AsNumber());
        }

        [TestMethod]
        [TestCategory("RPC")]
        public async Task MethodNotFound()
        {
            var p = new RpcProcessor();
            var req = new JObject
            {
                ["jsonrpc"] = "2.0",
                ["id"] = 1,
                ["method"] = "unknown",
                ["params"] = new JArray()
            };
            var json = await p.ProcessAsync(req.ToString());
            var parsed = JToken.Parse(json);
            Assert.IsNotNull(parsed);
            Assert.IsInstanceOfType(parsed, typeof(JObject));
            var token = (JObject)parsed!;
            var errorTok = token["error"];
            Assert.IsNotNull(errorTok);
            Assert.IsInstanceOfType(errorTok, typeof(JObject));
            var error = (JObject)errorTok!;
            var codeTok = error["code"];
            Assert.IsNotNull(codeTok);
            Assert.AreEqual(-32601, codeTok.AsNumber());
        }

        private sealed class EchoMethod : IRpcMethod
        {
            public string Name => "echo";
            public Task<JToken?> ProcessAsync(JArray? parameters)
            {
                return Task.FromResult<JToken?>(new JString("ok"));
            }
        }

        [TestMethod]
        [TestCategory("RPC")]
        public async Task Success_Echo()
        {
            var p = new RpcProcessor();
            p.RegisterMethod(new EchoMethod());
            var req = new JObject
            {
                ["jsonrpc"] = "2.0",
                ["id"] = 7,
                ["method"] = "echo",
                ["params"] = new JArray()
            };
            var json = await p.ProcessAsync(req.ToString());
            var parsed = JToken.Parse(json);
            Assert.IsNotNull(parsed);
            Assert.IsInstanceOfType(parsed, typeof(JObject));
            var token = (JObject)parsed!;
            var resultTok = token["result"];
            Assert.IsNotNull(resultTok);
            Assert.AreEqual("ok", resultTok!.AsString());
        }
    }
}
