// Copyright (C) 2015-2025 The Neo Project.
//
// QuicDhtRpcService.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Network.P2P.Transport;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;

namespace Neo.Network.Discovery
{
    /// <summary>
    /// QUIC-based implementation of IDhtRpcService for Kademlia DHT operations.
    /// </summary>
    [SupportedOSPlatform("linux")]
    [SupportedOSPlatform("windows")]
    [SupportedOSPlatform("osx")]
    public sealed class QuicDhtRpcService : IDhtRpcService, IAsyncDisposable
    {
        private readonly QuicTransport _transport;
        private readonly TimeSpan _timeout;
        private readonly byte[] _localNodeId;

        // DHT message types
        private const byte MsgPing = 0x01;
        private const byte MsgPong = 0x02;
        private const byte MsgFindNode = 0x03;
        private const byte MsgFindNodeResponse = 0x04;
        private const byte MsgGetProviders = 0x05;
        private const byte MsgGetProvidersResponse = 0x06;
        private const byte MsgAddProvider = 0x07;

        public QuicDhtRpcService(QuicTransport transport, byte[] localNodeId, TimeSpan? timeout = null)
        {
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
            _localNodeId = localNodeId ?? throw new ArgumentNullException(nameof(localNodeId));
            _timeout = timeout ?? TimeSpan.FromSeconds(5);
        }

        public async Task<IEnumerable<PeerInfo>?> FindNodeAsync(PeerInfo peer, byte[] targetId, CancellationToken cancellationToken = default)
        {
            if (peer.Addresses.Count == 0)
                return null;

            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(_timeout);

                var endpoint = peer.Addresses[0];
                var connection = await _transport.ConnectAsync(endpoint, cts.Token);

                // Build FIND_NODE message: [type:1][sender_id:32][target_id:32]
                var message = new byte[1 + 32 + 32];
                message[0] = MsgFindNode;
                _localNodeId.CopyTo(message.AsSpan(1));
                targetId.CopyTo(message.AsSpan(33));

                await connection.SendAsync(message, cts.Token);

                // Wait for response
                var response = await ReceiveResponseAsync(connection, MsgFindNodeResponse, cts.Token);
                if (response == null)
                    return null;

                return ParsePeerList(response);
            }
            catch (OperationCanceledException)
            {
                return null;
            }
            catch
            {
                return null;
            }
        }

