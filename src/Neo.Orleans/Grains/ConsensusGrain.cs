// Copyright (C) 2015-2025 The Neo Project.
//
// ConsensusGrain.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo;
using Neo.Core;
using Neo.Cryptography;
using Neo.Extensions;
using Neo.IO;
using Neo.Ledger;
using Neo.Network.P2P;
using Neo.Network.P2P.Payloads;
using Neo.Orleans.Adapters;
using Neo.Orleans.Dbft;
using Neo.Orleans.Dbft.Consensus;
using Neo.Orleans.Dbft.Messages;
using Neo.Orleans.Dbft.Types;
using Neo.Orleans.Hosting;
using Neo.Orleans.Interfaces;
using Neo.Orleans.Options;
using Neo.Orleans.States;
using Neo.Orleans.Utilities;
using Neo.Persistence;
using Neo.Sign;
using Neo.SmartContract;
using Neo.SmartContract.Native;
using Neo.VM;
using Orleans.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;

#pragma warning disable CS0618 // OrleansOptions is obsolete during migration
namespace Neo.Orleans.Grains
{
    /// <summary>
    /// Orleans Grain implementation for dBFT consensus management.
    /// </summary>
    /// <remarks>
    /// This grain implements the dBFT (delegated Byzantine Fault Tolerance) consensus algorithm,
    /// handling prepare requests, prepare responses, change views, commits, and recovery messages.
    /// It manages the consensus state and coordinates with other consensus participants.
    /// </remarks>
    public class ConsensusGrain : Grain, IConsensusGrain
    {
        private const string DbftCategory = "dBFT";

        private readonly IPersistentState<ConsensusGrainState> _state;
        private readonly IGrainFactory _grainFactory;
        private readonly INeoSystem _system;
        private readonly IOrleansOptions _options;
        private readonly DbftSettings _dbftSettings;
        private readonly ITimeProvider _timeProvider;
        private readonly Dictionary<(uint blockIndex, byte viewNumber, UInt256 hash), DateTime> _knownHashes = new();

        private ConsensusContext? _context;
        private ISigner _signer = NullSigner.Instance;
        private IGrainTimer? _timer;
        private DateTime _prepareRequestReceivedTime;
        private uint _prepareRequestReceivedBlockIndex;
        private uint _blockReceivedIndex;
        private DateTime _clockStarted;
        private TimeSpan _expectedDelay = TimeSpan.Zero;
        private uint _timerHeight;
        private byte _timerViewNumber;
        private uint _lastPersistedIndex;
        private bool _isRecovering;
        private bool _started;

        public ConsensusGrain(
            [PersistentState("consensus", "ConsensusStore")]
            IPersistentState<ConsensusGrainState> state,
            IGrainFactory grainFactory,
            INeoSystem system,
            ITimeProvider timeProvider,
            IOrleansOptions? options = null)
        {
            _state = state;
            _grainFactory = grainFactory;
            _system = system;
            _timeProvider = timeProvider;
            _clockStarted = timeProvider.UtcNow;
            _options = options ?? new OrleansOptions();
            _dbftSettings = new DbftSettings(system.Settings);
        }

        public override Task OnActivateAsync(CancellationToken cancellationToken)
        {
            _system.MemPool.NewTransaction += MemPool_NewTransaction;
            Neo.Ledger.Blockchain.Committed += Blockchain_Committed;
            return base.OnActivateAsync(cancellationToken);
        }

        public override Task OnDeactivateAsync(DeactivationReason reason, CancellationToken cancellationToken)
        {
            _timer?.Dispose();
            _timer = null;
            _system.MemPool.NewTransaction -= MemPool_NewTransaction;
            Neo.Ledger.Blockchain.Committed -= Blockchain_Committed;
            _context?.Dispose();
            _context = null;
            _started = false;
            return base.OnDeactivateAsync(reason, cancellationToken);
        }

        public async Task InitializeAsync(int myIndex, int validatorCount)
        {
            _state.State.MyIndex = myIndex;
            _state.State.ValidatorCount = validatorCount;
            await _state.WriteStateAsync();
        }

        /// <summary>
        /// Starts the consensus process.
        /// </summary>
        public async Task StartAsync()
        {
            if (_started)
                return;

            Log("OnStart");
            _signer = ResolveSigner();
            await EnsureLedgerInitializedAsync();
            EnsureContext();
            var context = _context!;

            _started = true;
            _state.State.IsRunning = true;
            _state.State.Phase = ConsensusPhase.Initial;
            _state.State.ViewStartTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            await _state.WriteStateAsync();

            if (!_dbftSettings.IgnoreRecoveryLogs && _options.ValidationMode != NeoValidationMode.None && context.Load())
            {
                if (context.Transactions != null)
                {
                    var blockchain = _grainFactory.GetGrain<IBlockchainGrain>(0);
                    await blockchain.FillMemoryPoolAsync(context.Transactions.Values);
                }

                if (context.CommitSent)
                {
                    await CheckPreparationsAsync();
                    return;
                }
            }

            InitializeConsensus(context.ViewNumber);
            if (!context.WatchOnly)
                await RequestRecoveryAsync();
        }

