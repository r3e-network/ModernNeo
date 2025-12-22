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
using System;
using System.Threading.Tasks;
using GrpcByteString = Google.Protobuf.ByteString;

namespace Neo.Grpc.Services
{
    /// <summary>
    /// Implementation of the NeoBlockchainService gRPC service.
    /// Uses IGrpcBlockchainProvider for data access abstraction.
    /// </summary>
    public class NeoBlockchainServiceImpl : NeoBlockchainService.NeoBlockchainServiceBase
    {
        private readonly IGrpcBlockchainProvider _provider;

        /// <summary>
        /// Initializes a new instance of the NeoBlockchainServiceImpl class.
        /// </summary>
        /// <param name="provider">The blockchain data provider.</param>
        public NeoBlockchainServiceImpl(IGrpcBlockchainProvider provider)
        {
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
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

                UInt256? nextBlockHash = null;
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

                UInt256? nextBlockHash = null;
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
            throw new RpcException(new Status(StatusCode.Unimplemented, "ApplicationLogs plugin is required for this operation"));
        }

        /// <summary>
        /// Subscribes to new blocks.
        /// </summary>
        public override Task SubscribeBlocks(SubscribeBlocksRequest request, IServerStreamWriter<V1.Block> responseStream, ServerCallContext context)
        {
            throw new RpcException(new Status(StatusCode.Unimplemented, "Block subscription not yet implemented"));
        }

        /// <summary>
        /// Subscribes to new transactions.
        /// </summary>
        public override Task SubscribeTransactions(SubscribeTransactionsRequest request, IServerStreamWriter<V1.Transaction> responseStream, ServerCallContext context)
        {
            throw new RpcException(new Status(StatusCode.Unimplemented, "Transaction subscription not yet implemented"));
        }
    }
}
