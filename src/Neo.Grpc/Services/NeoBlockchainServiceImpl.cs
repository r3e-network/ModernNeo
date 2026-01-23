// Copyright (C) 2015-2025 The Neo Project.
//
// NeoBlockchainServiceImpl.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Grpc.Core;
using Neo.Extensions;
using Neo.Grpc.Converters;
using Neo.Grpc.V1;
using Neo.Json;
using Neo.Plugins;
using Neo.Services.Events;
using System;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Threading.Channels;
using System.Threading.Tasks;
using GrpcByteString = Google.Protobuf.ByteString;
using NeoUInt160 = Neo.UInt160;
using NeoUInt256 = Neo.UInt256;

namespace Neo.Grpc.Services
{
    /// <summary>
    /// Implementation of the NeoBlockchainService gRPC service.
    /// Uses IGrpcBlockchainProvider for data access abstraction.
    /// </summary>
    public class NeoBlockchainServiceImpl : NeoBlockchainService.NeoBlockchainServiceBase
    {
        private readonly IGrpcBlockchainProvider _provider;
        private readonly IBlockchainEventService? _eventService;

        /// <summary>
        /// Initializes a new instance of the NeoBlockchainServiceImpl class.
        /// </summary>
        /// <param name="provider">The blockchain data provider.</param>
        /// <param name="eventService">Optional blockchain event service for streaming subscriptions.</param>
        public NeoBlockchainServiceImpl(IGrpcBlockchainProvider provider, IBlockchainEventService? eventService = null)
        {
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
            _eventService = eventService;
        }

        /// <summary>
        /// Gets a block by hash or index.
        /// </summary>
        public override async Task<GetBlockResponse> GetBlock(GetBlockRequest request, ServerCallContext context)
        {
            Network.P2P.Payloads.Block? block = null;

            switch (request.IdentifierCase)
            {
                case GetBlockRequest.IdentifierOneofCase.Hash:
                    var hash = request.Hash.ToNeo();
                    if (hash != null)
                    {
                        block = await _provider.GetBlockAsync(hash);
                    }
                    break;

                case GetBlockRequest.IdentifierOneofCase.Index:
                    block = await _provider.GetBlockByIndexAsync(request.Index);
                    break;

                default:
                    throw new RpcException(new Status(StatusCode.InvalidArgument, "Block identifier (hash or index) is required"));
            }

            if (block == null)
            {
                throw new RpcException(new Status(StatusCode.NotFound, "Block not found"));
            }

            var response = new GetBlockResponse();

            if (request.Verbose)
            {
                var currentCount = await _provider.GetBlockCountAsync();
                var confirmations = currentCount - block.Index;

                NeoUInt256? nextBlockHash = null;
                if (block.Index < currentCount - 1)
                {
                    nextBlockHash = await _provider.GetBlockHashAsync(block.Index + 1);
                }

                response.Block = block.ToGrpc(confirmations, nextBlockHash);
            }
            else
            {
                response.Raw = GrpcByteString.CopyFrom(block.ToArray());
            }

            return response;
        }

        /// <summary>
        /// Gets a block by index.
        /// </summary>
        public override async Task<GetBlockResponse> GetBlockByIndex(GetBlockByIndexRequest request, ServerCallContext context)
        {
            var block = await _provider.GetBlockByIndexAsync(request.Index);

            if (block == null)
            {
                throw new RpcException(new Status(StatusCode.NotFound, "Block not found"));
            }

            var response = new GetBlockResponse();

            if (request.Verbose)
            {
                var currentCount = await _provider.GetBlockCountAsync();
                var confirmations = currentCount - block.Index;

                NeoUInt256? nextBlockHash = null;
                if (block.Index < currentCount - 1)
                {
                    nextBlockHash = await _provider.GetBlockHashAsync(block.Index + 1);
                }

                response.Block = block.ToGrpc(confirmations, nextBlockHash);
            }
            else
            {
                response.Raw = GrpcByteString.CopyFrom(block.ToArray());
            }

            return response;
        }

