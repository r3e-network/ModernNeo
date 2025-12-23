// Copyright (C) 2015-2025 The Neo Project.
//
// WalletRpcHelper.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo;
using Neo.Core;
using Neo.Json;
using Neo.Persistence;
using Neo.RPC;
using Neo.Wallets;
using System;
using System.Globalization;
using System.Numerics;
using System.Threading.Tasks;

namespace Neo.Node.Rpc
{
    internal static class WalletRpcHelper
    {
        public static Wallet GetWalletOrThrow(WalletManager manager)
        {
            var wallet = manager.GetWallet();
            if (wallet is null)
                throw new RpcException(-400, "Wallet is not open.");
            return wallet;
        }

        public static UInt160 ParseScriptHash(JToken token, ProtocolSettings settings, string paramName)
        {
            if (token is null)
                throw new RpcException(RpcError.InvalidParams.Code, $"Missing {paramName}.");

            var text = token.AsString();
            if (string.IsNullOrWhiteSpace(text))
                throw new RpcException(RpcError.InvalidParams.Code, $"Invalid {paramName}.");

            if (UInt160.TryParse(text, out var parsed) && parsed is not null)
                return parsed;

            try
            {
                return text.ToScriptHash(settings.AddressVersion);
            }
            catch (Exception ex)
            {
                throw new RpcException(RpcError.InvalidParams.Code, $"Invalid {paramName}.", new JString(ex.Message));
            }
        }

        public static BigDecimal ParseAmount(JToken token, DataCache snapshot, ProtocolSettings settings, UInt160 assetId)
        {
            var text = token.AsString();
            if (string.IsNullOrWhiteSpace(text))
                throw new RpcException(RpcError.InvalidParams.Code, "Invalid amount.");

            var descriptor = new AssetDescriptor(snapshot, settings, assetId);
            try
            {
                return BigDecimal.Parse(text, descriptor.Decimals);
            }
            catch (Exception ex)
            {
                throw new RpcException(RpcError.InvalidParams.Code, "Invalid amount.", new JString(ex.Message));
            }
        }

        public static object? ParseTransferData(JToken? token)
        {
            if (token is null || token is JToken.Null)
                return null;

            if (token is JBoolean)
                return token.AsBoolean();

            if (token is JNumber)
            {
                var text = token.AsString();
                if (BigInteger.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number))
                    return number;
                return text;
            }

            if (token is JString)
                return token.AsString();

            return token.ToString();
        }

        public static JObject WalletAccountToJson(WalletAccount account)
        {
            return new JObject
            {
                ["address"] = account.Address,
                ["haskey"] = account.HasKey,
                ["label"] = account.Label is null ? JToken.Null : new JString(account.Label),
                ["watchonly"] = account.WatchOnly
            };
        }
    }
}
