// Copyright (C) 2015-2025 The Neo Project.
//
// CompositeTransportService.cs file belongs to the neo project and is free
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
    /// <summary>
    /// Transport that routes messages to the appropriate underlying transport.
    /// WebSocket or QUIC is used when an active connection exists; otherwise TCP is used.
    /// </summary>
    internal sealed class CompositeTransportService : ITransportService
    {
        private readonly TcpTransportService _tcpTransport;
        private readonly QuicTransportService _quicTransport;
        private readonly WsTransportService _wsTransport;

        public CompositeTransportService(
            TcpTransportService tcpTransport,
            QuicTransportService quicTransport,
            WsTransportService wsTransport)
        {
            _tcpTransport = tcpTransport ?? throw new ArgumentNullException(nameof(tcpTransport));
            _quicTransport = quicTransport ?? throw new ArgumentNullException(nameof(quicTransport));
            _wsTransport = wsTransport ?? throw new ArgumentNullException(nameof(wsTransport));
        }

        public async Task<bool> SendAsync(string address, int port, byte[] message, CancellationToken cancellationToken = default)
        {
            if (_wsTransport.IsConnected(address, port))
                return await _wsTransport.SendAsync(address, port, message, cancellationToken);

            if (_quicTransport.IsConnected(address, port))
                return await _quicTransport.SendAsync(address, port, message, cancellationToken);

            return await _tcpTransport.SendAsync(address, port, message, cancellationToken);
        }

        public Task<bool> ConnectAsync(string address, int port, CancellationToken cancellationToken = default)
        {
            return _tcpTransport.ConnectAsync(address, port, cancellationToken);
        }

        public async Task DisconnectAsync(string address, int port, CancellationToken cancellationToken = default)
        {
            if (_wsTransport.IsConnected(address, port))
                await _wsTransport.DisconnectAsync(address, port, cancellationToken);

            if (_quicTransport.IsConnected(address, port))
                await _quicTransport.DisconnectAsync(address, port, cancellationToken);

            await _tcpTransport.DisconnectAsync(address, port, cancellationToken);
        }

        public bool IsConnected(string address, int port)
        {
            return _wsTransport.IsConnected(address, port) ||
                _quicTransport.IsConnected(address, port) ||
                _tcpTransport.IsConnected(address, port);
        }
    }
}