        /// <summary>
        /// Gets the hash of a block by its index.
        /// </summary>
        public override async Task<GetBlockHashResponse> GetBlockHash(GetBlockHashRequest request, ServerCallContext context)
        {
            var hash = await _provider.GetBlockHashAsync(request.Index);

            if (hash == null)
            {
                throw new RpcException(new Status(StatusCode.NotFound, "Block hash not found"));
            }

            return new GetBlockHashResponse
            {
                Hash = hash.ToGrpc()
            };
        }

        /// <summary>
        /// Gets a block header by hash or index.
        /// </summary>
        public override async Task<GetBlockHeaderResponse> GetBlockHeader(GetBlockHeaderRequest request, ServerCallContext context)
        {
            Network.P2P.Payloads.Header? header = null;

            switch (request.IdentifierCase)
            {
                case GetBlockHeaderRequest.IdentifierOneofCase.Hash:
                    var hash = request.Hash.ToNeo();
                    if (hash != null)
                    {
                        header = await _provider.GetHeaderAsync(hash);
                    }
                    break;

                case GetBlockHeaderRequest.IdentifierOneofCase.Index:
                    header = await _provider.GetHeaderByIndexAsync(request.Index);
                    break;

                default:
                    throw new RpcException(new Status(StatusCode.InvalidArgument, "Header identifier (hash or index) is required"));
            }

            if (header == null)
            {
                throw new RpcException(new Status(StatusCode.NotFound, "Header not found"));
            }

            var response = new GetBlockHeaderResponse();

            if (request.Verbose)
            {
                response.Header = header.ToGrpc();
            }
            else
            {
                response.Raw = GrpcByteString.CopyFrom(header.ToArray());
            }

            return response;
        }

        /// <summary>
        /// Gets the current block count (height + 1).
        /// </summary>
        public override async Task<GetBlockCountResponse> GetBlockCount(GetBlockCountRequest request, ServerCallContext context)
        {
            var count = await _provider.GetBlockCountAsync();

            return new GetBlockCountResponse
            {
                Count = count
            };
        }

        /// <summary>
        /// Gets the hash of the best (latest) block.
        /// </summary>
        public override async Task<GetBestBlockHashResponse> GetBestBlockHash(GetBestBlockHashRequest request, ServerCallContext context)
        {
            var count = await _provider.GetBlockCountAsync();
            var hash = await _provider.GetBlockHashAsync(count - 1);

            if (hash == null)
            {
                throw new RpcException(new Status(StatusCode.Internal, "Failed to get best block hash"));
            }

            return new GetBestBlockHashResponse
            {
                Hash = hash.ToGrpc()
            };
        }

        /// <summary>
        /// Gets a transaction by hash.
        /// </summary>
        public override async Task<GetTransactionResponse> GetTransaction(GetTransactionRequest request, ServerCallContext context)
        {
            var hash = request.Hash.ToNeo();
            if (hash == null)
            {
                throw new RpcException(new Status(StatusCode.InvalidArgument, "Transaction hash is required"));
            }

            var result = await _provider.GetTransactionAsync(hash);

            if (result == null || result.Value.tx == null)
            {
                throw new RpcException(new Status(StatusCode.NotFound, "Transaction not found"));
            }

            var (tx, blockIndex, vmState) = result.Value;

            var response = new GetTransactionResponse();

            if (request.Verbose)
            {
                var currentCount = await _provider.GetBlockCountAsync();
                response.Transaction = tx!.ToGrpc(blockIndex);
                response.BlockIndex = blockIndex;
                response.Confirmations = currentCount - blockIndex;
                response.VmState = vmState.ToGrpc();
            }
            else
            {
                response.Raw = GrpcByteString.CopyFrom(tx!.ToArray());
            }

            return response;
        }

