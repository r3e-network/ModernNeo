// Copyright (C) 2015-2025 The Neo Project.
//
// OpenWalletRpcMethod.cs file belongs to the neo project and is free
// software distributed under the MIT software license.

using Neo;
using System.Threading.Tasks;
using Neo.Json;
using Neo.RPC;
using System;
using System.IO;

namespace Neo.Node.Rpc;

public sealed class OpenWalletRpcMethod : IRpcMethod
{
    private readonly WalletManager _walletManager;
    private readonly NeoSystemNode _node;

    public string Name => "openwallet";

    public OpenWalletRpcMethod(WalletManager walletManager, NeoSystemNode node)
    {
        _walletManager = walletManager ?? throw new ArgumentNullException(nameof(walletManager));
        _node = node ?? throw new ArgumentNullException(nameof(node));
    }

    public Task<JToken?> ProcessAsync(JArray? parameters)
    {
        if (parameters is null || parameters.Count < 2 || parameters[0] is null || parameters[1] is null)
            throw new RpcException(RpcError.InvalidParams.Code, "Missing wallet path or password.");

        var path = parameters[0]!.AsString();
        var password = parameters[1]!.AsString();
        if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(password))
            throw new RpcException(RpcError.InvalidParams.Code, "Invalid wallet path or password.");

        var fullPath = ResolvePath(path);
        if (!File.Exists(fullPath))
            throw new RpcException(RpcError.InvalidParams.Code, "Wallet file not found.");

        try
        {
            _walletManager.Open(fullPath, password, _node.System.Settings);
            return Task.FromResult<JToken?>(new JBoolean(true));
        }
        catch (Exception ex)
        {
            throw new RpcException(RpcError.InvalidParams.Code, "Failed to open wallet.", new JString(ex.Message));
        }
    }

    private static string ResolvePath(string path)
    {
        if (Path.IsPathRooted(path))
            return path;

        var resolved = ProtocolSettings.FindFile(path, Environment.CurrentDirectory);
        if (!string.IsNullOrWhiteSpace(resolved))
            return resolved;

        return Path.GetFullPath(path);
    }
}