        public async Task StopAsync()
        {
            if (!_started)
                return;

            Log("OnStop");
            _started = false;
            _timer?.Dispose();
            _timer = null;
            _context?.Dispose();
            _context = null;
            _state.State.IsRunning = false;
            _state.State.Phase = ConsensusPhase.Initial;
            await _state.WriteStateAsync();
        }

        /// <summary>
        /// Handles an incoming consensus message.
        /// </summary>
        /// <param name="message">The raw message bytes.</param>
        /// <param name="senderAddress">The address of the peer that sent the message.</param>
        public async Task OnConsensusMessageAsync(byte[] message, string senderAddress)
        {
            if (!_started || message == null || message.Length == 0 || _context == null)
                return;

            if (!SerializationHelper.TryDeserializeExtensible(message, out var payload))
                return;

            if (!string.Equals(payload.Category, DbftCategory, StringComparison.Ordinal))
                return;

            var blockchain = _grainFactory.GetGrain<IBlockchainGrain>(0);
            var verifyResult = await blockchain.VerifyExtensiblePayloadAsync(payload);
            if (verifyResult != VerifyResult.Succeed)
                return;

            await OnConsensusPayloadAsync(payload);
        }

        /// <summary>
        /// Gets the current consensus state.
        /// </summary>
        /// <returns>The consensus state including view number, block height, and phase.</returns>
        public Task<ConsensusState> GetStateAsync()
        {
            if (!_started || _context == null)
                return Task.FromResult(new ConsensusState(0, 0, ConsensusPhase.Initial, false));

            return Task.FromResult(new ConsensusState(
                _context.ViewNumber,
                _context.Block.Index,
                GetPhase(),
                _context.IsPrimary));
        }

        /// <summary>
        /// Gets the current view number.
        /// </summary>
        /// <returns>The current view number, or 0 if consensus is not started.</returns>
        public Task<byte> GetViewNumberAsync() =>
            Task.FromResult(_context?.ViewNumber ?? (byte)0);

        /// <summary>
        /// Checks if this node is the primary validator for the current view.
        /// </summary>
        /// <returns>True if this node is primary, false otherwise.</returns>
        public Task<bool> IsPrimaryAsync() =>
            Task.FromResult(_context?.IsPrimary ?? false);

        /// <summary>
        /// Handles a new transaction that should be considered for inclusion.
        /// </summary>
        /// <param name="transaction">The transaction to process.</param>
        public Task OnTransactionAsync(Transaction transaction)
        {
            if (!_started || _context == null || transaction == null)
                return Task.CompletedTask;

            return OnTransactionInternalAsync(transaction);
        }

        /// <summary>
        /// Called when a block has been persisted to the blockchain.
        /// </summary>
        /// <param name="block">The persisted block.</param>
        public Task OnPersistCompletedAsync(Block block)
        {
            if (!_started || _context == null || block == null)
                return Task.CompletedTask;

            OnPersistCompleted(block);
            return Task.CompletedTask;
        }

        private void Blockchain_Committed(INeoSystem system, Block block)
        {
            if (!ReferenceEquals(system, _system))
                return;

            var consensus = _grainFactory.GetGrain<IConsensusGrain>(0);
            _ = consensus.OnPersistCompletedAsync(block);
        }

        private void MemPool_NewTransaction(object? sender, NewTransactionEventArgs e)
        {
            e.Cancel = e.Transaction.SystemFee > _dbftSettings.MaxBlockSystemFee;
        }

        private async Task OnConsensusPayloadAsync(ExtensiblePayload payload)
        {
            if (_context == null || _context.BlockSent)
                return;

            ConsensusMessage message;
            try
            {
                message = _context.GetMessage(payload);
            }
            catch (Exception ex)
            {
                Utility.Log(nameof(ConsensusGrain), LogLevel.Debug, ex.ToString());
                return;
            }

            if (!message.Verify(_system.Settings)) return;
            if (message.BlockIndex != _context.Block.Index)
            {
                if (_context.Block.Index < message.BlockIndex)
                {
                    Log($"Chain is behind: expected={message.BlockIndex} current={_context.Block.Index - 1}", LogLevel.Warning);
                }
                return;
            }
            if (message.ValidatorIndex >= _context.Validators.Length) return;
            if (payload.Sender != Contract.CreateSignatureRedeemScript(_context.Validators[message.ValidatorIndex]).ToScriptHash()) return;

            _context.LastSeenMessage[_context.Validators[message.ValidatorIndex]] = message.BlockIndex;
            switch (message)
            {
                case PrepareRequest request:
                    await OnPrepareRequestReceivedAsync(payload, request);
                    break;
                case PrepareResponse response:
                    await OnPrepareResponseReceivedAsync(payload, response);
                    break;
                case ChangeView view:
                    await OnChangeViewReceivedAsync(payload, view);
                    break;
                case Commit commit:
                    await OnCommitReceivedAsync(payload, commit);
                    break;
                case RecoveryRequest request:
                    await OnRecoveryRequestReceivedAsync(payload, request);
                    break;
                case RecoveryMessage recovery:
                    await OnRecoveryMessageReceivedAsync(recovery);
                    break;
            }
        }