        /// <summary>
        /// Gets the block index containing a transaction.
        /// </summary>
        public override async Task<GetTransactionHeightResponse> GetTransactionHeight(GetTransactionHeightRequest request, ServerCallContext context)
        {
            var hash = request.Hash.ToNeo();
            if (hash == null)
            {
                throw new RpcException(new Status(StatusCode.InvalidArgument, "Transaction hash is required"));
            }

            var result = await _provider.GetTransactionAsync(hash);

            if (result == null)
            {
                throw new RpcException(new Status(StatusCode.NotFound, "Transaction not found"));
            }

            return new GetTransactionHeightResponse
            {
                Height = result.Value.blockIndex
            };
        }

        /// <summary>
        /// Gets memory pool statistics.
        /// </summary>
        public override async Task<GetMemPoolInfoResponse> GetMemPoolInfo(GetMemPoolInfoRequest request, ServerCallContext context)
        {
            var (verified, unverified) = await _provider.GetMemPoolInfoAsync();

            return new GetMemPoolInfoResponse
            {
                Info = new MemoryPoolInfo
                {
                    VerifiedCount = verified,
                    UnverifiedCount = unverified
                }
            };
        }

        /// <summary>
        /// Gets the contents of the memory pool.
        /// </summary>
        public override async Task<GetRawMemPoolResponse> GetRawMemPool(GetRawMemPoolRequest request, ServerCallContext context)
        {
            var contents = new MemoryPoolContents();

            var transactions = await _provider.GetMemPoolTransactionsAsync();
            foreach (var tx in transactions)
            {
                contents.Verified.Add(tx.Hash.ToGrpc());
            }

            return new GetRawMemPoolResponse
            {
                Contents = contents
            };
        }

        /// <summary>
        /// Gets the application log for a transaction.
        /// Note: This requires the ApplicationLogs plugin to be enabled.
        /// </summary>
        public override Task<GetApplicationLogResponse> GetApplicationLog(GetApplicationLogRequest request, ServerCallContext context)
        {
            var hash = TryGetUInt256(request.Hash);
            if (hash == null)
                throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid transaction or block hash"));

            var provider = Plugin.Plugins.OfType<IApplicationLogProvider>().FirstOrDefault();
            if (provider == null)
                throw new RpcException(new Status(StatusCode.FailedPrecondition, "ApplicationLogs plugin is not loaded"));

            var payload = provider.GetApplicationLog(hash, trigger: null);
            if (string.IsNullOrWhiteSpace(payload))
                throw new RpcException(new Status(StatusCode.NotFound, "Application log not found"));

            try
            {
                var log = ParseApplicationLog(hash, payload);
                return Task.FromResult(new GetApplicationLogResponse { Log = log });
            }
            catch (RpcException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new RpcException(new Status(StatusCode.Internal, $"Invalid application log payload: {ex.Message}"));
            }
        }

        /// <summary>
        /// Subscribes to new blocks.
        /// </summary>
        public override Task SubscribeBlocks(SubscribeBlocksRequest request, IServerStreamWriter<V1.Block> responseStream, ServerCallContext context)
        {
            if (_eventService == null)
                throw new RpcException(new Status(StatusCode.FailedPrecondition, "Blockchain event service is not configured"));

            return StreamBlocksAsync(request, responseStream, context);
        }

        /// <summary>
        /// Subscribes to new transactions.
        /// </summary>
        public override Task SubscribeTransactions(SubscribeTransactionsRequest request, IServerStreamWriter<V1.Transaction> responseStream, ServerCallContext context)
        {
            if (_eventService == null)
                throw new RpcException(new Status(StatusCode.FailedPrecondition, "Blockchain event service is not configured"));

            return StreamTransactionsAsync(request, responseStream, context);
        }

        private async Task StreamBlocksAsync(SubscribeBlocksRequest request, IServerStreamWriter<V1.Block> responseStream, ServerCallContext context)
        {
            var channel = Channel.CreateUnbounded<Network.P2P.Payloads.Block>();
            using var subscription = _eventService!.BlockCommitted.Subscribe(new ChannelObserver<Network.P2P.Payloads.Block>(channel.Writer));

            try
            {
                await foreach (var block in channel.Reader.ReadAllAsync(context.CancellationToken))
                {
                    if (block.Index < request.StartHeight)
                        continue;

                    await responseStream.WriteAsync(block.ToGrpc());
                }
            }
            catch (OperationCanceledException)
            {
            }
        }

