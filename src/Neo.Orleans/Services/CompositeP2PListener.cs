// Copyright (C) 2015-2025 The Neo Project.
//
// CompositeP2PListener.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Neo.Orleans.Services
{
    internal sealed class CompositeP2PListener : IP2PListener, IAsyncDisposable
    {
        private readonly TcpP2PListener _tcpListener;
        private readonly QuicP2PListener _quicListener;
        private readonly WsP2PListener _wsListener;

        public CompositeP2PListener(TcpP2PListener tcpListener, QuicP2PListener quicListener, WsP2PListener wsListener)
        {
            _tcpListener = tcpListener ?? throw new ArgumentNullException(nameof(tcpListener));
            _quicListener = quicListener ?? throw new ArgumentNullException(nameof(quicListener));
            _wsListener = wsListener ?? throw new ArgumentNullException(nameof(wsListener));
        }

        public async Task StartAsync(int tcpPort, string? bindAddress, CancellationToken cancellationToken = default)
        {
            await _tcpListener.StartAsync(tcpPort, bindAddress, cancellationToken);
            await _quicListener.StartAsync(tcpPort, bindAddress, cancellationToken);
            await _wsListener.StartAsync(tcpPort, bindAddress, cancellationToken);
        }

        public async Task StopAsync(CancellationToken cancellationToken = default)
        {
            await _wsListener.StopAsync(cancellationToken);
            await _quicListener.StopAsync(cancellationToken);
            await _tcpListener.StopAsync(cancellationToken);
        }

        public async ValueTask DisposeAsync()
        {
            await StopAsync();
        }
    }
}
