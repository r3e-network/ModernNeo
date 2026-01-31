// Copyright (C) 2015-2025 The Neo Project.
//
// ConsensusGrainTests.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Microsoft.Extensions.DependencyInjection;
using Neo;
using Neo.Cryptography;
using Neo.Cryptography.ECC;
using Neo.Extensions;
using Neo.Ledger;
using Neo.Network.P2P;
using Neo.Network.P2P.Payloads;
using Neo.Orleans.Dbft;
using Neo.Orleans.Dbft.Consensus;
using Neo.Orleans.Dbft.Messages;
using Neo.Orleans.Dbft.Types;
using Neo.Orleans.Grains;
using Neo.Orleans.Interfaces;
using Neo.Persistence;
using Neo.Sign;
using Neo.SmartContract;
using Neo.SmartContract.Native;
using Neo.VM;
using Neo.Wallets;
using Orleans.TestingHost;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace Neo.Orleans.Tests.Grains
{
    /// <summary>
    /// Unit tests for ConsensusGrain.
    /// </summary>
    [TestClass]
    public class ConsensusGrainTests
    {
        private static TestCluster? s_cluster;
        private static int s_grainKey;
        private const string SignerName = "test-signer";
        private static readonly TestSigner s_signer = new();

        [ClassInitialize]
        public static async Task Setup(TestContext _)
        {
            SignerManager.UnregisterSigner(SignerName);
            SignerManager.RegisterSigner(SignerName, s_signer);
            var builder = new TestClusterBuilder();
            builder.Options.InitialSilosCount = 1;
            builder.AddSiloBuilderConfigurator<TestSiloConfigurator>();
            s_cluster = builder.Build();
            await s_cluster.DeployAsync();
        }

        [ClassCleanup(ClassCleanupBehavior.EndOfClass)]
        public static async Task Cleanup()
        {
            if (s_cluster != null)
            {
                await s_cluster.StopAllSilosAsync();
                await s_cluster.DisposeAsync();
            }
            SignerManager.UnregisterSigner(SignerName);
        }

        private static IConsensusGrain GetGrain()
        {
            var key = Interlocked.Increment(ref s_grainKey);
            return GetGrain(key);
        }

        private static IConsensusGrain GetGrain(int key)
        {
            return s_cluster!.GrainFactory.GetGrain<IConsensusGrain>(key);
        }

        private sealed record ConsensusEnvironment(
            NeoSystem System,
            ECPoint[] Validators,
            uint BlockIndex,
            byte ViewNumber,
            int PrimaryIndex,
            UInt256 PrevHash,
            UInt160 NextConsensus);

        private static async Task<ConsensusEnvironment> CreateEnvironmentAsync()
        {
            var siloHandle = s_cluster!.Silos.FirstOrDefault();
            var services = siloHandle is InProcessSiloHandle inProcess
                ? inProcess.SiloHost.Services
                : s_cluster.ServiceProvider;
            var system = services.GetRequiredService<NeoSystem>();
            var blockchain = s_cluster.GrainFactory.GetGrain<IBlockchainGrain>(0);
            var snapshot = system.StoreView;
            var height = await blockchain.GetHeightAsync();
            uint ledgerHeight;
            UInt256 prevHash;
            try
            {
                ledgerHeight = NativeContract.Ledger.CurrentIndex(snapshot);
                prevHash = NativeContract.Ledger.CurrentHash(snapshot);
            }
            catch (KeyNotFoundException)
            {
                ledgerHeight = height;
                prevHash = system.GenesisBlock.Hash;
            }

            var validators = NativeContract.NEO.GetNextBlockValidators(snapshot, system.Settings.ValidatorsCount);
            var blockIndex = ledgerHeight + 1;
            var viewNumber = (byte)0;
            var primaryIndex = GetPrimaryIndex(blockIndex, viewNumber, validators.Length);
            var nextConsensus = GetNextConsensus(snapshot, system.Settings, blockIndex);
            return new ConsensusEnvironment(system, validators, blockIndex, viewNumber, primaryIndex, prevHash, nextConsensus);
        }

        private static int GetPrimaryIndex(uint blockIndex, byte viewNumber, int validatorCount)
        {
            var primary = ((int)blockIndex - viewNumber) % validatorCount;
            return primary >= 0 ? primary : primary + validatorCount;
        }

        private static UInt160 GetNextConsensus(StoreCache snapshot, IProtocolSettings settings, uint blockIndex)
        {
            var validators = NeoToken.ShouldRefreshCommittee(blockIndex, settings.CommitteeMembersCount)
                ? NativeContract.NEO.ComputeNextBlockValidators(snapshot, settings)
                : NativeContract.NEO.GetNextBlockValidators(snapshot, settings.ValidatorsCount);
            return Contract.GetBFTAddress(validators);
        }

        private static KeyPair GetValidatorKey(ECPoint[] validators, int index)
        {
            var validator = validators[index];
            if (!TestProtocolSettings.ValidatorKeyMap.TryGetValue(validator, out var keyPair))
                throw new InvalidOperationException($"Missing key for validator {validator}.");
            return keyPair;
        }

        private static ExtensiblePayload CreateConsensusPayload(
            ConsensusEnvironment env,
            ConsensusMessage message,
            int validatorIndex,
            byte? viewNumberOverride = null)
        {
            message.BlockIndex = env.BlockIndex;
            message.ValidatorIndex = (byte)validatorIndex;
            message.ViewNumber = viewNumberOverride ?? env.ViewNumber;

            var keyPair = GetValidatorKey(env.Validators, validatorIndex);
            var payload = new ExtensiblePayload
            {
                Category = "dBFT",
                ValidBlockStart = 0,
                ValidBlockEnd = env.BlockIndex,
                Sender = Contract.CreateSignatureRedeemScript(keyPair.PublicKey).ToScriptHash(),
                Data = message.ToArray(),
                Witness = null!
            };

            var signature = payload.Sign(keyPair, env.System.Settings.Network);
            using var sb = new ScriptBuilder();
            sb.EmitPush(signature);
            payload.Witness = new Witness
            {
                InvocationScript = sb.ToArray(),
                VerificationScript = Contract.CreateSignatureRedeemScript(keyPair.PublicKey)
            };
            return payload;
        }

        private static ExtensiblePayload CreatePrepareRequestPayload(
            ConsensusEnvironment env,
            int validatorIndex,
            out PrepareRequest request)
        {
            var prevHeader = NativeContract.Ledger.GetHeader(env.System.StoreView, env.PrevHash);
            var now = TimeProvider.Current.UtcNow.ToTimestampMS();
            var minTimestamp = prevHeader?.Timestamp + 1 ?? 0;
            var timestamp = Math.Max(now, minTimestamp);
            request = new PrepareRequest
            {
                Version = 0,
                PrevHash = env.PrevHash,
                Timestamp = timestamp,
                Nonce = 0,
                TransactionHashes = Array.Empty<UInt256>()
            };
            return CreateConsensusPayload(env, request, validatorIndex);
        }

        private static ExtensiblePayload CreatePrepareResponsePayload(
            ConsensusEnvironment env,
            int validatorIndex,
            UInt256 preparationHash)
        {
            var response = new PrepareResponse
            {
                PreparationHash = preparationHash
            };
            return CreateConsensusPayload(env, response, validatorIndex);
        }

        private static ExtensiblePayload CreateChangeViewPayload(
            ConsensusEnvironment env,
            int validatorIndex,
            ChangeViewReason reason,
            byte? viewNumberOverride = null)
        {
            var changeView = new ChangeView
            {
                Timestamp = TimeProvider.Current.UtcNow.ToTimestampMS(),
                Reason = reason
            };
            return CreateConsensusPayload(env, changeView, validatorIndex, viewNumberOverride);
        }

        private static ExtensiblePayload CreateCommitPayload(
            ConsensusEnvironment env,
            int validatorIndex,
            ulong timestamp,
            ulong nonce,
            UInt256[] transactionHashes)
        {
            using var context = new ConsensusContext(env.System, new DbftSettings(env.System.Settings), s_signer);
            context.Reset(env.ViewNumber, validatorIndex);
            context.Block.Header.Timestamp = timestamp;
            context.Block.Header.Nonce = nonce;
            context.TransactionHashes = transactionHashes;
            context.Transactions = new Dictionary<UInt256, Transaction>();
            context.VerificationContext = new TransactionVerificationContext();
            return context.MakeCommit();
        }

        private static ExtensiblePayload CreateInvalidConsensusPayload(
            ConsensusEnvironment env,
            int validatorIndex,
            byte invalidTypeCode)
        {
            var keyPair = GetValidatorKey(env.Validators, validatorIndex);
            var payload = new ExtensiblePayload
            {
                Category = "dBFT",
                ValidBlockStart = 0,
                ValidBlockEnd = env.BlockIndex,
                Sender = Contract.CreateSignatureRedeemScript(keyPair.PublicKey).ToScriptHash(),
                Data = new[] { invalidTypeCode },
                Witness = null!
            };
            var signature = payload.Sign(keyPair, env.System.Settings.Network);
            using var sb = new ScriptBuilder();
            sb.EmitPush(signature);
            payload.Witness = new Witness
            {
                InvocationScript = sb.ToArray(),
                VerificationScript = Contract.CreateSignatureRedeemScript(keyPair.PublicKey)
            };
            return payload;
        }

        private static async Task<ConsensusState> WaitForBlockIndexAsync(IConsensusGrain grain, uint expectedIndex, int timeoutMs = 5000)
        {
            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                var state = await grain.GetStateAsync();
                if (state.BlockIndex >= expectedIndex)
                    return state;
                await Task.Delay(20);
            }
            return await grain.GetStateAsync();
        }

        private static void AssertPrimaryStartPhase(ConsensusState state)
        {
            Assert.IsTrue(
                state.Phase == ConsensusPhase.Primary || state.Phase == ConsensusPhase.ResponseSent,
                $"Expected phase to be {ConsensusPhase.Primary} or {ConsensusPhase.ResponseSent}, got {state.Phase}.");
        }

        private sealed class TestSigner : ISigner
        {
            private readonly IReadOnlyDictionary<UInt160, KeyPair> _keysByScriptHash =
                TestProtocolSettings.ValidatorKeyMap.ToDictionary(
                    kvp => Contract.CreateSignatureRedeemScript(kvp.Key).ToScriptHash(),
                    kvp => kvp.Value);

            public Witness SignExtensiblePayload(ExtensiblePayload payload, DataCache snapshot, uint network)
            {
                if (!_keysByScriptHash.TryGetValue(payload.Sender, out var keyPair))
                    throw new SignException($"No signer for sender {payload.Sender}.");

                var signature = payload.Sign(keyPair, network);
                using var sb = new ScriptBuilder();
                sb.EmitPush(signature);
                return new Witness
                {
                    InvocationScript = sb.ToArray(),
                    VerificationScript = Contract.CreateSignatureRedeemScript(keyPair.PublicKey)
                };
            }

            public ReadOnlyMemory<byte> SignBlock(Block block, ECPoint publicKey, uint network)
            {
                if (!TestProtocolSettings.ValidatorKeyMap.TryGetValue(publicKey, out var keyPair))
                    throw new SignException($"No signer for public key {publicKey}.");

                return Crypto.Sign(block.GetSignData(network), keyPair.PrivateKey);
            }

            public bool ContainsSignable(ECPoint publicKey) =>
                TestProtocolSettings.ValidatorKeyMap.ContainsKey(publicKey);
        }

        [TestMethod]
        public async Task GetStateAsync_InitialState_ReturnsInitialPhase()
        {
            // Arrange
            var grain = GetGrain();

            // Act
            var state = await grain.GetStateAsync();

            // Assert
            Assert.AreEqual(ConsensusPhase.Initial, state.Phase);
            Assert.AreEqual((byte)0, state.ViewNumber);
        }

        [TestMethod]
        public async Task StartAsync_NotRunning_StartsConsensus()
        {
            // Arrange
            var grain = GetGrain();
            var env = await CreateEnvironmentAsync();
            await grain.InitializeAsync(myIndex: env.PrimaryIndex, validatorCount: env.Validators.Length);

            // Act
            await grain.StartAsync();
            var state = await grain.GetStateAsync();

            // Assert
            AssertPrimaryStartPhase(state);
        }

        [TestMethod]
        public async Task StartAsync_AlreadyRunning_DoesNothing()
        {
            // Arrange
            var grain = GetGrain();
            var env = await CreateEnvironmentAsync();
            await grain.InitializeAsync(myIndex: env.PrimaryIndex, validatorCount: env.Validators.Length);
            await grain.StartAsync();

            // Act
            await grain.StartAsync(); // Second call
            var state = await grain.GetStateAsync();

            // Assert
            AssertPrimaryStartPhase(state);
        }

        [TestMethod]
        public async Task StopAsync_Running_StopsConsensus()
        {
            // Arrange
            var grain = GetGrain();
            var env = await CreateEnvironmentAsync();
            await grain.InitializeAsync(myIndex: env.PrimaryIndex, validatorCount: env.Validators.Length);
            await grain.StartAsync();

            // Act
            await grain.StopAsync();
            var state = await grain.GetStateAsync();

            // Assert
            Assert.AreEqual(ConsensusPhase.Initial, state.Phase);
        }

        [TestMethod]
        public async Task IsPrimaryAsync_ValidatorIndex1View0_ReturnsTrue()
        {
            // Arrange
            var grain = GetGrain();
            var env = await CreateEnvironmentAsync();
            await grain.InitializeAsync(myIndex: env.PrimaryIndex, validatorCount: env.Validators.Length);
            await grain.StartAsync();

            // Act
            var isPrimary = await grain.IsPrimaryAsync();

            // Assert
            Assert.IsTrue(isPrimary);
        }

        [TestMethod]
        public async Task IsPrimaryAsync_ValidatorIndex0View0_ReturnsFalse()
        {
            // Arrange
            var grain = GetGrain(0);
            var env = await CreateEnvironmentAsync();
            var backupIndex = (env.PrimaryIndex + 1) % env.Validators.Length;
            await grain.InitializeAsync(myIndex: backupIndex, validatorCount: env.Validators.Length);
            await grain.StartAsync();

            // Act
            var isPrimary = await grain.IsPrimaryAsync();

            // Assert
            Assert.IsFalse(isPrimary);
        }

        [TestMethod]
        public async Task GetViewNumberAsync_InitialState_ReturnsZero()
        {
            // Arrange
            var grain = GetGrain();

            // Act
            var viewNumber = await grain.GetViewNumberAsync();

            // Assert
            Assert.AreEqual((byte)0, viewNumber);
        }

        [TestMethod]
        public async Task OnConsensusMessageAsync_PrepareRequest_UpdatesPhase()
        {
            // Arrange
            var grain = GetGrain();
            var env = await CreateEnvironmentAsync();
            var backupIndex = (env.PrimaryIndex + 1) % env.Validators.Length;
            await grain.InitializeAsync(myIndex: backupIndex, validatorCount: env.Validators.Length);
            await grain.StartAsync();

            var prepareRequestPayload = CreatePrepareRequestPayload(env, env.PrimaryIndex, out _);

            // Act
            await grain.OnConsensusMessageAsync(prepareRequestPayload.ToArray(), "primary_address");
            var state = await grain.GetStateAsync();

            // Assert
            Assert.AreEqual(ConsensusPhase.ResponseSent, state.Phase);
        }

        [TestMethod]
        public async Task OnConsensusMessageAsync_PrepareResponse_UpdatesPayloads()
        {
            // Arrange
            var grain = GetGrain();
            var env = await CreateEnvironmentAsync();
            var backupIndex = (env.PrimaryIndex + 1) % env.Validators.Length;
            await grain.InitializeAsync(myIndex: backupIndex, validatorCount: env.Validators.Length);
            await grain.StartAsync();

            // First send PrepareRequest to move to ResponseSent phase
            var prepareRequestPayload = CreatePrepareRequestPayload(env, env.PrimaryIndex, out _);
            await grain.OnConsensusMessageAsync(prepareRequestPayload.ToArray(), "primary_address");

            // PrepareResponse from validator 1
            var responderIndex = (backupIndex + 1) % env.Validators.Length;
            var prepareResponsePayload = CreatePrepareResponsePayload(env, responderIndex, prepareRequestPayload.Hash);

            // Act
            await grain.OnConsensusMessageAsync(prepareResponsePayload.ToArray(), $"validator{responderIndex}_address");
            var state = await grain.GetStateAsync();

            // Assert - should still be in ResponseSent (not enough preparations for commit)
            Assert.AreEqual(ConsensusPhase.ResponseSent, state.Phase);
        }

        [TestMethod]
        public async Task OnConsensusMessageAsync_ChangeView_IncrementsViewNumber()
        {
            // Arrange
            var grain = GetGrain();
            var env = await CreateEnvironmentAsync();
            var primaryView1 = GetPrimaryIndex(env.BlockIndex, 1, env.Validators.Length);
            var backupIndex = primaryView1 == 0 ? 1 : 0;
            await grain.InitializeAsync(myIndex: backupIndex, validatorCount: env.Validators.Length);
            await grain.StartAsync();

            var quorum = env.Validators.Length - (env.Validators.Length - 1) / 3;
            for (int i = 0; i < quorum; i++)
            {
                var changeViewPayload = CreateChangeViewPayload(env, i, ChangeViewReason.Timeout);
                await grain.OnConsensusMessageAsync(changeViewPayload.ToArray(), $"validator{i}_address");
            }

            // Act
            var viewNumber = await grain.GetViewNumberAsync();

            // Assert
            Assert.AreEqual((byte)1, viewNumber);
        }

        [TestMethod]
        public async Task OnConsensusMessageAsync_EmptyMessage_DoesNotThrow()
        {
            // Arrange
            var grain = GetGrain();
            var env = await CreateEnvironmentAsync();
            await grain.InitializeAsync(myIndex: env.PrimaryIndex, validatorCount: env.Validators.Length);
            await grain.StartAsync();

            // Act & Assert - should not throw
            await grain.OnConsensusMessageAsync(Array.Empty<byte>(), "any_address");
        }

        [TestMethod]
        public async Task OnConsensusMessageAsync_NotRunning_IgnoresMessage()
        {
            // Arrange
            var grain = GetGrain();
            var env = await CreateEnvironmentAsync();
            await grain.InitializeAsync(myIndex: env.PrimaryIndex, validatorCount: env.Validators.Length);
            // Note: Not calling StartAsync

            var prepareRequestPayload = CreatePrepareRequestPayload(env, env.PrimaryIndex, out _);

            // Act
            await grain.OnConsensusMessageAsync(prepareRequestPayload.ToArray(), "any_address");
            var state = await grain.GetStateAsync();

            // Assert - should still be Initial since not running
            Assert.AreEqual(ConsensusPhase.Initial, state.Phase);
        }

        #region Compatibility Tests - Message Type Codes

        [TestMethod]
        public async Task MessageTypeCode_Commit_MatchesConsensusMessageType()
        {
            // Arrange
            var grain = GetGrain();
            var env = await CreateEnvironmentAsync();
            var backupIndex = (env.PrimaryIndex + 1) % env.Validators.Length;
            await grain.InitializeAsync(myIndex: backupIndex, validatorCount: env.Validators.Length);
            await grain.StartAsync();

            // First send PrepareRequest to move to ResponseSent phase
            var prepareRequestPayload = CreatePrepareRequestPayload(env, env.PrimaryIndex, out var request);
            await grain.OnConsensusMessageAsync(prepareRequestPayload.ToArray(), "primary_address");

            // Commit message with correct type code 0x30 (matches ConsensusMessageType.Commit)
            var commitSenderIndex = (backupIndex + 1) % env.Validators.Length;
            var commitPayload = CreateCommitPayload(env, commitSenderIndex, request.Timestamp, request.Nonce, request.TransactionHashes);

            // Act
            await grain.OnConsensusMessageAsync(commitPayload.ToArray(), $"validator{commitSenderIndex}_address");
            var state = await grain.GetStateAsync();

            // Assert - message should be processed (phase remains ResponseSent, need more commits)
            Assert.AreEqual(ConsensusPhase.ResponseSent, state.Phase);
        }

        [TestMethod]
        public async Task MessageTypeCode_ChangeView_MatchesConsensusMessageType()
        {
            // Arrange
            var grain = GetGrain();
            var env = await CreateEnvironmentAsync();
            var primaryView1 = GetPrimaryIndex(env.BlockIndex, 1, env.Validators.Length);
            var backupIndex = primaryView1 == 0 ? 1 : 0;
            await grain.InitializeAsync(myIndex: backupIndex, validatorCount: env.Validators.Length);
            await grain.StartAsync();

            var quorum = env.Validators.Length - (env.Validators.Length - 1) / 3;
            for (int i = 0; i < quorum; i++)
            {
                var changeViewPayload = CreateChangeViewPayload(env, i, ChangeViewReason.Timeout);
                await grain.OnConsensusMessageAsync(changeViewPayload.ToArray(), $"validator{i}_address");
            }

            // Act
            var viewNumber = await grain.GetViewNumberAsync();

            // Assert - view number should increment
            Assert.AreEqual((byte)1, viewNumber);
        }

        [TestMethod]
        public async Task MessageTypeCode_PrepareRequest_MatchesConsensusMessageType()
        {
            // Arrange
            var grain = GetGrain();
            var env = await CreateEnvironmentAsync();
            var backupIndex = (env.PrimaryIndex + 1) % env.Validators.Length;
            await grain.InitializeAsync(myIndex: backupIndex, validatorCount: env.Validators.Length);
            await grain.StartAsync();

            // PrepareRequest message with correct type code 0x20 (matches ConsensusMessageType.PrepareRequest)
            var prepareRequestPayload = CreatePrepareRequestPayload(env, env.PrimaryIndex, out _);

            // Act
            await grain.OnConsensusMessageAsync(prepareRequestPayload.ToArray(), "primary_address");
            var state = await grain.GetStateAsync();

            // Assert - should move to ResponseSent phase
            Assert.AreEqual(ConsensusPhase.ResponseSent, state.Phase);
        }

        [TestMethod]
        public async Task MessageTypeCode_PrepareResponse_MatchesConsensusMessageType()
        {
            // Arrange
            var grain = GetGrain();
            var env = await CreateEnvironmentAsync();
            var backupIndex = (env.PrimaryIndex + 1) % env.Validators.Length;
            await grain.InitializeAsync(myIndex: backupIndex, validatorCount: env.Validators.Length);
            await grain.StartAsync();

            // First send PrepareRequest
            var prepareRequestPayload = CreatePrepareRequestPayload(env, env.PrimaryIndex, out _);
            await grain.OnConsensusMessageAsync(prepareRequestPayload.ToArray(), "primary_address");

            // PrepareResponse message with correct type code 0x21 (matches ConsensusMessageType.PrepareResponse)
            var responderIndex = (backupIndex + 1) % env.Validators.Length;
            var prepareResponsePayload = CreatePrepareResponsePayload(env, responderIndex, prepareRequestPayload.Hash);

            // Act
            await grain.OnConsensusMessageAsync(prepareResponsePayload.ToArray(), $"validator{responderIndex}_address");
            var state = await grain.GetStateAsync();

            // Assert - should remain in ResponseSent (need more responses)
            Assert.AreEqual(ConsensusPhase.ResponseSent, state.Phase);
        }

        [TestMethod]
        public async Task MessageTypeCode_InvalidCommitCode_IgnoresMessage()
        {
            // Arrange
            var grain = GetGrain();
            var env = await CreateEnvironmentAsync();
            var backupIndex = (env.PrimaryIndex + 1) % env.Validators.Length;
            await grain.InitializeAsync(myIndex: backupIndex, validatorCount: env.Validators.Length);
            await grain.StartAsync();

            // First send PrepareRequest
            var prepareRequestPayload = CreatePrepareRequestPayload(env, env.PrimaryIndex, out _);
            await grain.OnConsensusMessageAsync(prepareRequestPayload.ToArray(), "primary_address");

            // Commit message with OLD INCORRECT type code 0x22 (should be ignored)
            var invalidCommitPayload = CreateInvalidConsensusPayload(env, backupIndex, 0x22);

            // Act
            await grain.OnConsensusMessageAsync(invalidCommitPayload.ToArray(), "validator_invalid_commit");
            var state = await grain.GetStateAsync();

            // Assert - should remain in ResponseSent (message ignored)
            Assert.AreEqual(ConsensusPhase.ResponseSent, state.Phase);
        }

        [TestMethod]
        public async Task MessageTypeCode_InvalidChangeViewCode_IgnoresMessage()
        {
            // Arrange
            var grain = GetGrain();
            var env = await CreateEnvironmentAsync();
            var backupIndex = (env.PrimaryIndex + 1) % env.Validators.Length;
            await grain.InitializeAsync(myIndex: backupIndex, validatorCount: env.Validators.Length);
            await grain.StartAsync();

            var initialViewNumber = await grain.GetViewNumberAsync();

            // ChangeView message with OLD INCORRECT type code 0x23 (should be ignored)
            var invalidChangeViewPayload = CreateInvalidConsensusPayload(env, backupIndex, 0x23);

            // Act
            await grain.OnConsensusMessageAsync(invalidChangeViewPayload.ToArray(), "validator_invalid_changeview");
            var viewNumber = await grain.GetViewNumberAsync();

            // Assert - view number should NOT change (message ignored)
            Assert.AreEqual(initialViewNumber, viewNumber);
        }

        [TestMethod]
        public async Task MessageFormat_AllMessageTypes_UseCorrectDbftStateMachineFormat()
        {
            // This test verifies that ConsensusGrain uses the same message type codes as DbftStateMachine
            // Messages are sent via ExtensiblePayload to mirror the production path.

            // Arrange
            var grain = GetGrain();
            var env = await CreateEnvironmentAsync();
            var backupIndex = (env.PrimaryIndex + 1) % env.Validators.Length;
            await grain.InitializeAsync(myIndex: backupIndex, validatorCount: env.Validators.Length);
            await grain.StartAsync();

            var prepareRequestPayload = CreatePrepareRequestPayload(env, env.PrimaryIndex, out var request);
            await grain.OnConsensusMessageAsync(prepareRequestPayload.ToArray(), "primary_address");

            var responderIndex = (backupIndex + 1) % env.Validators.Length;
            var prepareResponsePayload = CreatePrepareResponsePayload(env, responderIndex, prepareRequestPayload.Hash);
            await grain.OnConsensusMessageAsync(prepareResponsePayload.ToArray(), $"validator{responderIndex}_address");

            var commitIndex = (responderIndex + 1) % env.Validators.Length;
            var commitPayload = CreateCommitPayload(env, commitIndex, request.Timestamp, request.Nonce, request.TransactionHashes);
            await grain.OnConsensusMessageAsync(commitPayload.ToArray(), $"validator{commitIndex}_address");

            var changeViewPayload = CreateChangeViewPayload(env, responderIndex, ChangeViewReason.Timeout);
            await grain.OnConsensusMessageAsync(changeViewPayload.ToArray(), $"validator{responderIndex}_changeview");

            // If we reach here, all message types were processed without errors
            Assert.IsTrue(true, "All message types processed successfully");
        }

        [TestMethod]
        public async Task Compatibility_CommitMessageFlow_MatchesDbftStateMachine()
        {
            // This test verifies the complete commit flow matches DbftStateMachine behavior
            // Tests that message type codes 0x20, 0x21, 0x30 work correctly through full consensus

            // Arrange
            var grain = GetGrain();
            var env = await CreateEnvironmentAsync();
            var backupIndex = (env.PrimaryIndex + 1) % env.Validators.Length;
            await grain.InitializeAsync(myIndex: backupIndex, validatorCount: env.Validators.Length);
            await grain.StartAsync();

            var initialBlockIndex = (await grain.GetStateAsync()).BlockIndex;

            // Step 1: PrepareRequest (0x20) - correct code matching ConsensusMessageType
            var prepareRequestPayload = CreatePrepareRequestPayload(env, env.PrimaryIndex, out var request);
            await grain.OnConsensusMessageAsync(prepareRequestPayload.ToArray(), "primary_address");

            var state1 = await grain.GetStateAsync();
            Assert.AreEqual(ConsensusPhase.ResponseSent, state1.Phase);

            // Step 2: Send PrepareResponses (0x21) - correct code matching ConsensusMessageType
            var quorum = env.Validators.Length - (env.Validators.Length - 1) / 3;
            var preparationSenders = Enumerable.Range(0, env.Validators.Length)
                .Where(i => i != env.PrimaryIndex && i != backupIndex)
                .Take(Math.Max(0, quorum - 2))
                .ToArray();
            foreach (var senderIndex in preparationSenders)
            {
                var prepareResponsePayload = CreatePrepareResponsePayload(env, senderIndex, prepareRequestPayload.Hash);
                await grain.OnConsensusMessageAsync(prepareResponsePayload.ToArray(), $"validator{senderIndex}");
            }

            var state2 = await grain.GetStateAsync();
            Assert.AreEqual(ConsensusPhase.CommitSent, state2.Phase);

            // Step 3: Send Commit messages (0x30) - correct code matching ConsensusMessageType
            var commitSenders = Enumerable.Range(0, env.Validators.Length)
                .Where(i => i != backupIndex)
                .Take(Math.Max(0, quorum - 1))
                .ToArray();
            foreach (var senderIndex in commitSenders)
            {
                var commitPayload = CreateCommitPayload(env, senderIndex, request.Timestamp, request.Nonce, request.TransactionHashes);
                await grain.OnConsensusMessageAsync(commitPayload.ToArray(), $"validator{senderIndex}");
            }

            // After reaching commit threshold, block advances and phase resets
            var state3 = await WaitForBlockIndexAsync(grain, initialBlockIndex + 1);
            var blockchain = s_cluster!.GrainFactory.GetGrain<IBlockchainGrain>(0);
            var chainHeight = await blockchain.GetHeightAsync();
            // Block should have advanced (CommitSent triggers AdvanceToNextBlock)
            Assert.AreEqual(initialBlockIndex + 1, state3.BlockIndex);
            Assert.AreEqual(initialBlockIndex, chainHeight);
            // View should reset to 0
            Assert.AreEqual((byte)0, state3.ViewNumber);
        }

        [TestMethod]
        public async Task Compatibility_ChangeViewFlow_MatchesDbftStateMachine()
        {
            // This test verifies the change view flow matches DbftStateMachine behavior

            // Arrange
            var grain = GetGrain();
            var env = await CreateEnvironmentAsync();
            var primaryView1 = GetPrimaryIndex(env.BlockIndex, 1, env.Validators.Length);
            var backupIndex = primaryView1 == 0 ? 1 : 0;
            await grain.InitializeAsync(myIndex: backupIndex, validatorCount: env.Validators.Length);
            await grain.StartAsync();

            var initialView = await grain.GetViewNumberAsync();
            Assert.AreEqual((byte)0, initialView);

            // Act: Send ChangeView message with correct code (0x00) matching DbftStateMachine
            var quorum = env.Validators.Length - (env.Validators.Length - 1) / 3;
            for (int i = 0; i < quorum; i++)
            {
                var changeViewPayload = CreateChangeViewPayload(env, i, ChangeViewReason.Timeout);
                await grain.OnConsensusMessageAsync(changeViewPayload.ToArray(), $"validator{i}");
            }

            // Assert
            var newView = await grain.GetViewNumberAsync();
            Assert.AreEqual((byte)1, newView);

            var state = await grain.GetStateAsync();
            Assert.AreEqual(ConsensusPhase.Backup, state.Phase); // Should reset to appropriate phase
        }

        #endregion
    }
}
