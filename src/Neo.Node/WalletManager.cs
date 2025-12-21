// Copyright (C) 2015-2025 The Neo Project.
//
// WalletManager.cs file belongs to the neo project and is free
// software distributed under the MIT software license.

using Neo;
using Neo.Wallets;
using System;

namespace Neo.Node;

public sealed class WalletManager : IWalletProvider
{
    private readonly object _syncRoot = new();
    private Wallet? _wallet;

    public event EventHandler<Wallet?>? WalletChanged;

    public Wallet? GetWallet()
    {
        lock (_syncRoot)
        {
            return _wallet;
        }
    }

    public Wallet Open(string path, string password, ProtocolSettings settings)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path, nameof(path));
        ArgumentNullException.ThrowIfNull(settings);

        var wallet = Wallet.Open(path, password, settings)
            ?? throw new InvalidOperationException($"Failed to open wallet at '{path}'.");

        if (!wallet.VerifyPassword(password))
            throw new InvalidOperationException("Invalid wallet password.");

        lock (_syncRoot)
        {
            _wallet = wallet;
        }

        WalletChanged?.Invoke(this, wallet);
        return wallet;
    }

    public void Close()
    {
        lock (_syncRoot)
        {
            _wallet = null;
        }

        WalletChanged?.Invoke(this, null);
    }
}