        private async Task OnPrepareRequestReceivedAsync(ExtensiblePayload payload, PrepareRequest message)
        {
            if (_context == null)
                return;

            if (_context.RequestSentOrReceived || _context.NotAcceptingPayloadsDueToViewChanging) return;
            if (message.ValidatorIndex != _context.Block.PrimaryIndex || message.ViewNumber != _context.ViewNumber) return;
            if (message.Version != _context.Block.Version || message.PrevHash != _context.Block.PrevHash) return;
            if (message.TransactionHashes.Length > _system.Settings.MaxTransactionsPerBlock) return;
            Log($"{nameof(OnPrepareRequestReceivedAsync)}: height={message.BlockIndex} view={message.ViewNumber} index={message.ValidatorIndex} tx={message.TransactionHashes.Length}");
            if (message.Timestamp <= _context.PrevHeader.Timestamp || message.Timestamp > _timeProvider.UtcNow.AddMilliseconds(8 * _context.TimePerBlock.TotalMilliseconds).ToTimestampMS())
            {
                Log($"Timestamp incorrect: {message.Timestamp}", LogLevel.Warning);
                return;
            }

            // SECURITY NOTE: _timeProvider.UtcNow is used for timestamp validation.
            // In production deployments, ensure all consensus nodes have their system clocks
            // synchronized via NTP (Network Time Protocol) with a reliable time source.
            // Clock drift between nodes can cause valid blocks to be rejected, leading to
            // consensus failures. Recommended: synchronize clocks within 500ms of each other.

            if (message.TransactionHashes.Any(p => NativeContract.Ledger.ContainsTransaction(_context.Snapshot, p)))
            {
                Log("Invalid request: transaction already exists", LogLevel.Warning);
                return;
            }

            ExtendTimerByFactor(2);

            _prepareRequestReceivedTime = _timeProvider.UtcNow;
            _prepareRequestReceivedBlockIndex = message.BlockIndex;

            _context.Block.Header.Timestamp = message.Timestamp;
            _context.Block.Header.Nonce = message.Nonce;
            _context.TransactionHashes = message.TransactionHashes;

            _context.Transactions = new Dictionary<UInt256, Transaction>();
            _context.VerificationContext = new TransactionVerificationContext();
            for (int i = 0; i < _context.PreparationPayloads.Length; i++)
                if (_context.PreparationPayloads[i] != null)
                    if (!_context.GetMessage<PrepareResponse>(_context.PreparationPayloads[i]).PreparationHash.Equals(payload.Hash))
                        _context.PreparationPayloads[i] = null;
            _context.PreparationPayloads[message.ValidatorIndex] = payload;
            byte[] hashData = _context.EnsureHeader().GetSignData(_system.Settings.Network);
            for (int i = 0; i < _context.CommitPayloads.Length; i++)
                if (_context.GetMessage(_context.CommitPayloads[i])?.ViewNumber == _context.ViewNumber)
                    if (!Crypto.VerifySignature(hashData, _context.GetMessage<Commit>(_context.CommitPayloads[i]).Signature.Span, _context.Validators[i]))
                        _context.CommitPayloads[i] = null;

            if (_context.TransactionHashes.Length == 0)
            {
                await CheckPrepareResponseAsync();
                return;
            }

            Dictionary<UInt256, Transaction> mempoolVerified = _system.MemPool.GetVerifiedTransactions().ToDictionary(p => p.Hash);
            var unverified = new List<Transaction>();
            var mtb = _system.GetMaxTraceableBlocks();
            foreach (UInt256 hash in _context.TransactionHashes)
            {
                if (mempoolVerified.TryGetValue(hash, out var tx))
                {
                    if (tx is null)
                        continue;
                    if (NativeContract.Ledger.ContainsConflictHash(_context.Snapshot, hash, System.Runtime.InteropServices.MemoryMarshal.CreateReadOnlySpan(ref System.Runtime.CompilerServices.Unsafe.As<Signer, UInt160>(ref tx.Signers[0]), tx.Signers.Length), mtb))
                    {
                        Log("Invalid request: transaction has on-chain conflict", LogLevel.Warning);
                        return;
                    }

                    if (!await AddTransactionAsync(tx, false))
                        return;
                }
                else
                {
                    if (_system.MemPool.TryGetValue(hash, out tx))
                    {
                        if (tx is null)
                            continue;
                        if (NativeContract.Ledger.ContainsConflictHash(_context.Snapshot, hash, System.Runtime.InteropServices.MemoryMarshal.CreateReadOnlySpan(ref System.Runtime.CompilerServices.Unsafe.As<Signer, UInt160>(ref tx.Signers[0]), tx.Signers.Length), mtb))
                        {
                            Log("Invalid request: transaction has on-chain conflict", LogLevel.Warning);
                            return;
                        }
                        unverified.Add(tx);
                    }
                }
            }
            foreach (Transaction tx in unverified)
                if (!await AddTransactionAsync(tx, true))
                    return;
            if (_context.Transactions.Count < _context.TransactionHashes.Length)
            {
                var missingHashes = new List<UInt256>(_context.TransactionHashes.Length);
                foreach (var hash in _context.TransactionHashes)
                    if (!_context.Transactions.ContainsKey(hash))
                        missingHashes.Add(hash);
                var taskManager = _grainFactory.GetGrain<ITaskManagerGrain>(0);
                var hashDataArray = new byte[missingHashes.Count][];
                for (int i = 0; i < missingHashes.Count; i++)
                    hashDataArray[i] = missingHashes[i].GetSpan().ToArray();
                await taskManager.RestartTasksAsync(hashDataArray, (byte)InventoryType.TX);
            }
        }

