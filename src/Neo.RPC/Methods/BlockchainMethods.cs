// Copyright (C) 2015-2025 The Neo Project.
//
// BlockchainMethods.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Json;

namespace Neo.RPC.Methods
{
    /// <summary>
    /// Standard Neo blockchain RPC methods.
    /// </summary>
    public static class BlockchainMethods
    {
        /// <summary>
        /// Creates the getblockcount method.
        /// </summary>
        public static IRpcMethod GetBlockCount(Func<uint> getBlockCount)
        {
            return new DelegateRpcMethod("getblockcount", _ =>
            {
                return Task.FromResult<JToken?>(getBlockCount());
            });
        }

        /// <summary>
        /// Creates the getbestblockhash method.
        /// </summary>
        public static IRpcMethod GetBestBlockHash(Func<string> getBestBlockHash)
        {
            return new DelegateRpcMethod("getbestblockhash", _ =>
            {
                return Task.FromResult<JToken?>(getBestBlockHash());
            });
        }

        /// <summary>
        /// Creates the getblock method.
        /// </summary>
        public static IRpcMethod GetBlock(Func<string, bool, JToken?> getBlock)
        {
            return new DelegateRpcMethod("getblock", parameters =>
            {
                if (parameters == null || parameters.Count < 1)
                    throw new RpcException(-32602, "Invalid params: block hash or index required");

                var hashOrIndex = parameters[0]?.AsString() ??
                                  parameters[0]?.GetInt32().ToString() ?? "";
                var verbose = parameters.Count > 1 &&
                              parameters[1]?.GetInt32() == 1;

                var result = getBlock(hashOrIndex, verbose);
                if (result == null)
                    throw new RpcException(-100, "Unknown block");

                return Task.FromResult<JToken?>(result);
            });
        }

        /// <summary>
        /// Creates the getblockhash method.
        /// </summary>
        public static IRpcMethod GetBlockHash(Func<uint, string?> getBlockHash)
        {
            return new DelegateRpcMethod("getblockhash", parameters =>
            {
                if (parameters == null || parameters.Count < 1)
                    throw new RpcException(-32602, "Invalid params: block index required");

                var index = (uint)(parameters[0]?.GetInt32() ?? 0);
                var hash = getBlockHash(index);

                if (hash == null)
                    throw new RpcException(-100, "Unknown block");

                return Task.FromResult<JToken?>(hash);
            });
        }

        /// <summary>
        /// Creates the getblockheader method.
        /// </summary>
        public static IRpcMethod GetBlockHeader(Func<string, bool, JToken?> getBlockHeader)
        {
            return new DelegateRpcMethod("getblockheader", parameters =>
            {
                if (parameters == null || parameters.Count < 1)
                    throw new RpcException(-32602, "Invalid params: block hash or index required");

                var hashOrIndex = parameters[0]?.AsString() ??
                                  parameters[0]?.GetInt32().ToString() ?? "";
                var verbose = parameters.Count > 1 &&
                              parameters[1]?.GetInt32() == 1;

                var result = getBlockHeader(hashOrIndex, verbose);
                if (result == null)
                    throw new RpcException(-100, "Unknown block");

                return Task.FromResult<JToken?>(result);
            });
        }

        /// <summary>
        /// Creates the getconnectioncount method.
        /// </summary>
        public static IRpcMethod GetConnectionCount(Func<int> getConnectionCount)
        {
            return new DelegateRpcMethod("getconnectioncount", _ =>
            {
                return Task.FromResult<JToken?>(getConnectionCount());
            });
        }

        /// <summary>
        /// Creates the getpeers method.
        /// </summary>
        public static IRpcMethod GetPeers(Func<JToken> getPeers)
        {
            return new DelegateRpcMethod("getpeers", _ =>
            {
                return Task.FromResult<JToken?>(getPeers());
            });
        }

        /// <summary>
        /// Creates the getversion method.
        /// </summary>
        public static IRpcMethod GetVersion(string userAgent, uint network, uint protocol)
        {
            return new DelegateRpcMethod("getversion", _ =>
            {
                var protocolObj = new JObject
                {
                    ["network"] = network,
                    ["validatorscount"] = 7,
                    ["msperblock"] = 15000,
                    ["maxtraceableblocks"] = 2102400,
                    ["maxvaliduntilblockincrement"] = 86400000 / 15000,
                    ["maxtransactionsperblock"] = 512,
                    ["memorypoolmaxtransactions"] = 50000
                };

                var result = new JObject
                {
                    ["tcpport"] = 10333,
                    ["wsport"] = 10334,
                    ["nonce"] = Random.Shared.NextInt64(),
                    ["useragent"] = userAgent,
                    ["protocol"] = protocolObj
                };

                return Task.FromResult<JToken?>(result);
            });
        }

