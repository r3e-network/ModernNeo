// Copyright (C) 2015-2025 The Neo Project.
//
// RpcParameterParser.cs file belongs to the neo project and is free
// software distributed under the MIT software license.

using Neo.Extensions;
using System.Threading.Tasks;
using Neo.Json;
using Neo;
using System;
using System.Globalization;

namespace Neo.Node.Rpc;

internal static class RpcParameterParser
{
    public static bool TryGetUInt32(JArray? parameters, int index, out uint value)
    {
        value = 0;
        if (parameters is null || parameters.Count <= index)
            return false;

        var token = parameters[index];
        if (token is null)
            return false;

        var text = token.AsString();
        if (uint.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
            return true;

        var number = token.AsNumber();
        if (double.IsNaN(number) || number < 0 || number > uint.MaxValue)
            return false;

        value = (uint)number;
        return true;
    }

    public static bool TryGetUInt64(JArray? parameters, int index, out ulong value)
    {
        value = 0;
        if (parameters is null || parameters.Count <= index)
            return false;

        var token = parameters[index];
        if (token is null)
            return false;

        var text = token.AsString();
        if (ulong.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
            return true;

        var number = token.AsNumber();
        if (double.IsNaN(number) || number < 0 || number > ulong.MaxValue)
            return false;

        value = (ulong)number;
        return true;
    }

    public static bool TryGetInt32(JArray? parameters, int index, out int value)
    {
        value = 0;
        if (parameters is null || parameters.Count <= index)
            return false;

        var token = parameters[index];
        if (token is null)
            return false;

        var text = token.AsString();
        if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
            return true;

        var number = token.AsNumber();
        if (double.IsNaN(number) || number < int.MinValue || number > int.MaxValue)
            return false;

        value = (int)number;
        return true;
    }

    public static bool TryGetUInt160(JArray? parameters, int index, out UInt160 value)
    {
        value = UInt160.Zero;
        if (parameters is null || parameters.Count <= index)
            return false;

        var token = parameters[index];
        if (token is null)
            return false;

        var text = token.AsString();
        if (string.IsNullOrWhiteSpace(text))
            return false;

        if (!UInt160.TryParse(text, out var parsed) || parsed is null)
            return false;

        value = parsed;
        return true;
    }

    public static bool TryGetUInt256(JArray? parameters, int index, out UInt256 value)
    {
        value = UInt256.Zero;
        if (parameters is null || parameters.Count <= index)
            return false;

        var token = parameters[index];
        if (token is null)
            return false;

        var text = token.AsString();
        if (string.IsNullOrWhiteSpace(text))
            return false;

        if (!UInt256.TryParse(text, out var parsed) || parsed is null)
            return false;

        value = parsed;
        return true;
    }

    public static bool TryGetBoolean(JArray? parameters, int index, out bool value)
    {
        value = false;
        if (parameters is null || parameters.Count <= index)
            return false;

        var token = parameters[index];
        if (token is null)
            return false;

        if (token is JBoolean)
        {
            value = token.AsBoolean();
            return true;
        }

        var text = token.AsString();
        if (bool.TryParse(text, out value))
            return true;

        if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number))
        {
            value = number != 0;
            return true;
        }

        var numericValue = token.AsNumber();
        if (double.IsNaN(numericValue))
            return false;

        value = numericValue != 0;
        return true;
    }

    public static bool TryGetHexBytes(JArray? parameters, int index, out byte[] value)
    {
        value = Array.Empty<byte>();
        if (parameters is null || parameters.Count <= index)
            return false;

        var token = parameters[index];
        if (token is null)
            return false;

        var text = token.AsString();
        if (string.IsNullOrWhiteSpace(text))
            return false;

        try
        {
            value = text.AsSpan().TrimStartIgnoreCase("0x").HexToBytes();
            return true;
        }
        catch
        {
            return false;
        }
    }
}