        private async Task OnPrepareResponseReceivedAsync(ExtensiblePayload payload, PrepareResponse message)
        {
            if (_context == null)
                return;

            if (message.ViewNumber != _context.ViewNumber) return;
            if (_context.PreparationPayloads[message.ValidatorIndex] != null || _context.NotAcceptingPayloadsDueToViewChanging) return;
            if (_context.PreparationPayloads[_context.Block.PrimaryIndex] != null && !message.PreparationHash.Equals(_context.PreparationPayloads[_context.Block.PrimaryIndex].Hash))
                return;

            ExtendTimerByFactor(2);

            Log($"{nameof(OnPrepareResponseReceivedAsync)}: height={message.BlockIndex} view={message.ViewNumber} index={message.ValidatorIndex}");
            _context.PreparationPayloads[message.ValidatorIndex] = payload;
            if (_context.WatchOnly || _context.CommitSent) return;
            if (_context.RequestSentOrReceived)
                await CheckPreparationsAsync();
        }

        private async Task OnChangeViewReceivedAsync(ExtensiblePayload payload, ChangeView message)
        {
            if (_context == null)
                return;

            if (message.NewViewNumber <= _context.ViewNumber)
                await OnRecoveryRequestReceivedAsync(payload, message);

            if (_context.CommitSent) return;

            var expectedView = _context.GetMessage<ChangeView>(_context.ChangeViewPayloads[message.ValidatorIndex])?.NewViewNumber ?? 0;
            if (message.NewViewNumber <= expectedView)
                return;

            Log($"{nameof(OnChangeViewReceivedAsync)}: height={message.BlockIndex} view={message.ViewNumber} index={message.ValidatorIndex} nv={message.NewViewNumber} reason={message.Reason}");
            _context.ChangeViewPayloads[message.ValidatorIndex] = payload;
            await CheckExpectedViewAsync(message.NewViewNumber);
        }

        private async Task OnCommitReceivedAsync(ExtensiblePayload payload, Commit commit)
        {
            if (_context == null)
                return;

            ref ExtensiblePayload existingCommitPayload = ref _context.CommitPayloads[commit.ValidatorIndex];
            if (existingCommitPayload != null)
            {
                if (existingCommitPayload.Hash != payload.Hash)
                    Log($"Rejected {nameof(Commit)}: height={commit.BlockIndex} index={commit.ValidatorIndex} view={commit.ViewNumber} existingView={_context.GetMessage(existingCommitPayload).ViewNumber}", LogLevel.Warning);
                return;
            }

            if (commit.ViewNumber == _context.ViewNumber)
            {
                ExtendTimerByFactor(4);

                Log($"{nameof(OnCommitReceivedAsync)}: height={commit.BlockIndex} view={commit.ViewNumber} index={commit.ValidatorIndex} nc={_context.CountCommitted} nf={_context.CountFailed}");

                byte[]? hashData = _context.EnsureHeader()?.GetSignData(_system.Settings.Network);
                if (hashData == null)
                {
                    existingCommitPayload = payload;
                }
                else if (Crypto.VerifySignature(hashData, commit.Signature.Span, _context.Validators[commit.ValidatorIndex]))
                {
                    existingCommitPayload = payload;
                    await CheckCommitsAsync();
                }
                return;
            }

            existingCommitPayload = payload;
        }

        private async Task OnRecoveryMessageReceivedAsync(RecoveryMessage message)
        {
            if (_context == null)
                return;

            _isRecovering = true;
            int validChangeViews = 0, totalChangeViews = 0, validPrepReq = 0, totalPrepReq = 0;
            int validPrepResponses = 0, totalPrepResponses = 0, validCommits = 0, totalCommits = 0;

            Log($"{nameof(OnRecoveryMessageReceivedAsync)}: height={message.BlockIndex} view={message.ViewNumber} index={message.ValidatorIndex}");
            try
            {
                if (message.ViewNumber > _context.ViewNumber)
                {
                    if (_context.CommitSent) return;
                    ExtensiblePayload[] changeViewPayloads = message.GetChangeViewPayloads(_context);
                    totalChangeViews = changeViewPayloads.Length;
                    foreach (ExtensiblePayload changeViewPayload in changeViewPayloads)
                        if (await ReverifyAndProcessPayloadAsync(changeViewPayload)) validChangeViews++;
                }
                if (message.ViewNumber == _context.ViewNumber && !_context.NotAcceptingPayloadsDueToViewChanging && !_context.CommitSent)
                {
                    if (!_context.RequestSentOrReceived)
                    {
                        ExtensiblePayload prepareRequestPayload = message.GetPrepareRequestPayload(_context);
                        if (prepareRequestPayload != null)
                        {
                            totalPrepReq = 1;
                            if (await ReverifyAndProcessPayloadAsync(prepareRequestPayload)) validPrepReq++;
                        }
                    }
                    ExtensiblePayload[] prepareResponsePayloads = message.GetPrepareResponsePayloads(_context);
                    totalPrepResponses = prepareResponsePayloads.Length;
                    foreach (ExtensiblePayload prepareResponsePayload in prepareResponsePayloads)
                        if (await ReverifyAndProcessPayloadAsync(prepareResponsePayload)) validPrepResponses++;
                }
                if (message.ViewNumber <= _context.ViewNumber)
                {
                    ExtensiblePayload[] commitPayloads = message.GetCommitPayloadsFromRecoveryMessage(_context);
                    totalCommits = commitPayloads.Length;
                    foreach (ExtensiblePayload commitPayload in commitPayloads)
                        if (await ReverifyAndProcessPayloadAsync(commitPayload)) validCommits++;
                }
            }
            finally
            {
                Log($"Recovery finished: (valid/total) ChgView: {validChangeViews}/{totalChangeViews} PrepReq: {validPrepReq}/{totalPrepReq} PrepResp: {validPrepResponses}/{totalPrepResponses} Commits: {validCommits}/{totalCommits}");
                _isRecovering = false;
            }
        }