        /// <summary>
        /// Creates the getrawtransaction method.
        /// </summary>
        public static IRpcMethod GetRawTransaction(Func<string, bool, JToken?> getRawTransaction)
        {
            return new DelegateRpcMethod("getrawtransaction", parameters =>
            {
                if (parameters == null || parameters.Count < 1)
                    throw new RpcException(-32602, "Invalid params: transaction hash required");

                var hash = parameters[0]?.AsString();
                if (string.IsNullOrEmpty(hash))
                    throw new RpcException(-32602, "Invalid params: transaction hash required");

                var verbose = parameters.Count > 1 &&
                              parameters[1]?.GetInt32() == 1;

                var result = getRawTransaction(hash, verbose);
                if (result == null)
                    throw new RpcException(-100, "Unknown transaction");

                return Task.FromResult<JToken?>(result);
            });
        }

        /// <summary>
        /// Creates the sendrawtransaction method.
        /// </summary>
        public static IRpcMethod SendRawTransaction(Func<string, Task<(bool success, string hash, string? error)>> sendRawTransaction)
        {
            return new DelegateRpcMethod("sendrawtransaction", async parameters =>
            {
                if (parameters == null || parameters.Count < 1)
                    throw new RpcException(-32602, "Invalid params: transaction hex required");

                var txHex = parameters[0]?.AsString();
                if (string.IsNullOrEmpty(txHex))
                    throw new RpcException(-32602, "Invalid params: transaction hex required");

                var (success, hash, error) = await sendRawTransaction(txHex);

                if (!success)
                    throw new RpcException(-500, error ?? "Transaction validation failed");

                return new JObject { ["hash"] = hash };
            });
        }

        /// <summary>
        /// Creates the getrawmempool method.
        /// </summary>
        public static IRpcMethod GetRawMemPool(Func<bool, JToken> getRawMemPool)
        {
            return new DelegateRpcMethod("getrawmempool", parameters =>
            {
                var shouldGetUnverified = parameters != null &&
                                           parameters.Count > 0 &&
                                           parameters[0]?.GetInt32() == 1;

                return Task.FromResult<JToken?>(getRawMemPool(shouldGetUnverified));
            });
        }

        /// <summary>
        /// Creates the getstorage method.
        /// </summary>
        public static IRpcMethod GetStorage(Func<string, string, string?> getStorage)
        {
            return new DelegateRpcMethod("getstorage", parameters =>
            {
                if (parameters == null || parameters.Count < 2)
                    throw new RpcException(-32602, "Invalid params: script hash and key required");

                var scriptHash = parameters[0]?.AsString();
                var key = parameters[1]?.AsString();

                if (string.IsNullOrEmpty(scriptHash) || string.IsNullOrEmpty(key))
                    throw new RpcException(-32602, "Invalid params");

                var result = getStorage(scriptHash, key);
                return Task.FromResult<JToken?>(result);
            });
        }

        /// <summary>
        /// Creates the getcontractstate method.
        /// </summary>
        public static IRpcMethod GetContractState(Func<string, JToken?> getContractState)
        {
            return new DelegateRpcMethod("getcontractstate", parameters =>
            {
                if (parameters == null || parameters.Count < 1)
                    throw new RpcException(-32602, "Invalid params: script hash required");

                var scriptHash = parameters[0]?.AsString();
                if (string.IsNullOrEmpty(scriptHash))
                    throw new RpcException(-32602, "Invalid params: script hash required");

                var result = getContractState(scriptHash);
                if (result == null)
                    throw new RpcException(-100, "Unknown contract");

                return Task.FromResult<JToken?>(result);
            });
        }
    }

    /// <summary>
    /// Delegate-based RPC method implementation.
    /// </summary>
    public sealed class DelegateRpcMethod : IRpcMethod
    {
        private readonly Func<JArray?, Task<JToken?>> _handler;

        public string Name { get; }
        public string? Description { get; init; }

        public DelegateRpcMethod(string name, Func<JArray?, Task<JToken?>> handler)
        {
            Name = name;
            _handler = handler;
        }

        public Task<JToken?> ProcessAsync(JArray? parameters)
        {
            return _handler(parameters);
        }
    }
}
