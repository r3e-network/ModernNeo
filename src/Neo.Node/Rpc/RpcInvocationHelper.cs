// Copyright (C) 2015-2025 The Neo Project.
//
// RpcInvocationHelper.cs file belongs to the neo project and is free
// software distributed under the MIT software license.

using Neo.Extensions;
using System.Threading.Tasks;
using Neo.Json;
using Neo.Network.P2P.Payloads;
using Neo.RPC;
using Neo.SmartContract;
using Neo.SmartContract.Native;
using Neo.Wallets;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Neo.Node.Rpc;

internal static class RpcInvocationHelper
{
    public static Signer[] ParseSigners(JToken? token)
    {
        if (token is null)
            return Array.Empty<Signer>();

        if (token is not JArray signerArray)
            throw new RpcException(RpcError.InvalidParams.Code, "Invalid signers format.");

        if (signerArray.Count == 0)
            return Array.Empty<Signer>();

        var signers = new List<Signer>(signerArray.Count);
        foreach (var entry in signerArray)
        {
            if (entry is not JObject signerJson)
                throw new RpcException(RpcError.InvalidParams.Code, "Invalid signer entry.");

            signers.Add(Signer.FromJson(signerJson));
        }

        return signers.ToArray();
    }

    public static IReadOnlyList<ContractParameter> ParseParameters(JToken? token)
    {
        if (token is null)
            return Array.Empty<ContractParameter>();

        if (token is not JArray array)
            throw new RpcException(RpcError.InvalidParams.Code, "Invalid parameters format.");

        if (array.Count == 0)
            return Array.Empty<ContractParameter>();

        var parameters = new List<ContractParameter>(array.Count);
        foreach (var entry in array)
        {
            if (entry is not JObject paramJson)
                throw new RpcException(RpcError.InvalidParams.Code, "Invalid parameter entry.");

            try
            {
                parameters.Add(ContractParameter.FromJson(paramJson));
            }
            catch (Exception ex)
            {
                throw new RpcException(RpcError.InvalidParams.Code, "Invalid parameter value.", new JString(ex.Message));
            }
        }

        return parameters;
    }

    public static UInt160 ResolveContractHash(NeoSystem system, JToken token)
    {
        var text = token.AsString();
        if (!string.IsNullOrWhiteSpace(text))
        {
            if (UInt160.TryParse(text, out var parsed) && parsed is not null)
                return parsed;

            if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id))
            {
                var contract = NativeContract.ContractManagement.GetContractById(system.StoreView, id);
                if (contract is null)
                    throw new RpcException(RpcError.InvalidParams.Code, "Unknown contract id.");
                return contract.Hash;
            }

            try
            {
                return text.ToScriptHash(system.Settings.AddressVersion);
            }
            catch (Exception ex)
            {
                throw new RpcException(RpcError.InvalidParams.Code, "Invalid contract hash.", new JString(ex.Message));
            }
        }

        throw new RpcException(RpcError.InvalidParams.Code, "Invalid contract hash or id.");
    }

    public static CallFlags ParseCallFlags(JToken? token)
    {
        if (token is null)
            return CallFlags.All;

        var text = token.AsString();
        if (!string.IsNullOrWhiteSpace(text) &&
            Enum.TryParse<CallFlags>(text, ignoreCase: true, out var flags))
            return flags;

        var number = token.AsNumber();
        if (!double.IsNaN(number))
        {
            if (number < 0 || number > byte.MaxValue)
                throw new RpcException(RpcError.InvalidParams.Code, "Invalid call flags.");

            return (CallFlags)(byte)number;
        }

        throw new RpcException(RpcError.InvalidParams.Code, "Invalid call flags.");
    }

    public static Transaction CreateInvocationTransaction(NeoSystem system, ReadOnlyMemory<byte> script, IReadOnlyList<Signer> signers)
    {
        var effectiveSigners = signers.Count == 0
            ? new[] { new Signer { Account = UInt160.Zero, Scopes = WitnessScope.None } }
            : signers.ToArray();

        var currentIndex = NativeContract.Ledger.CurrentIndex(system.StoreView);
        var maxIncrement = system.GetMaxValidUntilBlockIncrement();
        var validUntilBlock = currentIndex + maxIncrement;
        if (validUntilBlock < currentIndex)
            validUntilBlock = uint.MaxValue;

        return new Transaction
        {
            Version = 0,
            Nonce = 0,
            SystemFee = 0,
            NetworkFee = 0,
            ValidUntilBlock = validUntilBlock,
            Signers = effectiveSigners,
            Attributes = Array.Empty<TransactionAttribute>(),
            Script = script,
            Witnesses = effectiveSigners.Select(_ => Witness.Empty).ToArray()
        };
    }
}