        private async Task OnRecoveryRequestReceivedAsync(ExtensiblePayload payload, ConsensusMessage message)
        {
            if (_context == null) return;

            var key = (message.BlockIndex, message.ViewNumber, payload.Hash);
            var now = _timeProvider.UtcNow;
            if (_knownHashes.TryGetValue(key, out var timestamp))
            {
                if ((now - timestamp) < TimeSpan.FromMinutes(5))
                    return;
            }
            _knownHashes[key] = now;

            Log($"{nameof(OnRecoveryRequestReceivedAsync)}: height={message.BlockIndex} index={message.ValidatorIndex} view={message.ViewNumber}");
            if (_context.WatchOnly) return;
            if (!_context.CommitSent)
            {
                bool shouldSendRecovery = false;
                int allowedRecoveryNodeCount = _context.F + 1;
                for (int i = 1; i <= allowedRecoveryNodeCount; i++)
                {
                    var chosenIndex = (message.ValidatorIndex + i) % _context.Validators.Length;
                    if (chosenIndex != _context.MyIndex) continue;
                    shouldSendRecovery = true;
                    break;
                }

                if (!shouldSendRecovery) return;
            }

            await SendDirectlyAsync(_context.MakeRecoveryMessage());
        }

        private async Task<bool> CheckPrepareResponseAsync()
        {
            if (_context == null || _context.TransactionHashes == null)
                return false;

            if (_context.TransactionHashes.Length == _context.Transactions.Count)
            {
                if (_context.IsPrimary || _context.WatchOnly) return true;

                if (_context.GetExpectedBlockSize() > _dbftSettings.MaxBlockSize)
                {
                    Log($"Rejected block: {_context.Block.Index} The size exceed the policy", LogLevel.Warning);
                    await RequestChangeViewAsync(ChangeViewReason.BlockRejectedByPolicy);
                    return false;
                }
                if (_context.GetExpectedBlockSystemFee() > _dbftSettings.MaxBlockSystemFee)
                {
                    Log($"Rejected block: {_context.Block.Index} The system fee exceed the policy", LogLevel.Warning);
                    await RequestChangeViewAsync(ChangeViewReason.BlockRejectedByPolicy);
                    return false;
                }

                ExtendTimerByFactor(2);

                Log($"Sending {nameof(PrepareResponse)}");
                await SendDirectlyAsync(_context.MakePrepareResponse());
                await CheckPreparationsAsync();
            }
            return true;
        }

        private async Task CheckCommitsAsync()
        {
            if (_context == null || _context.TransactionHashes == null)
                return;

            if (_context.CommitPayloads.Count(p => _context.GetMessage(p)?.ViewNumber == _context.ViewNumber) >= _context.M
                && _context.TransactionHashes.All(p => _context.Transactions.ContainsKey(p)))
            {
                _blockReceivedIndex = _context.Block.Index;
                Block block = _context.CreateBlock();
                Log($"Sending {nameof(Block)}: height={block.Index} hash={block.Hash} tx={block.Transactions.Length}");
                var blockchain = _grainFactory.GetGrain<IBlockchainGrain>(0);
                var result = await blockchain.PersistBlockAsync(block);
                if (_options.ValidationMode == NeoValidationMode.None
                    && (result == BlockVerifyResult.Succeed || result == BlockVerifyResult.AlreadyExists))
                {
                    OnPersistCompleted(block);
                }
            }
        }

        private async Task CheckExpectedViewAsync(byte viewNumber)
        {
            if (_context == null || _context.ViewNumber >= viewNumber) return;
            var messages = new List<ChangeView?>(_context.ChangeViewPayloads.Length);
            foreach (var p in _context.ChangeViewPayloads)
                messages.Add(_context.GetMessage<ChangeView>(p));
            if (messages.Count(p => p != null && p.NewViewNumber >= viewNumber) >= _context.M)
            {
                if (!_context.WatchOnly)
                {
                    ChangeView? message = messages[_context.MyIndex];
                    if (message is null || message.NewViewNumber < viewNumber)
                        await SendDirectlyAsync(_context.MakeChangeView(ChangeViewReason.ChangeAgreement));
                }
                InitializeConsensus(viewNumber);
            }
        }