        public async Task<IEnumerable<PeerInfo>?> GetProvidersAsync(PeerInfo peer, byte[] key, CancellationToken cancellationToken = default)
        {
            if (peer.Addresses.Count == 0)
                return null;

            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(_timeout);

                var endpoint = peer.Addresses[0];
                var connection = await _transport.ConnectAsync(endpoint, cts.Token);

                // Build GET_PROVIDERS message: [type:1][sender_id:32][key:32]
                var message = new byte[1 + 32 + 32];
                message[0] = MsgGetProviders;
                _localNodeId.CopyTo(message.AsSpan(1));
                key.CopyTo(message.AsSpan(33));

                await connection.SendAsync(message, cts.Token);

                var response = await ReceiveResponseAsync(connection, MsgGetProvidersResponse, cts.Token);
                if (response == null)
                    return null;

                return ParsePeerList(response);
            }
            catch (OperationCanceledException)
            {
                return null;
            }
            catch
            {
                return null;
            }
        }

        public async Task AddProviderAsync(PeerInfo peer, byte[] key, PeerInfo provider, CancellationToken cancellationToken = default)
        {
            if (peer.Addresses.Count == 0)
                return;

            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(_timeout);

                var endpoint = peer.Addresses[0];
                var connection = await _transport.ConnectAsync(endpoint, cts.Token);

                // Build ADD_PROVIDER message: [type:1][sender_id:32][key:32][provider_data]
                var providerData = SerializePeerInfo(provider);
                var message = new byte[1 + 32 + 32 + providerData.Length];
                message[0] = MsgAddProvider;
                _localNodeId.CopyTo(message.AsSpan(1));
                key.CopyTo(message.AsSpan(33));
                providerData.CopyTo(message.AsSpan(65));

                await connection.SendAsync(message, cts.Token);
            }
            catch
            {
                // Fire and forget - no response expected
            }
        }

        public async Task<bool> PingAsync(PeerInfo peer, CancellationToken cancellationToken = default)
        {
            if (peer.Addresses.Count == 0)
                return false;

            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(_timeout);

                var endpoint = peer.Addresses[0];
                var connection = await _transport.ConnectAsync(endpoint, cts.Token);

                // Build PING message: [type:1][sender_id:32]
                var message = new byte[1 + 32];
                message[0] = MsgPing;
                _localNodeId.CopyTo(message.AsSpan(1));

                await connection.SendAsync(message, cts.Token);

                var response = await ReceiveResponseAsync(connection, MsgPong, cts.Token);
                return response != null;
            }
            catch
            {
                return false;
            }
        }

        public ValueTask DisposeAsync()
        {
            return _transport.DisposeAsync();
        }

        private async Task<byte[]?> ReceiveResponseAsync(QuicPeerConnection connection, byte expectedType, CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource<byte[]?>();

            connection.OnMessageReceived += OnMessage;

            try
            {
                await connection.StartReceivingAsync(cancellationToken);
                return await tcs.Task.WaitAsync(cancellationToken);
            }
            finally
            {
                connection.OnMessageReceived -= OnMessage;
            }

            Task OnMessage(ReadOnlyMemory<byte> data)
            {
                if (data.Length > 0 && data.Span[0] == expectedType)
                {
                    tcs.TrySetResult(data.ToArray());
                }
                return Task.CompletedTask;
            }
        }

        private static IEnumerable<PeerInfo> ParsePeerList(byte[] data)
        {
            var peers = new List<PeerInfo>();
            var offset = 1; // Skip message type

            // Format: [count:4][peer1][peer2]...
            if (data.Length < 5)
                return peers;

            var count = BitConverter.ToInt32(data, offset);
            offset += 4;

            for (int i = 0; i < count && offset < data.Length; i++)
            {
                var peer = ParsePeerInfo(data, ref offset);
                if (peer != null)
                    peers.Add(peer);
            }

            return peers;
        }

        private static PeerInfo? ParsePeerInfo(byte[] data, ref int offset)
        {
            // Format: [id_len:1][id:n][addr_count:1][addr1][addr2]...
            if (offset >= data.Length)
                return null;

            var idLen = data[offset++];
            if (offset + idLen > data.Length)
                return null;

            var peerId = new byte[idLen];
            Array.Copy(data, offset, peerId, 0, idLen);
            offset += idLen;

            if (offset >= data.Length)
                return null;

            var addrCount = data[offset++];
            var addresses = new List<IPEndPoint>();

            for (int i = 0; i < addrCount && offset + 6 <= data.Length; i++)
            {
                // Format: [ip:4][port:2]
                var ip = new IPAddress(data.AsSpan(offset, 4));
                offset += 4;
                var port = BitConverter.ToUInt16(data, offset);
                offset += 2;
                addresses.Add(new IPEndPoint(ip, port));
            }

            return new PeerInfo
            {
                PeerId = peerId,
                Addresses = addresses,
                DiscoverySource = "dht"
            };
        }

        private static byte[] SerializePeerInfo(PeerInfo peer)
        {
            // Format: [id_len:1][id:n][addr_count:1][addr1][addr2]...
            var idLen = (byte)Math.Min(peer.PeerId.Length, 255);
            var addrCount = (byte)Math.Min(peer.Addresses.Count, 255);
            var size = 1 + idLen + 1 + (addrCount * 6);

            var buffer = new byte[size];
            var offset = 0;

            buffer[offset++] = idLen;
            Array.Copy(peer.PeerId, 0, buffer, offset, idLen);
            offset += idLen;

            buffer[offset++] = addrCount;
            foreach (var addr in peer.Addresses.Take(addrCount))
            {
                addr.Address.GetAddressBytes().CopyTo(buffer.AsSpan(offset));
                offset += 4;
                BitConverter.GetBytes((ushort)addr.Port).CopyTo(buffer.AsSpan(offset));
                offset += 2;
            }

            return buffer;
        }
    }
}