        private async Task StreamTransactionsAsync(SubscribeTransactionsRequest request, IServerStreamWriter<V1.Transaction> responseStream, ServerCallContext context)
        {
            var channel = Channel.CreateUnbounded<Network.P2P.Payloads.Transaction>();
            using var subscription = _eventService!.TransactionAdded.Subscribe(new ChannelObserver<Network.P2P.Payloads.Transaction>(channel.Writer));

            NeoUInt160? senderFilter = null;
            var senderBytes = request.SenderFilter.Data;
            if (senderBytes != null && senderBytes.Length == NeoUInt160.Length)
            {
                senderFilter = new NeoUInt160(senderBytes.ToByteArray());
            }

            try
            {
                await foreach (var tx in channel.Reader.ReadAllAsync(context.CancellationToken))
                {
                    if (senderFilter != null && !tx.Sender.Equals(senderFilter))
                        continue;

                    await responseStream.WriteAsync(tx.ToGrpc());
                }
            }
            catch (OperationCanceledException)
            {
            }
        }

        private static NeoUInt256? TryGetUInt256(V1.UInt256? value)
        {
            if (value == null || value.Data.Length != NeoUInt256.Length)
                return null;

            return new NeoUInt256(value.Data.ToByteArray());
        }

        private static ApplicationLog ParseApplicationLog(NeoUInt256 hash, string payload)
        {
            var token = JToken.Parse(payload) as JObject
                ?? throw new RpcException(new Status(StatusCode.Internal, "Application log payload is invalid"));

            var log = new ApplicationLog
            {
                TxHash = hash.ToGrpc()
            };

            if (token["executions"] is not JArray executions)
                return log;

            foreach (var executionToken in executions)
            {
                if (executionToken is not JObject executionObject)
                    continue;

                var execution = new Execution
                {
                    Trigger = executionObject["trigger"]?.AsString() ?? string.Empty,
                    VmState = ParseVmState(executionObject["vmstate"]?.AsString()),
                    GasConsumed = ParseGasConsumed(executionObject["gasconsumed"]?.AsString()),
                    Exception = executionObject["exception"]?.AsString() ?? string.Empty
                };

                if (executionObject["stack"] is JArray stackItems)
                {
                    foreach (var item in stackItems)
                    {
                        execution.Stack.Add(ParseStackItem(item));
                    }
                }

                if (executionObject["notifications"] is JArray notifications)
                {
                    foreach (var notificationToken in notifications)
                    {
                        var notification = ParseNotification(notificationToken);
                        if (notification != null)
                            execution.Notifications.Add(notification);
                    }
                }

                log.Executions.Add(execution);
            }

            return log;
        }

        private static VMState ParseVmState(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return VMState.None;

            if (Enum.TryParse<Neo.VM.VMState>(value, true, out var state))
                return state.ToGrpc();

            return VMState.None;
        }

        private static long ParseGasConsumed(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return 0;

            if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var gas))
                return gas;