        private async Task CheckPreparationsAsync()
        {
            if (_context == null || _context.TransactionHashes == null)
                return;

            if (_context.PreparationPayloads.Count(p => p != null) >= _context.M
                && _context.TransactionHashes.All(p => _context.Transactions.ContainsKey(p)))
            {
                ExtensiblePayload payload = _context.MakeCommit();
                Log($"Sending {nameof(Commit)}");
                _context.Save();
                await SendDirectlyAsync(payload);
                ChangeTimer(_context.TimePerBlock);
                await CheckCommitsAsync();
            }
        }

        private async Task OnTransactionInternalAsync(Transaction transaction)
        {
            if (_context == null)
                return;

            if (!_context.IsBackup || _context.NotAcceptingPayloadsDueToViewChanging || !_context.RequestSentOrReceived || _context.ResponseSent || _context.BlockSent)
                return;
            if (_context.Transactions.ContainsKey(transaction.Hash)) return;
            if (!_context.TransactionHashes.Contains(transaction.Hash)) return;
            await AddTransactionAsync(transaction, true);
        }

        private async Task<bool> AddTransactionAsync(Transaction tx, bool verify)
        {
            if (_context == null)
                return false;

            if (verify)
            {
                if (tx.SystemFee > _dbftSettings.MaxBlockSystemFee)
                {
                    Log($"Rejected tx: {tx.Hash}, SystemFee {tx.SystemFee} exceeds MaxBlockSystemFee {_dbftSettings.MaxBlockSystemFee}", LogLevel.Warning);
                    return false;
                }

                foreach (var h in tx.GetAttributes<Conflicts>())
                {
                    if (_context.TransactionHashes.Contains(h.Hash))
                    {
                        Log($"Rejected tx: {tx.Hash}, {VerifyResult.HasConflicts}{Environment.NewLine}{tx.ToArray().ToHexString()}", LogLevel.Warning);
                        await RequestChangeViewAsync(ChangeViewReason.TxInvalid);
                        return false;
                    }
                }
                foreach (var pooledTx in _context.Transactions.Values)
                {
                    foreach (var conflictAttr in pooledTx.GetAttributes<Conflicts>())
                    {
                        if (conflictAttr.Hash == tx.Hash)
                        {
                            Log($"Rejected tx: {tx.Hash}, {VerifyResult.HasConflicts}{Environment.NewLine}{tx.ToArray().ToHexString()}", LogLevel.Warning);
                            await RequestChangeViewAsync(ChangeViewReason.TxInvalid);
                            return false;
                        }
                    }
                }

                var conflictingTxs = new List<Transaction>();
                var result = tx.Verify(_system.Settings, _context.Snapshot, _context.VerificationContext, conflictingTxs);
                if (result != VerifyResult.Succeed)
                {
                    Log($"Rejected tx: {tx.Hash}, {result}{Environment.NewLine}{tx.ToArray().ToHexString()}", LogLevel.Warning);
                    await RequestChangeViewAsync(result == VerifyResult.PolicyFail ? ChangeViewReason.TxRejectedByPolicy : ChangeViewReason.TxInvalid);
                    return false;
                }
            }
            _context.Transactions[tx.Hash] = tx;
            _context.VerificationContext.AddTransaction(tx);
            return await CheckPrepareResponseAsync();
        }

        private void InitializeConsensus(byte viewNumber)
        {
            if (_context == null)
                return;

            var myIndexOverride = _state.State.MyIndex >= 0 ? _state.State.MyIndex : (int?)null;
            _context.Reset(viewNumber, myIndexOverride);
            if (viewNumber > 0)
                Log($"View changed: view={viewNumber} primary={_context.Validators[_context.GetPrimaryIndex((byte)(viewNumber - 1u))]}", LogLevel.Warning);
            Log($"Initialize: height={_context.Block.Index} view={viewNumber} index={_context.MyIndex} role={(_context.IsPrimary ? "Primary" : _context.WatchOnly ? "WatchOnly" : "Backup")}");
            if (_context.WatchOnly) return;
            if (_context.IsPrimary)
            {
                if (_isRecovering)
                {
                    ChangeTimer(TimeSpan.FromMilliseconds((int)_context.TimePerBlock.TotalMilliseconds << (viewNumber + 1)));
                }
                else
                {
                    TimeSpan span = _context.TimePerBlock;
                    if (_blockReceivedIndex + 1 == _context.Block.Index && _prepareRequestReceivedBlockIndex + 1 == _context.Block.Index)
                    {
                        var diff = _timeProvider.UtcNow - _prepareRequestReceivedTime;
                        if (diff >= span)
                            span = TimeSpan.Zero;
                        else
                            span -= diff;
                    }
                    ChangeTimer(span);
                }
            }
            else
            {
                ChangeTimer(TimeSpan.FromMilliseconds((int)_context.TimePerBlock.TotalMilliseconds << (viewNumber + 1)));
            }
        }

