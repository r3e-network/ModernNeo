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

using Neo.Orleans.Interfaces;
using Neo.Orleans.States;
using Orleans.Runtime;

namespace Neo.Orleans.Grains
{
    /// <summary>
    /// Orleans Grain implementation for dBFT consensus management.
    /// Replaces Akka.NET ConsensusService Actor.
    /// </summary>
    public class ConsensusGrain : Grain, IConsensusGrain
    {
        private readonly IPersistentState<ConsensusGrainState> _state;
        private readonly IGrainFactory _grainFactory;

        public ConsensusGrain(
            [PersistentState("consensus", "ConsensusStore")]
            IPersistentState<ConsensusGrainState> state,
            IGrainFactory grainFactory)
        {
            _state = state;
            _grainFactory = grainFactory;
        }

        public async Task StartAsync()
        {
            if (_state.State.IsRunning)
                return;

            _state.State.IsRunning = true;
            _state.State.ViewNumber = 0;
            _state.State.Phase = ConsensusPhase.Initial;
            _state.State.ViewStartTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            // Get current block height from blockchain
            var blockchain = _grainFactory.GetGrain<IBlockchainGrain>(0);
            _state.State.BlockIndex = await blockchain.GetHeightAsync() + 1;

            // Determine if we are primary for view 0
            UpdatePhaseForView();

            await _state.WriteStateAsync();
        }

        public async Task StopAsync()
        {
            _state.State.IsRunning = false;
            _state.State.Phase = ConsensusPhase.Initial;
            ClearPayloads();
            await _state.WriteStateAsync();
        }

        public async Task OnConsensusMessageAsync(byte[] message, string senderAddress)
        {
            if (!_state.State.IsRunning || message.Length == 0)
                return;

            // Message format: [type:1][validator_index:1][payload:...]
            var messageType = message[0];
            var validatorIndex = message.Length > 1 ? (int)message[1] : -1;
            var payload = message.Length > 2 ? message[2..] : Array.Empty<byte>();

            switch (messageType)
            {
                case 0x20: // PrepareRequest
                    await HandlePrepareRequest(validatorIndex, payload);
                    break;
                case 0x21: // PrepareResponse
                    await HandlePrepareResponse(validatorIndex, payload);
                    break;
                case 0x22: // Commit
                    await HandleCommit(validatorIndex, payload);
                    break;
                case 0x23: // ChangeView
                    await HandleChangeView(validatorIndex, payload);
                    break;
            }
        }

        public Task<ConsensusState> GetStateAsync() =>
            Task.FromResult(new ConsensusState(
                _state.State.ViewNumber,
                _state.State.BlockIndex,
                _state.State.Phase,
                IsPrimaryForView(_state.State.ViewNumber)));

        public Task<byte> GetViewNumberAsync() =>
            Task.FromResult(_state.State.ViewNumber);

        public Task<bool> IsPrimaryAsync() =>
            Task.FromResult(IsPrimaryForView(_state.State.ViewNumber));

        /// <summary>
        /// Initializes validator configuration.
        /// </summary>
        public async Task InitializeAsync(int myIndex, int validatorCount)
        {
            _state.State.MyIndex = myIndex;
            _state.State.ValidatorCount = validatorCount;
            await _state.WriteStateAsync();
        }

        private async Task HandlePrepareRequest(int validatorIndex, byte[] payload)
        {
            if (_state.State.Phase != ConsensusPhase.Initial &&
                _state.State.Phase != ConsensusPhase.Backup)
                return;

            // Store the prepare request
            _state.State.PrepareRequestPayloads[validatorIndex] = payload;
            _state.State.ProposedBlockHash = payload;
            _state.State.Phase = ConsensusPhase.RequestSent;

            await _state.WriteStateAsync();

            // If we are backup, send prepare response
            if (!IsPrimaryForView(_state.State.ViewNumber))
            {
                await SendPrepareResponse();
            }
        }

        private async Task HandlePrepareResponse(int validatorIndex, byte[] payload)
        {
            _state.State.PrepareResponsePayloads[validatorIndex] = payload;
            await _state.WriteStateAsync();

            // Check if we have M responses (2f+1)
            var threshold = (_state.State.ValidatorCount * 2 / 3) + 1;
            if (_state.State.PrepareResponsePayloads.Count >= threshold &&
                _state.State.Phase == ConsensusPhase.RequestSent)
            {
                _state.State.Phase = ConsensusPhase.ResponseSent;
                await _state.WriteStateAsync();
                await SendCommit();
            }
        }

        private async Task HandleCommit(int validatorIndex, byte[] payload)
        {
            _state.State.CommitPayloads[validatorIndex] = payload;
            await _state.WriteStateAsync();

            // Check if we have M commits (2f+1)
            var threshold = (_state.State.ValidatorCount * 2 / 3) + 1;
            if (_state.State.CommitPayloads.Count >= threshold &&
                _state.State.Phase == ConsensusPhase.ResponseSent)
            {
                _state.State.Phase = ConsensusPhase.CommitSent;
                await _state.WriteStateAsync();

                // Block can be finalized - advance to next block
                await AdvanceToNextBlock();
            }
        }

        private async Task HandleChangeView(int validatorIndex, byte[] payload)
        {
            // Simple view change: increment view number and reset
            _state.State.ViewNumber++;
            _state.State.Phase = ConsensusPhase.ViewChanging;
            _state.State.ViewStartTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            ClearPayloads();
            UpdatePhaseForView();
            await _state.WriteStateAsync();
        }

        private async Task SendPrepareResponse()
        {
            // Would broadcast prepare response to peers
            _state.State.PrepareResponsePayloads[_state.State.MyIndex] =
                _state.State.ProposedBlockHash;
            await _state.WriteStateAsync();
        }

        private async Task SendCommit()
        {
            // Would broadcast commit to peers
            _state.State.CommitPayloads[_state.State.MyIndex] =
                _state.State.ProposedBlockHash;
            await _state.WriteStateAsync();
        }

        private async Task AdvanceToNextBlock()
        {
            _state.State.BlockIndex++;
            _state.State.ViewNumber = 0;
            _state.State.Phase = ConsensusPhase.Initial;
            _state.State.ViewStartTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            ClearPayloads();
            UpdatePhaseForView();
            await _state.WriteStateAsync();
        }

        private void ClearPayloads()
        {
            _state.State.PrepareRequestPayloads.Clear();
            _state.State.PrepareResponsePayloads.Clear();
            _state.State.CommitPayloads.Clear();
            _state.State.ProposedBlockHash = Array.Empty<byte>();
        }

        private void UpdatePhaseForView()
        {
            if (_state.State.MyIndex < 0)
                return;

            if (IsPrimaryForView(_state.State.ViewNumber))
                _state.State.Phase = ConsensusPhase.Primary;
            else
                _state.State.Phase = ConsensusPhase.Backup;
        }

        private bool IsPrimaryForView(byte viewNumber)
        {
            if (_state.State.MyIndex < 0 || _state.State.ValidatorCount == 0)
                return false;

            var primaryIndex = (int)((_state.State.BlockIndex + viewNumber) % (uint)_state.State.ValidatorCount);
            return primaryIndex == _state.State.MyIndex;
        }
    }
}