            return 0;
        }

        private static Notification? ParseNotification(JToken? token)
        {
            if (token is not JObject obj)
                return null;

            var contractText = obj["contract"]?.AsString();
            if (string.IsNullOrWhiteSpace(contractText))
                return null;

            NeoUInt160 contractHash;
            try
            {
                contractHash = NeoUInt160.Parse(contractText);
            }
            catch
            {
                return null;
            }

            var notification = new Notification
            {
                Contract = contractHash.ToGrpc(),
                EventName = obj["eventname"]?.AsString() ?? string.Empty,
                State = ParseStackItem(obj["state"])
            };

            return notification;
        }

        private static V1.StackItem ParseStackItem(JToken? token)
        {
            if (token is not JObject obj)
                return new V1.StackItem { Type = StackItemType.Any };

            var typeText = obj["type"]?.AsString() ?? string.Empty;
            var valueToken = obj["value"];

            switch (typeText)
            {
                case "Boolean":
                    return new V1.StackItem
                    {
                        Type = StackItemType.Boolean,
                        BooleanValue = valueToken?.AsBoolean() ?? false
                    };
                case "Integer":
                    return new V1.StackItem
                    {
                        Type = StackItemType.Integer,
                        IntegerValue = GrpcByteString.CopyFrom(ParseBigIntegerBytes(valueToken?.AsString()))
                    };
                case "ByteString":
                    return new V1.StackItem
                    {
                        Type = StackItemType.ByteString,
                        ByteStringValue = GrpcByteString.CopyFrom(ParseBase64(valueToken?.AsString()))
                    };
                case "Buffer":
                    return new V1.StackItem
                    {
                        Type = StackItemType.Buffer,
                        BufferValue = GrpcByteString.CopyFrom(ParseBase64(valueToken?.AsString()))
                    };
                case "Array":
                    return new V1.StackItem
                    {
                        Type = StackItemType.Array,
                        ArrayValue = ParseStackItemArray(valueToken as JArray)
                    };
                case "Struct":
                    return new V1.StackItem
                    {
                        Type = StackItemType.Struct,
                        StructValue = ParseStackItemStruct(valueToken as JArray)
                    };
                case "Map":
                    return new V1.StackItem
                    {
                        Type = StackItemType.Map,
                        MapValue = ParseStackItemMap(valueToken as JArray)
                    };
                case "Pointer":
                    return new V1.StackItem
                    {
                        Type = StackItemType.Pointer,
                        PointerValue = GrpcByteString.CopyFrom(ParsePointerBytes(valueToken))
                    };
                case "InteropInterface":
                    return new V1.StackItem
                    {
                        Type = StackItemType.InteropInterface,
                        InteropInterfaceValue = GrpcByteString.Empty
                    };
                default:
                    return new V1.StackItem { Type = StackItemType.Any };
            }
        }

        private static StackItemArray ParseStackItemArray(JArray? array)
        {
            var result = new StackItemArray();
            if (array == null)
                return result;

            foreach (var item in array)
                result.Items.Add(ParseStackItem(item));

            return result;
        }

        private static StackItemStruct ParseStackItemStruct(JArray? array)
        {
            var result = new StackItemStruct();
            if (array == null)
                return result;

            foreach (var item in array)
                result.Items.Add(ParseStackItem(item));

            return result;
        }

        private static StackItemMap ParseStackItemMap(JArray? array)
        {
            var result = new StackItemMap();
            if (array == null)
                return result;

            foreach (var entry in array)
            {
                if (entry is not JObject entryObj)
                    continue;

                var key = ParseStackItem(entryObj["key"]);
                var value = ParseStackItem(entryObj["value"]);
                result.Entries.Add(new StackItemMapEntry { Key = key, Value = value });
            }

            return result;
        }

        private static byte[] ParseBase64(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return Array.Empty<byte>();

            try
            {
                return Convert.FromBase64String(value);
            }
            catch
            {
                return Array.Empty<byte>();
            }
        }

        private static byte[] ParseBigIntegerBytes(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return Array.Empty<byte>();

            try
            {
                var number = BigInteger.Parse(value, CultureInfo.InvariantCulture);
                return number.ToByteArray();
            }
            catch
            {
                return Array.Empty<byte>();
            }
        }

        private static byte[] ParsePointerBytes(JToken? valueToken)
        {
            if (valueToken == null)
                return Array.Empty<byte>();

            var number = valueToken.AsNumber();
            if (double.IsNaN(number))
                return Array.Empty<byte>();

            return BitConverter.GetBytes((long)number);
        }

        private sealed class ChannelObserver<T> : IObserver<T>
        {
            private readonly ChannelWriter<T> _writer;

            public ChannelObserver(ChannelWriter<T> writer)
            {
                _writer = writer;
            }

            public void OnCompleted() => _writer.TryComplete();

            public void OnError(Exception error) => _writer.TryComplete(error);

            public void OnNext(T value) => _writer.TryWrite(value);
        }
    }
}