        private async Task OnTimerAsync()
        {
            if (_context == null || _context.WatchOnly || _context.BlockSent) return;
            if (_timerHeight != _context.Block.Index || _timerViewNumber != _context.ViewNumber) return;

            if (_context.IsPrimary && !_context.RequestSentOrReceived)
            {
                await SendPrepareRequestAsync();
            }
            else if ((_context.IsPrimary && _context.RequestSentOrReceived) || _context.IsBackup)
            {
                if (_context.CommitSent)
                {
                    Log($"Sending {nameof(RecoveryMessage)} to resend {nameof(Commit)}");
                    await SendDirectlyAsync(_context.MakeRecoveryMessage());
                    ChangeTimer(TimeSpan.FromMilliseconds((int)_context.TimePerBlock.TotalMilliseconds << 1));
                }
                else
                {
                    var reason = ChangeViewReason.Timeout;

                    if (_context.Block != null && _context.TransactionHashes?.Length > _context.Transactions?.Count)
                    {
                        reason = ChangeViewReason.TxNotFound;
                    }

                    await RequestChangeViewAsync(reason);
                }
            }
        }

        private async Task SendPrepareRequestAsync()
        {
            if (_context == null)
                return;

            Log($"Sending {nameof(PrepareRequest)}: height={_context.Block.Index} view={_context.ViewNumber}");
            await SendDirectlyAsync(_context.MakePrepareRequest());

            if (_context.Validators.Length == 1)
                await CheckPreparationsAsync();

            if (_context.TransactionHashes.Length > 0)
            {
                foreach (InvPayload payload in InvPayload.CreateGroup(InventoryType.TX, _context.TransactionHashes))
                    await BroadcastMessageAsync(Message.Create(MessageCommand.Inv, payload));
            }

            var delayMs = ((int)_context.TimePerBlock.TotalMilliseconds << (_context.ViewNumber + 1))
                - (_context.ViewNumber == 0 ? _context.TimePerBlock.TotalMilliseconds : 0);
            ChangeTimer(TimeSpan.FromMilliseconds(delayMs));
        }

        private async Task RequestRecoveryAsync()
        {
            if (_context == null)
                return;

            Log($"Sending {nameof(RecoveryRequest)}: height={_context.Block.Index} view={_context.ViewNumber} nc={_context.CountCommitted} nf={_context.CountFailed}");
            await SendDirectlyAsync(_context.MakeRecoveryRequest());
        }

        private async Task RequestChangeViewAsync(ChangeViewReason reason)
        {
            if (_context == null || _context.WatchOnly) return;

            byte expectedView = _context.ViewNumber;
            expectedView++;
            ChangeTimer(TimeSpan.FromMilliseconds((int)_context.TimePerBlock.TotalMilliseconds << (expectedView + 1)));
            if ((_context.CountCommitted + _context.CountFailed) > _context.F)
            {
                await RequestRecoveryAsync();
            }
            else
            {
                Log($"Sending {nameof(ChangeView)}: height={_context.Block.Index} view={_context.ViewNumber} nv={expectedView} nc={_context.CountCommitted} nf={_context.CountFailed} reason={reason}");
                await SendDirectlyAsync(_context.MakeChangeView(reason));
                await CheckExpectedViewAsync(expectedView);
            }
        }

        private async Task<bool> ReverifyAndProcessPayloadAsync(ExtensiblePayload payload)
        {
            var blockchain = _grainFactory.GetGrain<IBlockchainGrain>(0);
            var relayResult = await blockchain.VerifyExtensiblePayloadAsync(payload);
            if (relayResult != VerifyResult.Succeed) return false;
            await OnConsensusPayloadAsync(payload);
            return true;
        }

        private void ChangeTimer(TimeSpan delay)
        {
            if (_context == null)
                return;

            _clockStarted = _timeProvider.UtcNow;
            _expectedDelay = delay;
            _timerHeight = _context.Block.Index;
            _timerViewNumber = _context.ViewNumber;

            var jitterMs = RandomNumberGenerator.GetInt32(0, 500);
            var finalDelay = delay + TimeSpan.FromMilliseconds(jitterMs);

            _timer?.Dispose();
            _timer = this.RegisterGrainTimer(
                _ => OnTimerAsync(),
                new GrainTimerCreationOptions
                {
                    DueTime = finalDelay,
                    Period = Timeout.InfiniteTimeSpan
                });
        }

        private void ExtendTimerByFactor(int maxDelayInBlockTimes)
        {
            if (_context == null)
                return;

            TimeSpan nextDelay = _expectedDelay - (_timeProvider.UtcNow - _clockStarted)
                + TimeSpan.FromMilliseconds(maxDelayInBlockTimes * _context.TimePerBlock.TotalMilliseconds / (double)_context.M);
            if (!_context.WatchOnly && !_context.ViewChanging && !_context.CommitSent && (nextDelay > TimeSpan.Zero))
                ChangeTimer(nextDelay);
        }

        private void OnPersistCompleted(Block block)
        {
            if (_context == null) return;

            if (block.Index <= _lastPersistedIndex) return;

            _lastPersistedIndex = block.Index;
            Log($"Persisted {nameof(Block)}: height={block.Index} hash={block.Hash} tx={block.Transactions.Length} nonce={block.Nonce}");
            var keysToRemove = new List<(uint blockIndex, byte viewNumber, UInt256 hash)>(_knownHashes.Count);
            foreach (var kvp in _knownHashes)
                if (kvp.Key.blockIndex <= block.Index)
                    keysToRemove.Add(kvp.Key);
            foreach (var key in keysToRemove)
                _knownHashes.Remove(key);
            InitializeConsensus(0);
        }

        private async Task SendDirectlyAsync(IInventory inventory)
        {
            if (inventory is ExtensiblePayload payload && payload.Witness is null)
            {
                Log("Skipping consensus payload without witness.", LogLevel.Warning);
                return;
            }
            await BroadcastMessageAsync(Message.Create((MessageCommand)inventory.InventoryType, inventory));
        }

        private async Task BroadcastMessageAsync(Message message)
        {
            var localNode = _grainFactory.GetGrain<ILocalNodeGrain>(0);
            await localNode.BroadcastAsync(message.ToArray(_options.EnableCompression));
        }

        private ConsensusPhase GetPhase()
        {
            if (_context == null)
                return ConsensusPhase.Initial;
            if (_context.ViewChanging)
                return ConsensusPhase.ViewChanging;
            if (_context.CommitSent)
                return ConsensusPhase.CommitSent;
            if (_context.ResponseSent)
                return ConsensusPhase.ResponseSent;
            if (_context.RequestSentOrReceived)
                return ConsensusPhase.RequestSent;
            return _context.IsPrimary ? ConsensusPhase.Primary : _context.WatchOnly ? ConsensusPhase.Initial : ConsensusPhase.Backup;
        }

        private void EnsureContext()
        {
            if (_context != null)
                return;

            var neoSystem = _system is NeoSystemAdapter adapter ? adapter.NeoSystem : (NeoSystem)_system;
            _context = new ConsensusContext(neoSystem, _dbftSettings, _signer);
        }

        private bool IsLedgerReady()
        {
            try
            {
                _ = NativeContract.Ledger.CurrentIndex(_system.StoreView);
                return true;
            }
            catch (KeyNotFoundException)
            {
                return false;
            }
        }

        private async Task EnsureLedgerInitializedAsync()
        {
            if (IsLedgerReady())
                return;

            var blockchain = _grainFactory.GetGrain<IBlockchainGrain>(0);
            await blockchain.GetHeightAsync();

            if (IsLedgerReady())
                return;

            var genesis = _system.GenesisBlock;
            if (genesis is null)
                return;

            using var snapshot = _system.GetSnapshotCache();
            var allApplicationExecuted = new List<Neo.Ledger.Blockchain.ApplicationExecuted>();

            using (var engine = ApplicationEngine.Create(TriggerType.OnPersist, null, snapshot, genesis, _system.Settings, 0))
            {
                engine.LoadScript(NativeContractScripts.OnPersist);
                if (engine.Execute() != VMState.HALT)
                {
                    if (engine.FaultException != null)
                        throw engine.FaultException;
                    throw new InvalidOperationException("OnPersist failed.");
                }

                allApplicationExecuted.Add(new Neo.Ledger.Blockchain.ApplicationExecuted(engine));
            }

            using (var engine = ApplicationEngine.Create(TriggerType.PostPersist, null, snapshot, genesis, _system.Settings, 0))
            {
                engine.LoadScript(NativeContractScripts.PostPersist);
                if (engine.Execute() != VMState.HALT)
                {
                    if (engine.FaultException != null)
                        throw engine.FaultException;
                    throw new InvalidOperationException("PostPersist failed.");
                }

                allApplicationExecuted.Add(new Neo.Ledger.Blockchain.ApplicationExecuted(engine));
            }

            Neo.Ledger.Blockchain.InvokeCommitting(_system, genesis, snapshot, allApplicationExecuted);
            snapshot.Commit();
            Neo.Ledger.Blockchain.InvokeCommitted(_system, genesis);
            _system.MemPool.UpdatePoolForBlockPersisted(genesis, _system.StoreView);
        }

        private ISigner ResolveSigner()
        {
            var signer = SignerManager.GetSignerOrDefault(string.Empty);
            if (signer != null) return signer;

            Log("Consensus signer not found. Running in watch-only mode.", LogLevel.Warning);
            return NullSigner.Instance;
        }

        private static void Log(string message, LogLevel level = LogLevel.Info)
        {
            Utility.Log(nameof(ConsensusGrain), level, message);
        }

        private sealed class NullSigner : ISigner
        {
            public static readonly NullSigner Instance = new();

            public Witness SignExtensiblePayload(ExtensiblePayload payload, DataCache snapshot, uint network) =>
                throw new InvalidOperationException("No signer configured for consensus.");

            public ReadOnlyMemory<byte> SignBlock(Block block, Neo.Cryptography.ECC.ECPoint publicKey, uint network) =>
                throw new InvalidOperationException("No signer configured for consensus.");

            public bool ContainsSignable(Neo.Cryptography.ECC.ECPoint publicKey) => false;
        }
    }
}
#pragma warning restore CS0618
